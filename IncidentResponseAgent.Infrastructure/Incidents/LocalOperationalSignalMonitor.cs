using System.Security.Cryptography;
using System.Text;
using IncidentResponseAgent.Application.Incidents;
using IncidentResponseAgent.Application.Tools;
using IncidentResponseAgent.Domain.Incidents;
using IncidentResponseAgent.Infrastructure.Tools;
using Microsoft.Extensions.Options;

namespace IncidentResponseAgent.Infrastructure.Incidents;

public sealed class LocalOperationalSignalMonitor : IIncidentSignalMonitor
{
	private readonly ILogSearchProvider _logSearchProvider;
	private readonly IMetricsProvider _metricsProvider;
	private readonly OperationalDataOptions _options;

	public LocalOperationalSignalMonitor(
		ILogSearchProvider logSearchProvider,
		IMetricsProvider metricsProvider,
		IOptions<OperationalDataOptions> options)
	{
		_logSearchProvider = logSearchProvider;
		_metricsProvider = metricsProvider;
		_options = options.Value ?? new OperationalDataOptions();
	}

	public async Task<IReadOnlyList<DetectedIncidentCandidate>> DetectAsync(CancellationToken cancellationToken = default)
	{
		var candidates = new List<DetectedIncidentCandidate>();
		candidates.AddRange(await DetectMetricIncidentsAsync(cancellationToken).ConfigureAwait(false));
		candidates.AddRange(await DetectLogIncidentsAsync(cancellationToken).ConfigureAwait(false));

		return candidates
			.GroupBy(candidate => $"{candidate.ServiceName}|{candidate.Environment}|{candidate.Title}", StringComparer.OrdinalIgnoreCase)
			.Select(group => Merge(group.ToArray()))
			.OrderByDescending(candidate => candidate.Severity)
			.ThenByDescending(candidate => candidate.DetectedAtUtc)
			.Take(Math.Clamp(_options.MaxDetectedIncidents, 1, 25))
			.ToArray();
	}

	private async Task<IReadOnlyList<DetectedIncidentCandidate>> DetectMetricIncidentsAsync(CancellationToken cancellationToken)
	{
		var checks = new[]
		{
			new MetricCheck("request_error_rate", "checkout-api", "production", _options.HighErrorRateThreshold, _options.CriticalErrorRateThreshold, "checkout", "5xx"),
			new MetricCheck("request_error_rate", "auth-api", "production", 8m, _options.HighErrorRateThreshold, "auth", "login"),
			new MetricCheck("queue_depth", "orders-worker", "production", _options.QueueDepthWarningThreshold, 1500m, "queue", "backlog")
		};

		var candidates = new List<DetectedIncidentCandidate>();
		foreach (var check in checks)
		{
			var result = await _metricsProvider.QueryAsync(new MetricsQueryRequest
			{
				MetricName = check.MetricName,
				ServiceName = check.ServiceName,
				Environment = check.Environment
			}, cancellationToken).ConfigureAwait(false);

			var latest = result.Samples.OrderBy(sample => sample.Timestamp).LastOrDefault();
			if (latest is null || latest.Value < check.WarningThreshold)
			{
				continue;
			}

			var severity = latest.Value >= check.CriticalThreshold ? IncidentSeverity.Critical : IncidentSeverity.High;
			var readableMetric = check.MetricName.Replace('_', ' ');
			candidates.Add(new DetectedIncidentCandidate
			{
				Id = StableId($"{check.MetricName}:{check.ServiceName}:{check.Environment}:{latest.Timestamp:O}:{latest.Value}"),
				Title = $"{check.ServiceName} {readableMetric} threshold breached",
				Description = $"{check.ServiceName} in {check.Environment} has {readableMetric} at {latest.Value}, above the configured threshold of {check.WarningThreshold}.",
				Severity = severity,
				ServiceName = check.ServiceName,
				Environment = check.Environment,
				DetectedAtUtc = latest.Timestamp,
				Source = "metrics",
				Signals = new[] { $"{check.MetricName}={latest.Value}", $"threshold={check.WarningThreshold}" },
				SuggestedTags = new[] { check.PrimaryTag, check.SecondaryTag, check.MetricName }
			});
		}

		return candidates;
	}

