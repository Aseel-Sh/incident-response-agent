namespace IncidentResponseAgent.Application.Incidents;

public sealed record AgentStructuredAnalysis
{
	public string? Summary { get; init; }

	public IReadOnlyList<IncidentAnalysisEvidenceItem> Evidence { get; init; } = Array.Empty<IncidentAnalysisEvidenceItem>();

	public IReadOnlyList<IncidentHypothesis> Hypotheses { get; init; } = Array.Empty<IncidentHypothesis>();

	public IReadOnlyList<IncidentActionRecommendation> RecommendedActions { get; init; } = Array.Empty<IncidentActionRecommendation>();

	public string? Confidence { get; init; }

	public string? Notes { get; init; }
}
