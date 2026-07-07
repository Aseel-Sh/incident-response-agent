namespace IncidentResponseAgent.Application.Incidents;

public sealed record IncidentAnalysisResult
{
	public string SessionId { get; init; } = string.Empty;

	public int SessionTurnNumber { get; init; }

	public string? SessionContextSummary { get; init; }

	public Guid IncidentId { get; init; }

	public string ProjectId { get; init; } = "default";

	public string IncidentSummary { get; init; } = string.Empty;

	public string Severity { get; init; } = string.Empty;

	public string AnalysisText { get; init; } = string.Empty;

	public string AnalysisProvider { get; init; } = string.Empty;

	public string? AnalysisModel { get; init; }

	public bool UsedFallbackAnalysis { get; init; }

	public string? FallbackReason { get; init; }

	public IReadOnlyList<IncidentAnalysisEvidenceItem> Evidence { get; init; } = Array.Empty<IncidentAnalysisEvidenceItem>();

	public IReadOnlyList<GroundedIncidentClaim> KnownFacts { get; init; } = Array.Empty<GroundedIncidentClaim>();

	public IReadOnlyList<string> Unknowns { get; init; } = Array.Empty<string>();

	public IReadOnlyList<IncidentRunbookMatch> RunbookMatches { get; init; } = Array.Empty<IncidentRunbookMatch>();

	public IReadOnlyList<IncidentHypothesis> Hypotheses { get; init; } = Array.Empty<IncidentHypothesis>();

	public IReadOnlyList<IncidentActionRecommendation> RecommendedActions { get; init; } = Array.Empty<IncidentActionRecommendation>();

	public IReadOnlyList<IncidentActionOutcome> ActionOutcomes { get; init; } = Array.Empty<IncidentActionOutcome>();

	public IReadOnlyList<SimilarIncidentMatch> SimilarIncidents { get; init; } = Array.Empty<SimilarIncidentMatch>();

	public AnalysisQualityScore Quality { get; init; } = new();

	public AnalysisProviderTransparency ProviderTransparency { get; init; } = new();

	public string? Confidence { get; init; }

	public string? Notes { get; init; }
}