	private async Task<IReadOnlyList<DetectedIncidentCandidate>> DetectLogIncidentsAsync(CancellationToken cancellationToken)
	{
		var result = await _logSearchProvider.SearchAsync(new LogSearchRequest
		{
			Query = "error warning timeout latency backlog failure 500",
			MaxResults = 20
		}, cancellationToken).ConfigureAwait(false);

		return result.Entries
			.GroupBy(entry => entry.Source, StringComparer.OrdinalIgnoreCase)
			.Select(group =>
			{
				var entries = group.OrderByDescending(entry => entry.Timestamp).ToArray();
				var latest = entries[0];
				var hasError = entries.Any(entry => entry.Level.Equals("Error", StringComparison.OrdinalIgnoreCase));
				var severity = hasError ? IncidentSeverity.High : IncidentSeverity.Medium;
				var environment = InferEnvironment(entries);
				var signals = entries
					.Take(3)
					.Select(entry => $"{entry.Level}: {entry.Message}")
					.ToArray();

				return new DetectedIncidentCandidate
				{
					Id = StableId($"logs:{group.Key}:{latest.Timestamp:O}:{string.Join('|', signals)}"),
					Title = $"{group.Key} suspicious log pattern",
					Description = $"{group.Key} emitted {entries.Length} matching operational log signal(s). Latest: {latest.Message}",
					Severity = severity,
					ServiceName = group.Key,
					Environment = environment,
					DetectedAtUtc = latest.Timestamp,
					Source = "logs",
					Signals = signals,
					SuggestedTags = TagsFromLogs(entries)
				};
			})
			.ToArray();
	}

	private static DetectedIncidentCandidate Merge(IReadOnlyList<DetectedIncidentCandidate> candidates)
	{
		var primary = candidates
			.OrderByDescending(candidate => candidate.Severity)
			.ThenByDescending(candidate => candidate.DetectedAtUtc)
			.First();

		return primary with
		{
			Id = StableId(string.Join('|', candidates.Select(candidate => candidate.Id).Order(StringComparer.Ordinal))),
			Source = string.Join(", ", candidates.Select(candidate => candidate.Source).Distinct(StringComparer.OrdinalIgnoreCase)),
			Signals = candidates.SelectMany(candidate => candidate.Signals).Distinct(StringComparer.OrdinalIgnoreCase).Take(8).ToArray(),
			SuggestedTags = candidates.SelectMany(candidate => candidate.SuggestedTags).Distinct(StringComparer.OrdinalIgnoreCase).Take(8).ToArray()
		};
	}

	private static string InferEnvironment(IEnumerable<LogSearchEntry> entries)
	{
		return entries.Any(entry => entry.Message.Contains("production", StringComparison.OrdinalIgnoreCase))
			? "production"
			: "unknown";
	}

	private static IReadOnlyList<string> TagsFromLogs(IEnumerable<LogSearchEntry> entries)
	{
		var text = string.Join(' ', entries.Select(entry => entry.Message));
		var tags = new List<string>();
		AddIfContains(tags, text, "checkout");
		AddIfContains(tags, text, "latency");
		AddIfContains(tags, text, "timeout");
		AddIfContains(tags, text, "backlog");
		AddIfContains(tags, text, "login");
		AddIfContains(tags, text, "500", "5xx");
		return tags.Count == 0 ? new[] { "logs" } : tags;
	}

	private static void AddIfContains(ICollection<string> tags, string text, string token, string? tag = null)
	{
		if (text.Contains(token, StringComparison.OrdinalIgnoreCase))
		{
			tags.Add(tag ?? token);
		}
	}

	private static string StableId(string value)
	{
		var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
		return Convert.ToHexString(bytes)[..16].ToLowerInvariant();
	}

	private sealed record MetricCheck(
		string MetricName,
		string ServiceName,
		string Environment,
		decimal WarningThreshold,
		decimal CriticalThreshold,
		string PrimaryTag,
		string SecondaryTag);
}
