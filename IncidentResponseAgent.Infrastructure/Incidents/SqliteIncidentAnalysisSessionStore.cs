using IncidentResponseAgent.Application.Incidents;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace IncidentResponseAgent.Infrastructure.Incidents;

public sealed class SqliteIncidentAnalysisSessionStore : IIncidentAnalysisSessionStore
{
	private readonly SemaphoreSlim _schemaLock = new(1, 1);
	private readonly ILogger<SqliteIncidentAnalysisSessionStore> _logger;
	private readonly IncidentStorageOptions _options;
	private bool _schemaReady;

	public SqliteIncidentAnalysisSessionStore(
		IOptions<IncidentStorageOptions> options,
		ILogger<SqliteIncidentAnalysisSessionStore> logger)
	{
		_options = options.Value ?? new IncidentStorageOptions();
		_logger = logger;
	}

	public async Task<IncidentAnalysisSessionContext> GetOrCreateAsync(string? sessionId, CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();
		await EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);

		var key = string.IsNullOrWhiteSpace(sessionId)
			? Guid.NewGuid().ToString("N")
			: sessionId.Trim();

		await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
		var existing = await GetAsync(connection, key, cancellationToken).ConfigureAwait(false);
		if (existing is not null)
		{
			_logger.LogDebug("Loaded incident analysis session {SessionId} at turn {TurnNumber}.", existing.SessionId, existing.TurnNumber);
			return existing;
		}

		var created = new IncidentAnalysisSessionContext
		{
			SessionId = key,
			TurnNumber = 0,
			UpdatedAtUtc = DateTimeOffset.UtcNow
		};
		await SaveAsync(created, cancellationToken).ConfigureAwait(false);
		_logger.LogInformation("Created incident analysis session {SessionId}.", created.SessionId);
		return created;
	}

	public async Task SaveAsync(IncidentAnalysisSessionContext sessionContext, CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(sessionContext);
		cancellationToken.ThrowIfCancellationRequested();
		await EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);

		await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
		await using var command = connection.CreateCommand();
		command.CommandText = """
insert into incident_analysis_sessions (
	session_id,
	turn_number,
	last_incident_summary,
	last_analysis_summary,
	updated_at_utc)
values (
	$sessionId,
	$turnNumber,
	$lastIncidentSummary,
	$lastAnalysisSummary,
	$updatedAtUtc)
on conflict(session_id) do update set
	turn_number = excluded.turn_number,
	last_incident_summary = excluded.last_incident_summary,
	last_analysis_summary = excluded.last_analysis_summary,
	updated_at_utc = excluded.updated_at_utc;
""";
		command.Parameters.AddWithValue("$sessionId", sessionContext.SessionId);
		command.Parameters.AddWithValue("$turnNumber", sessionContext.TurnNumber);
		command.Parameters.AddWithValue("$lastIncidentSummary", (object?)sessionContext.LastIncidentSummary ?? DBNull.Value);
		command.Parameters.AddWithValue("$lastAnalysisSummary", (object?)sessionContext.LastAnalysisSummary ?? DBNull.Value);
		command.Parameters.AddWithValue("$updatedAtUtc", sessionContext.UpdatedAtUtc.ToString("O"));
		await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
		_logger.LogDebug("Saved incident analysis session {SessionId} at turn {TurnNumber}.", sessionContext.SessionId, sessionContext.TurnNumber);
	}

	private static async Task<IncidentAnalysisSessionContext?> GetAsync(
		SqliteConnection connection,
		string sessionId,
		CancellationToken cancellationToken)
	{
		await using var command = connection.CreateCommand();
		command.CommandText = """
select session_id, turn_number, last_incident_summary, last_analysis_summary, updated_at_utc
from incident_analysis_sessions
where session_id = $sessionId;
""";
		command.Parameters.AddWithValue("$sessionId", sessionId);

		await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
		if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
		{
			return null;
		}

		return new IncidentAnalysisSessionContext
		{
			SessionId = reader.GetString(0),
			TurnNumber = reader.GetInt32(1),
			LastIncidentSummary = reader.IsDBNull(2) ? null : reader.GetString(2),
			LastAnalysisSummary = reader.IsDBNull(3) ? null : reader.GetString(3),
			UpdatedAtUtc = DateTimeOffset.Parse(reader.GetString(4))
		};
	}

	private async Task EnsureSchemaAsync(CancellationToken cancellationToken)
	{
		if (_schemaReady)
		{
			return;
		}

		await _schemaLock.WaitAsync(cancellationToken).ConfigureAwait(false);
		try
		{
			if (_schemaReady)
			{
				return;
			}

			await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
			await using var command = connection.CreateCommand();
			command.CommandText = """
create table if not exists incident_analysis_sessions (
	session_id text primary key,
	turn_number integer not null,
	last_incident_summary text null,
	last_analysis_summary text null,
	updated_at_utc text not null
);
""";
			await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
			_schemaReady = true;
		}
		finally
		{
			_schemaLock.Release();
		}
	}

	private async Task<SqliteConnection> OpenConnectionAsync(CancellationToken cancellationToken)
	{
		var databasePath = ResolveDatabasePath();
		Directory.CreateDirectory(Path.GetDirectoryName(databasePath)!);
		var connectionString = new SqliteConnectionStringBuilder
		{
			DataSource = databasePath,
			Pooling = false
		}.ToString();
		var connection = new SqliteConnection(connectionString);
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
		return connection;
	}

	private string ResolveDatabasePath()
	{
		if (!string.IsNullOrWhiteSpace(_options.SessionDatabasePath))
		{
			return Path.GetFullPath(Environment.ExpandEnvironmentVariables(_options.SessionDatabasePath));
		}

		return Path.Combine(
			Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
			"IncidentResponseAgent",
			"incident-sessions.sqlite");
	}
}
