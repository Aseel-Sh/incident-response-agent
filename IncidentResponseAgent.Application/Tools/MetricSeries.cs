namespace IncidentResponseAgent.Application.Tools;

public sealed record MetricSeries
{
	public required string MetricName { get; init; }

	public required string ServiceName { get; init; }

	public required string Environment { get; init; }

	public IReadOnlyList<MetricSample> Samples { get; init; } = Array.Empty<MetricSample>();
}
