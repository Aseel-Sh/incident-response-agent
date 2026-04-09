namespace IncidentResponseAgent.Application.Tools;

public sealed record LogSearchEntry
{
	public required DateTimeOffset Timestamp { get; init; }

	public required string Source { get; init; }

	public required string Level { get; init; }

	public required string Message { get; init; }

	public string? CorrelationId { get; init; }
}