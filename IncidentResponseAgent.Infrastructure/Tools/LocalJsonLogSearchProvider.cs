using System.Text.Json;
using IncidentResponseAgent.Application.Tools;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace IncidentResponseAgent.Infrastructure.Tools;

public sealed class LocalJsonLogSearchProvider : ILogSearchProvider
{
	private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
	{
		PropertyNameCaseInsensitive = true
	};

	private readonly ILogger<LocalJsonLogSearchProvider> _logger;
	private readonly OperationalDataOptions _options;

	public LocalJsonLogSearchProvider(
		IOptions<OperationalDataOptions> options,
		ILogger<LocalJsonLogSearchProvider> logger)
	{
		_options = options.Value ?? new OperationalDataOptions();
		_logger = logger;
	}

	public async Task<LogSearchResult> SearchAsync(LogSearchRequest request, CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(request);
		cancellationToken.ThrowIfCancellationRequested();

		if (string.IsNullOrWhiteSpace(request.Query))
		{
			throw new ArgumentException("Log search query cannot be empty.", nameof(request));
		}

		var entries = await LoadEntriesAsync(cancellationToken).ConfigureAwait(false);
		var queryTokens = Tokenize(request.Query);
		var serviceName = request.ServiceName;
		var environment = request.Environment;
		var maxResults = request.MaxResults <= 0 ? 1 : Math.Min(request.MaxResults, 20);

		var matches = entries
			.Where(entry => IsInWindow(entry, request.StartTime, request.EndTime))
			.Where(entry => string.IsNullOrWhiteSpace(serviceName) || entry.Source.Contains(serviceName, StringComparison.OrdinalIgnoreCase))
			.Where(entry => string.IsNullOrWhiteSpace(environment) || entry.Message.Contains(environment, StringComparison.OrdinalIgnoreCase))
			.Select(entry => new { Entry = entry, Score = Score(entry, queryTokens) })
			.Where(match => match.Score > 0)
			.OrderByDescending(match => match.Score)
			.ThenByDescending(match => match.Entry.Timestamp)
			.Take(maxResults)
			.Select(match => match.Entry)
			.ToArray();

		if (matches.Length == 0)
		{
			_logger.LogInformation("No local log entries matched query {Query}. Returning deterministic fallback entries.", request.Query);
			return await new DeterministicFallbackLogSearchProvider().SearchAsync(request, cancellationToken).ConfigureAwait(false);
		}

		_logger.LogInformation("Log search query {Query} returned {Count} local entries.", request.Query, matches.Length);
		return new LogSearchResult { Entries = matches };
	}

	private async Task<IReadOnlyList<LogSearchEntry>> LoadEntriesAsync(CancellationToken cancellationToken)
	{
		var path = ResolvePath();
		if (!File.Exists(path))
		{
			_logger.LogWarning("Local log sample file {Path} was not found.", path);
			return Array.Empty<LogSearchEntry>();
		}

		await using var stream = File.OpenRead(path);
		return await JsonSerializer.DeserializeAsync<LogSearchEntry[]>(stream, SerializerOptions, cancellationToken).ConfigureAwait(false)
			?? Array.Empty<LogSearchEntry>();
	}

	private string ResolvePath()
	{
		if (!string.IsNullOrWhiteSpace(_options.LogEntriesPath))
		{
			return Path.GetFullPath(Environment.ExpandEnvironmentVariables(_options.LogEntriesPath));
		}

		return Path.Combine(AppContext.BaseDirectory, "Tools", "SampleData", "logs.json");
	}

	private static bool IsInWindow(LogSearchEntry entry, DateTimeOffset? startTime, DateTimeOffset? endTime)
	{
		if (startTime is not null && entry.Timestamp < startTime.Value)
		{
			return false;
		}

		if (endTime is not null && entry.Timestamp > endTime.Value)
		{
			return false;
		}

		return true;
	}

	private static int Score(LogSearchEntry entry, HashSet<string> queryTokens)
	{
		var haystack = Tokenize($"{entry.Source} {entry.Level} {entry.Message} {entry.CorrelationId}");
		var score = 0;
		foreach (var token in queryTokens)
		{
			if (haystack.Contains(token))
			{
				score++;
			}
		}

		return score;
	}

	private static HashSet<string> Tokenize(string? value)
	{
		if (string.IsNullOrWhiteSpace(value))
		{
			return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		}

		return System.Text.RegularExpressions.Regex.Matches(value.ToLowerInvariant(), "[a-z0-9]+")
			.Select(match => match.Value)
			.ToHashSet(StringComparer.OrdinalIgnoreCase);
	}
}
