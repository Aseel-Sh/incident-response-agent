namespace IncidentResponseAgent.Infrastructure.Tools;

public sealed class OperationalDataOptions
{
	public string? LogEntriesPath { get; init; }

	public string? MetricSamplesPath { get; init; }

	public decimal HighErrorRateThreshold { get; init; } = 25m;

	public decimal CriticalErrorRateThreshold { get; init; } = 40m;

	public decimal QueueDepthWarningThreshold { get; init; } = 700m;

	public decimal LatencyWarningThresholdMs { get; init; } = 1000m;

	public decimal LatencyCriticalThresholdMs { get; init; } = 3000m;

	public decimal HealthCheckFailureThreshold { get; init; } = 3m;

	public decimal HealthCheckCriticalFailureThreshold { get; init; } = 10m;

	public int LogPatternCountThreshold { get; init; } = 2;

	public int MaxDetectedIncidents { get; init; } = 10;

	public bool UseDeterministicFallbacks { get; init; }
}
