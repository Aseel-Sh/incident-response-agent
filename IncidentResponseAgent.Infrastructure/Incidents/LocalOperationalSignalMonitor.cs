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
	private readonly IMetricSeriesCatalog _metricSeriesCatalog;
	private readonly OperationalDataOptions _options;

	public LocalOperationalSignalMonitor(
		ILogSearchProvider logSearchProvider,
		IMetricSeriesCatalog metricSeriesCatalog,
		IOptions<OperationalDataOptions> options)
	{
		_logSearchProvider = logSearchProvider;
		_metricSeriesCatalog = metricSeriesCatalog;
		_options = options.Value ?? new OperationalDataOptions();
	}

	public async Task<IReadOnlyList<DetectedIncidentCandidate>> DetectAsync(CancellationToken cancellationToken = default)
	{
		var candidates = new List<DetectedIncidentCandidate>();
		candidates.AddRange(await DetectMetricIncidentsAsync(cancellationToken).ConfigureAwait(false));
		candidates.AddRange(await DetectLogIncidentsAsync(cancellationToken).ConfigureAwait(false));

		return candidates
			.GroupBy(candidate => BuildMergeKey(candidate), StringComparer.OrdinalIgnoreCase)
			.Select(group => Merge(group.ToArray()))
			.OrderBy(candidate => candidate.Severity)
			.ThenByDescending(candidate => candidate.DetectedAtUtc)
			.Take(Math.Clamp(_options.MaxDetectedIncidents, 1, 25))
			.ToArray();
	}

	private async Task<IReadOnlyList<DetectedIncidentCandidate>> DetectMetricIncidentsAsync(CancellationToken cancellationToken)
	{
		var candidates = new List<DetectedIncidentCandidate>();
		var series = await _metricSeriesCatalog.ListSeriesAsync(cancellationToken).ConfigureAwait(false);
		foreach (var item in series)
		{
			var check = BuildMetricCheck(item);
			if (check is null)
			{
				continue;
			}

			var latest = item.Samples.OrderBy(sample => sample.Timestamp).LastOrDefault();
			if (latest is null || latest.Value < check.WarningThreshold)
			{
				continue;
			}

			var severity = latest.Value >= check.CriticalThreshold ? IncidentSeverity.Sev1 : IncidentSeverity.Sev2;
			var readableMetric = item.MetricName.Replace('_', ' ');
			candidates.Add(new DetectedIncidentCandidate
			{
				Id = StableId($"{item.MetricName}:{item.ServiceName}:{item.Environment}:{latest.Timestamp:O}:{latest.Value}"),
				Title = $"{item.ServiceName} {readableMetric} threshold breached",
				Description = $"{item.ServiceName} in {item.Environment} has {readableMetric} at {latest.Value}, above the configured threshold of {check.WarningThreshold}.",
				Severity = severity,
				ServiceName = item.ServiceName,
				Environment = item.Environment,
				DetectedAtUtc = latest.Timestamp,
				Source = "metrics",
				Signals = new[] { $"{item.MetricName}={latest.Value}", $"threshold={check.WarningThreshold}" },
				SuggestedTags = BuildMetricTags(item.MetricName, item.ServiceName)
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
			.Where(group => group.Count() >= _options.LogPatternCountThreshold)
			.Select(group =>
			{
				var entries = group.OrderByDescending(entry => entry.Timestamp).ToArray();
				var latest = entries[0];
				var hasError = entries.Any(entry => entry.Level.Equals("Error", StringComparison.OrdinalIgnoreCase));
				var severity = hasError ? IncidentSeverity.Sev2 : IncidentSeverity.Sev3;
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

	private static string BuildMergeKey(DetectedIncidentCandidate candidate)
	{
		var service = string.IsNullOrWhiteSpace(candidate.ServiceName) ? candidate.Title : candidate.ServiceName;
		var environment = string.IsNullOrWhiteSpace(candidate.Environment) ? "unknown" : candidate.Environment;
		return $"{service}|{environment}";
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

	private MetricCheck? BuildMetricCheck(MetricSeries series)
	{
		if (series.MetricName.Equals("request_error_rate", StringComparison.OrdinalIgnoreCase))
		{
			var warningThreshold = series.ServiceName.Contains("auth", StringComparison.OrdinalIgnoreCase)
				? Math.Min(8m, _options.HighErrorRateThreshold)
				: _options.HighErrorRateThreshold;

			return new MetricCheck(warningThreshold, _options.CriticalErrorRateThreshold);
		}

		if (series.MetricName.Equals("queue_depth", StringComparison.OrdinalIgnoreCase))
		{
			return new MetricCheck(_options.QueueDepthWarningThreshold, Math.Max(1500m, _options.QueueDepthWarningThreshold * 2));
		}

		if (series.MetricName.Contains("latency", StringComparison.OrdinalIgnoreCase))
		{
			return new MetricCheck(_options.LatencyWarningThresholdMs, _options.LatencyCriticalThresholdMs);
		}

		if (series.MetricName.Contains("health", StringComparison.OrdinalIgnoreCase) || series.MetricName.Contains("failure", StringComparison.OrdinalIgnoreCase))
		{
			return new MetricCheck(_options.HealthCheckFailureThreshold, _options.HealthCheckCriticalFailureThreshold);
		}

		return null;
	}

	private static IReadOnlyList<string> BuildMetricTags(string metricName, string serviceName)
	{
		var tags = new List<string> { metricName };
		AddIfContains(tags, serviceName, "checkout");
		AddIfContains(tags, serviceName, "auth");
		AddIfContains(tags, metricName, "queue");
		AddIfContains(tags, metricName, "backlog");
		AddIfContains(tags, metricName, "error", "5xx");
		return tags.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
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
		decimal WarningThreshold,
		decimal CriticalThreshold);
}
