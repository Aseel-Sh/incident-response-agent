namespace IncidentResponseAgent.Application.Tools;

public sealed record LogSearchRequest
{
	public required string Query { get; init; }

	public string? ServiceName { get; init; }

	public string? Environment { get; init; }

	public DateTimeOffset? StartTime { get; init; }

	public DateTimeOffset? EndTime { get; init; }

	public int MaxResults { get; init; } = 10;
}