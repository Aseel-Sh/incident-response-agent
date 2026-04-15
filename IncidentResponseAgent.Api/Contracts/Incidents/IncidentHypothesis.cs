namespace IncidentResponseAgent.Api.Contracts.Incidents;

public sealed record IncidentHypothesis
{
	public required string Description { get; init; }

	public string? Confidence { get; init; }

	public IReadOnlyList<string> SupportingEvidence { get; init; } = Array.Empty<string>();
}