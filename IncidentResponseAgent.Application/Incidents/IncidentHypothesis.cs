namespace IncidentResponseAgent.Application.Incidents;

public sealed record IncidentHypothesis
{
	public required string Description { get; init; }

	public required string InferenceStrength { get; init; }

	public string? Confidence { get; init; }

	public IReadOnlyList<string> SupportingEvidence { get; init; } = Array.Empty<string>();

	public IReadOnlyList<string> EvidenceReferences { get; init; } = Array.Empty<string>();
}