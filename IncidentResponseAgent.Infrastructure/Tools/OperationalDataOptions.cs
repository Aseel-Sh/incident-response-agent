namespace IncidentResponseAgent.Infrastructure.Tools;

public sealed class OperationalDataOptions
{
	public string? LogEntriesPath { get; init; }

	public string? MetricSamplesPath { get; init; }

	public decimal HighErrorRateThreshold { get; init; } = 25m;

	public decimal CriticalErrorRateThreshold { get; init; } = 40m;

	public decimal QueueDepthWarningThreshold { get; init; } = 700m;

	public int MaxDetectedIncidents { get; init; } = 10;

	public bool UseDeterministicFallbacks { get; init; }
}
