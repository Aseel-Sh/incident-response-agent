using IncidentResponseAgent.Application.Incidents;
using IncidentResponseAgent.Infrastructure.Incidents;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace IncidentResponseAgent.Tests;

public sealed class SqliteIncidentAnalysisSessionStoreTests : IDisposable
{
	private readonly string _rootPath = Path.Combine(Path.GetTempPath(), "ira-session-tests", Guid.NewGuid().ToString("N"));

	[Fact]
	public async Task GetOrCreateAsyncPersistsSavedSessionAcrossStoreInstances()
	{
		Directory.CreateDirectory(_rootPath);
		var databasePath = Path.Combine(_rootPath, "sessions.sqlite");
		var options = Options.Create(new IncidentStorageOptions { SessionDatabasePath = databasePath });
		var firstStore = new SqliteIncidentAnalysisSessionStore(options, NullLogger<SqliteIncidentAnalysisSessionStore>.Instance);
		var created = await firstStore.GetOrCreateAsync("session-1");
		await firstStore.SaveAsync(created with
		{
			TurnNumber = 2,
			LastIncidentSummary = "checkout incident",
			LastAnalysisSummary = "review runbook",
			UpdatedAtUtc = DateTimeOffset.UtcNow
		});

		var secondStore = new SqliteIncidentAnalysisSessionStore(options, NullLogger<SqliteIncidentAnalysisSessionStore>.Instance);
		var loaded = await secondStore.GetOrCreateAsync("session-1");

		Assert.Equal(2, loaded.TurnNumber);
		Assert.Equal("checkout incident", loaded.LastIncidentSummary);
		Assert.True(File.Exists(databasePath));
	}

	public void Dispose()
	{
		if (Directory.Exists(_rootPath))
		{
			Directory.Delete(_rootPath, recursive: true);
		}
	}
}
