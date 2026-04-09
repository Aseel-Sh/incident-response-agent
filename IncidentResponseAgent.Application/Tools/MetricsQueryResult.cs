namespace IncidentResponseAgent.Application.Tools;

public sealed record MetricsQueryResult
{
	public required string MetricName { get; init; }

	public IReadOnlyList<MetricSample> Samples { get; init; } = Array.Empty<MetricSample>();
}