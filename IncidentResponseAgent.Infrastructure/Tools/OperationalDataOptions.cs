namespace IncidentResponseAgent.Infrastructure.Tools;

public sealed class OperationalDataOptions
{
	public IReadOnlyList<OperationalProjectOptions> Projects { get; init; } = Array.Empty<OperationalProjectOptions>();

	public string? ProjectRegistryPath { get; init; }

	public string ProjectId { get; init; } = "default";

	public string ProjectName { get; init; } = "Default project";

	public string? LogEntriesPath { get; init; }

	public string? MetricSamplesPath { get; init; }

	public string? SourceHealthEndpoint { get; init; }

	public int SourceHealthTimeoutSeconds { get; init; } = 3;

	public decimal HighErrorRateThreshold { get; init; } = 25m;

	public decimal CriticalErrorRateThreshold { get; init; } = 40m;

	public decimal QueueDepthWarningThreshold { get; init; } = 700m;

	public decimal LatencyWarningThresholdMs { get; init; } = 1000m;

	public decimal LatencyCriticalThresholdMs { get; init; } = 3000m;

	public decimal HealthCheckFailureThreshold { get; init; } = 3m;

	public decimal HealthCheckCriticalFailureThreshold { get; init; } = 10m;

	public int LogPatternCountThreshold { get; init; } = 2;

	public int DetectionWindowMinutes { get; init; } = 10;

	public int MaxDetectedIncidents { get; init; } = 10;

	public bool UseDeterministicFallbacks { get; init; }
}

public sealed class OperationalProjectOptions
{
	public string Id { get; init; } = "default";

	public string Name { get; init; } = "Default project";

	public string? LogEntriesPath { get; init; }

	public string? MetricSamplesPath { get; init; }

	public string? SourceHealthEndpoint { get; init; }

	public decimal? HighErrorRateThreshold { get; init; }

	public decimal? CriticalErrorRateThreshold { get; init; }

	public decimal? QueueDepthWarningThreshold { get; init; }

	public decimal? LatencyWarningThresholdMs { get; init; }

	public decimal? LatencyCriticalThresholdMs { get; init; }

	public decimal? HealthCheckFailureThreshold { get; init; }

	public decimal? HealthCheckCriticalFailureThreshold { get; init; }

	public int? LogPatternCountThreshold { get; init; }

	public int? DetectionWindowMinutes { get; init; }

	public int? MaxDetectedIncidents { get; init; }
}
