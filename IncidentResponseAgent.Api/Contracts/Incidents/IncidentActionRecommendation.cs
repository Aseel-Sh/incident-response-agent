namespace IncidentResponseAgent.Api.Contracts.Incidents;

public sealed record IncidentActionRecommendation
{
	public required string Description { get; init; }

	public required string Priority { get; init; }

	public string? Rationale { get; init; }
}