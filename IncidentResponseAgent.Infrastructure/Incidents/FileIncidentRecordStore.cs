using System.Text.Json;
using System.Text.RegularExpressions;
using IncidentResponseAgent.Application.Incidents;
using IncidentResponseAgent.Domain.Incidents;
using Microsoft.Extensions.Options;

namespace IncidentResponseAgent.Infrastructure.Incidents;

public sealed class FileIncidentRecordStore : IIncidentRecordStore
{
	private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
	{
		WriteIndented = true
	};

	private readonly SemaphoreSlim _fileLock = new(1, 1);
	private readonly string _filePath;

	public FileIncidentRecordStore(IOptions<IncidentStorageOptions> options)
	{
		var configuredPath = options.Value?.IncidentRecordsPath;
		if (!string.IsNullOrWhiteSpace(configuredPath))
		{
			_filePath = Path.GetFullPath(Environment.ExpandEnvironmentVariables(configuredPath));
			var configuredDirectory = Path.GetDirectoryName(_filePath);
			if (!string.IsNullOrWhiteSpace(configuredDirectory))
			{
				Directory.CreateDirectory(configuredDirectory);
			}

			return;
		}

		var rootFolder = Path.Combine(
			Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
			"IncidentResponseAgent");

		Directory.CreateDirectory(rootFolder);
		_filePath = Path.Combine(rootFolder, "incident-records.json");
	}

	public async Task SaveAsync(Incident incident, IncidentAnalysisResult analysisResult, CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(incident);
		ArgumentNullException.ThrowIfNull(analysisResult);
		cancellationToken.ThrowIfCancellationRequested();

		await _fileLock.WaitAsync(cancellationToken);
		try
		{
			var records = await ReadRecordsAsync(cancellationToken);
			records[incident.Id] = new IncidentAnalysisRecord
			{
				Incident = incident,
				AnalysisResult = analysisResult,
				CreatedAtUtc = DateTimeOffset.UtcNow
			};

			await WriteRecordsAsync(records.Values, cancellationToken);
		}
		finally
		{
			_fileLock.Release();
		}
	}

	public async Task<IncidentAnalysisRecord?> GetByIncidentIdAsync(Guid incidentId, CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();

		await _fileLock.WaitAsync(cancellationToken);
		try
		{
			var records = await ReadRecordsAsync(cancellationToken);
			return records.TryGetValue(incidentId, out var record) ? record : null;
		}
		finally
		{
			_fileLock.Release();
		}
	}

	public async Task<IReadOnlyList<IncidentAnalysisRecord>> GetRecentAsync(int maxResults, CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();

		await _fileLock.WaitAsync(cancellationToken);
		try
		{
			var count = maxResults <= 0 ? 1 : maxResults;
			var records = (await ReadRecordsAsync(cancellationToken)).Values
				.OrderByDescending(record => record.CreatedAtUtc)
				.Take(count)
				.ToArray();

			return records;
		}
		finally
		{
			_fileLock.Release();
		}
	}

	public async Task<IncidentActionOutcome> AddActionOutcomeAsync(
		Guid incidentId,
		string description,
		string status,
		CancellationToken cancellationToken = default)
	{
		if (string.IsNullOrWhiteSpace(description))
		{
			throw new ArgumentException("Action outcome description is required.", nameof(description));
		}

		var normalizedStatus = NormalizeOutcomeStatus(status);
		var outcome = new IncidentActionOutcome
		{
			Description = description.Trim(),
			Status = normalizedStatus,
			LoggedAtUtc = DateTimeOffset.UtcNow
		};

		await _fileLock.WaitAsync(cancellationToken);
		try
		{
			var records = await ReadRecordsAsync(cancellationToken);
			if (!records.TryGetValue(incidentId, out var record))
			{
				throw new KeyNotFoundException($"Incident record {incidentId} was not found.");
			}

			var outcomes = record.AnalysisResult.ActionOutcomes
				.Append(outcome)
				.OrderByDescending(item => item.LoggedAtUtc)
				.Take(20)
				.OrderBy(item => item.LoggedAtUtc)
				.ToArray();

			records[incidentId] = record with
			{
				AnalysisResult = record.AnalysisResult with { ActionOutcomes = outcomes }
			};

			await WriteRecordsAsync(records.Values, cancellationToken);
			return outcome;
		}
		finally
		{
			_fileLock.Release();
		}
	}

	public async Task<IReadOnlyList<SimilarIncidentMatch>> FindSimilarAsync(
		Incident incident,
		int maxResults,
		CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(incident);
		cancellationToken.ThrowIfCancellationRequested();

		await _fileLock.WaitAsync(cancellationToken);
		try
		{
			var count = Math.Clamp(maxResults <= 0 ? 3 : maxResults, 1, 10);
			var queryTokens = Tokenize(BuildIncidentText(incident));
			var records = (await ReadRecordsAsync(cancellationToken)).Values
				.Where(record => record.Incident.Id != incident.Id)
				.Select(record => ToSimilarMatch(incident, queryTokens, record))
				.Where(match => match.Score >= 0.18)
				.OrderByDescending(match => match.Score)
				.ThenByDescending(match => match.CreatedAtUtc)
				.Take(count)
				.ToArray();

			return records;
		}
		finally
		{
			_fileLock.Release();
		}
	}

