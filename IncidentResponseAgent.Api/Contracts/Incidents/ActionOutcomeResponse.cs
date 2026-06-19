namespace IncidentResponseAgent.Api.Contracts.Incidents;

public sealed record ActionOutcomeResponse
{
	public Guid Id { get; init; }

	public required string Description { get; init; }

	public required string Status { get; init; }

	public required DateTimeOffset LoggedAtUtc { get; init; }

	public string? EvidenceReference { get; init; }
}
