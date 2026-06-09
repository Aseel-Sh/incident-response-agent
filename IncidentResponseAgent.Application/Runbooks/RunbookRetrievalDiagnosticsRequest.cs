namespace IncidentResponseAgent.Application.Runbooks;

public sealed record RunbookRetrievalDiagnosticsRequest
{
	public required string Query { get; init; }

	public string? ServiceName { get; init; }

	public string? Environment { get; init; }

	public int MaxResults { get; init; } = 3;
}