	private async Task<Dictionary<Guid, IncidentAnalysisRecord>> ReadRecordsAsync(CancellationToken cancellationToken)
	{
		if (!File.Exists(_filePath))
		{
			return new Dictionary<Guid, IncidentAnalysisRecord>();
		}

		await using var stream = File.Open(_filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
		var records = await JsonSerializer.DeserializeAsync<List<IncidentAnalysisRecord>>(stream, SerializerOptions, cancellationToken)
			?? [];

		return records.ToDictionary(record => record.Incident.Id, record => record);
	}

	private async Task WriteRecordsAsync(IEnumerable<IncidentAnalysisRecord> records, CancellationToken cancellationToken)
	{
		await using var stream = File.Open(_filePath, FileMode.Create, FileAccess.Write, FileShare.None);
		await JsonSerializer.SerializeAsync(stream, records, SerializerOptions, cancellationToken);
	}

	private static SimilarIncidentMatch ToSimilarMatch(
		Incident incident,
		HashSet<string> queryTokens,
		IncidentAnalysisRecord record)
	{
		var candidateTokens = Tokenize(BuildIncidentText(record.Incident, record.AnalysisResult));
		var shared = queryTokens.Intersect(candidateTokens, StringComparer.OrdinalIgnoreCase)
			.Order(StringComparer.OrdinalIgnoreCase)
			.Take(12)
			.ToArray();

		var score = queryTokens.Count == 0
			? 0d
			: shared.Length / (double)queryTokens.Count;

		if (!string.IsNullOrWhiteSpace(incident.ServiceName) &&
		    string.Equals(incident.ServiceName, record.Incident.ServiceName, StringComparison.OrdinalIgnoreCase))
		{
			score += 0.35;
		}

		if (!string.IsNullOrWhiteSpace(incident.Environment) &&
		    string.Equals(incident.Environment, record.Incident.Environment, StringComparison.OrdinalIgnoreCase))
		{
			score += 0.1;
		}

		if (incident.Severity == record.Incident.Severity)
		{
			score += 0.05;
		}

		return new SimilarIncidentMatch
		{
			IncidentId = record.Incident.Id,
			IncidentSummary = record.AnalysisResult.IncidentSummary,
			ServiceName = record.Incident.ServiceName ?? "unknown service",
			Environment = record.Incident.Environment ?? "unknown environment",
			ResolutionSummary = BuildResolutionSummary(record.AnalysisResult),
			Score = Math.Round(Math.Min(score, 1), 4),
			CreatedAtUtc = record.CreatedAtUtc,
			SharedSignals = shared
		};
	}

	private static string BuildIncidentText(Incident incident, IncidentAnalysisResult? analysis = null)
	{
		var parts = new[]
		{
			incident.Title,
			incident.Description,
			incident.ServiceName,
			incident.Environment,
			string.Join(' ', incident.Tags),
			analysis?.IncidentSummary,
			analysis?.Notes,
			string.Join(' ', analysis?.RecommendedActions.Select(action => action.Description) ?? Array.Empty<string>()),
			string.Join(' ', analysis?.ActionOutcomes.Select(outcome => $"{outcome.Status} {outcome.Description}") ?? Array.Empty<string>())
		};

		return string.Join(' ', parts.Where(part => !string.IsNullOrWhiteSpace(part)));
	}

	private static string BuildResolutionSummary(IncidentAnalysisResult analysis)
	{
		var firstAction = analysis.RecommendedActions.FirstOrDefault()?.Description;
		var workedOutcome = analysis.ActionOutcomes
			.LastOrDefault(outcome => string.Equals(outcome.Status, "worked", StringComparison.OrdinalIgnoreCase));
		if (workedOutcome is not null)
		{
			return $"Worked: {workedOutcome.Description}";
		}

		if (!string.IsNullOrWhiteSpace(firstAction))
		{
			return firstAction;
		}

		return string.IsNullOrWhiteSpace(analysis.Notes) ? analysis.IncidentSummary : analysis.Notes;
	}

	private static HashSet<string> Tokenize(string value)
	{
		return Regex.Matches(value.ToLowerInvariant(), "[a-z0-9]+")
			.Select(match => match.Value)
			.Where(token => token.Length > 2)
			.ToHashSet(StringComparer.OrdinalIgnoreCase);
	}

	private static string NormalizeOutcomeStatus(string status)
	{
		var normalized = string.IsNullOrWhiteSpace(status) ? "worked" : status.Trim().ToLowerInvariant();
		return normalized is "worked" or "partial" or "failed" ? normalized : "worked";
	}
}
