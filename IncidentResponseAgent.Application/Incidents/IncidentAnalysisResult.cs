namespace IncidentResponseAgent.Application.Incidents;

public sealed record IncidentAnalysisResult
{
	public Guid IncidentId { get; init; }

	public string IncidentSummary { get; init; } = string.Empty;

	public string AnalysisText { get; init; } = string.Empty;

	public IReadOnlyList<IncidentAnalysisEvidenceItem> Evidence { get; init; } = Array.Empty<IncidentAnalysisEvidenceItem>();

	public IReadOnlyList<IncidentHypothesis> Hypotheses { get; init; } = Array.Empty<IncidentHypothesis>();

	public IReadOnlyList<IncidentActionRecommendation> RecommendedActions { get; init; } = Array.Empty<IncidentActionRecommendation>();

	public string? Confidence { get; init; }

	public string? Notes { get; init; }
}