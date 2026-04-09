namespace IncidentResponseAgent.Application.Tools;

public sealed record MetricsQueryRequest
{
	public required string MetricName { get; init; }

	public string? ServiceName { get; init; }

	public string? Environment { get; init; }

	public DateTimeOffset? StartTime { get; init; }

	public DateTimeOffset? EndTime { get; init; }
}