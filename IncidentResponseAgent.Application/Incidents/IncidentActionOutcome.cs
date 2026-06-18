namespace IncidentResponseAgent.Application.Incidents;

public sealed record IncidentActionOutcome
{
	public Guid Id { get; init; } = Guid.NewGuid();

	public required string Description { get; init; }

	public required string Status { get; init; }

	public required DateTimeOffset LoggedAtUtc { get; init; }

	public string? EvidenceReference { get; init; }
}
