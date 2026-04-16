namespace IncidentResponseAgent.Application.Incidents;

public sealed record IncidentAnalysisResult
{
	public string SessionId { get; init; } = string.Empty;

	public int SessionTurnNumber { get; init; }

	public string? SessionContextSummary { get; init; }

	public Guid IncidentId { get; init; }

	public string IncidentSummary { get; init; } = string.Empty;

	public string AnalysisText { get; init; } = string.Empty;

	public IReadOnlyList<IncidentAnalysisEvidenceItem> Evidence { get; init; } = Array.Empty<IncidentAnalysisEvidenceItem>();

	public IReadOnlyList<IncidentHypothesis> Hypotheses { get; init; } = Array.Empty<IncidentHypothesis>();

	public IReadOnlyList<IncidentActionRecommendation> RecommendedActions { get; init; } = Array.Empty<IncidentActionRecommendation>();

	public string? Confidence { get; init; }

	public string? Notes { get; init; }
}