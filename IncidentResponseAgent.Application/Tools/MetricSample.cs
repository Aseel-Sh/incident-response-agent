namespace IncidentResponseAgent.Application.Tools;

public sealed record MetricSample
{
	public required DateTimeOffset Timestamp { get; init; }

	public required decimal Value { get; init; }
}