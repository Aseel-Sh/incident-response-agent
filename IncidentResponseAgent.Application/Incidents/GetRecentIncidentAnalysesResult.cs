namespace IncidentResponseAgent.Application.Incidents;

public sealed record GetRecentIncidentAnalysesResult
{
	public required Guid IncidentId { get; init; }

	public required string IncidentTitle { get; init; }

	public required string IncidentSummary { get; init; }

	public required string IncidentDescription { get; init; }

	public string? ServiceName { get; init; }

	public string? Environment { get; init; }

	public required string Severity { get; init; }

	public IReadOnlyList<string> Tags { get; init; } = Array.Empty<string>();

	public required string AnalysisText { get; init; }

	public string AnalysisProvider { get; init; } = string.Empty;

	public string? AnalysisModel { get; init; }

	public bool UsedFallbackAnalysis { get; init; }

	public string? FallbackReason { get; init; }

	public required string SessionId { get; init; }

	public required int SessionTurnNumber { get; init; }

	public string? Confidence { get; init; }

	public string? Notes { get; init; }

	public IReadOnlyList<IncidentActionOutcome> ActionOutcomes { get; init; } = Array.Empty<IncidentActionOutcome>();

	public string Status { get; init; } = "active";

	public required DateTimeOffset CreatedAtUtc { get; init; }

	public IReadOnlyList<IncidentTimelineEvent> Timeline { get; init; } = Array.Empty<IncidentTimelineEvent>();

	public ProposedKnowledgeUpdate? ProposedKnowledgeUpdate { get; init; }

	public IReadOnlyList<IncidentAnalysisFeedback> Feedback { get; init; } = Array.Empty<IncidentAnalysisFeedback>();

	public IReadOnlyList<GroundedIncidentClaim> KnownFacts { get; init; } = Array.Empty<GroundedIncidentClaim>();
	public IReadOnlyList<string> Unknowns { get; init; } = Array.Empty<string>();
	public IReadOnlyList<IncidentRunbookMatch> RunbookMatches { get; init; } = Array.Empty<IncidentRunbookMatch>();
	public IReadOnlyList<IncidentHypothesis> Hypotheses { get; init; } = Array.Empty<IncidentHypothesis>();
	public IReadOnlyList<IncidentActionRecommendation> RecommendedActions { get; init; } = Array.Empty<IncidentActionRecommendation>();
	public IReadOnlyList<IncidentAnalysisEvidenceItem> Evidence { get; init; } = Array.Empty<IncidentAnalysisEvidenceItem>();
	public IReadOnlyList<SimilarIncidentMatch> SimilarIncidents { get; init; } = Array.Empty<SimilarIncidentMatch>();
	public AnalysisQualityScore Quality { get; init; } = new();
	public AnalysisProviderTransparency ProviderTransparency { get; init; } = new();
}
