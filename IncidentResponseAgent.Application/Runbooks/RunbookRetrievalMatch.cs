namespace IncidentResponseAgent.Application.Runbooks;

public sealed record RunbookRetrievalMatch
{
	public required string RunbookId { get; init; }

	public required string Title { get; init; }

	public required string SectionPath { get; init; }

	public required string Summary { get; init; }

	public required string Source { get; init; }

	public required double Score { get; init; }

	public IReadOnlyCollection<string> Tags { get; init; } = Array.Empty<string>();
}
