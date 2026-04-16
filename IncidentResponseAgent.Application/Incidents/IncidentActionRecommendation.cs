namespace IncidentResponseAgent.Application.Incidents;

public sealed record IncidentActionRecommendation
{
	public required string Description { get; init; }

	public required string Priority { get; init; }

	public string? Rationale { get; init; }

	public IReadOnlyList<string> SupportingSignals { get; init; } = Array.Empty<string>();
}