namespace IncidentResponseAgent.Application.Incidents;

public sealed record ProposedKnowledgeUpdate
{
	public Guid Id { get; init; } = Guid.NewGuid();

	public required string Title { get; init; }

	public required string Content { get; init; }

	public string Status { get; init; } = "pending";

	public required DateTimeOffset GeneratedAtUtc { get; init; }

	public DateTimeOffset? ReviewedAtUtc { get; init; }

	public string? ReviewNotes { get; init; }
}
