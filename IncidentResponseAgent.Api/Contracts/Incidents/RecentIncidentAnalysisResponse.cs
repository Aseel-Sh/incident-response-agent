namespace IncidentResponseAgent.Api.Contracts.Incidents;

public sealed record RecentIncidentAnalysisResponse
{
	public string AnalysisState { get; init; } = "completed";
	public string? Assignee { get; init; }
	public string? AcknowledgedBy { get; init; }
	public DateTimeOffset? AcknowledgedAtUtc { get; init; }
	public required Guid IncidentId { get; init; }

	public string ProjectId { get; init; } = "default";

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

	public IReadOnlyList<ActionOutcomeResponse> ActionOutcomes { get; init; } = Array.Empty<ActionOutcomeResponse>();

	public string Status { get; init; } = "active";

	public required DateTimeOffset CreatedAtUtc { get; init; }

	public IReadOnlyList<IncidentTimelineEventResponse> Timeline { get; init; } = Array.Empty<IncidentTimelineEventResponse>();

	public ProposedKnowledgeUpdateResponse? ProposedKnowledgeUpdate { get; init; }

	public IReadOnlyList<AnalysisFeedbackResponse> Feedback { get; init; } = Array.Empty<AnalysisFeedbackResponse>();
	public IReadOnlyList<GroundedClaimResponse> KnownFacts { get; init; } = Array.Empty<GroundedClaimResponse>();
	public IReadOnlyList<string> Unknowns { get; init; } = Array.Empty<string>();
	public IReadOnlyList<RunbookMatchResponse> RunbookMatches { get; init; } = Array.Empty<RunbookMatchResponse>();
	public IReadOnlyList<IncidentHypothesis> Hypotheses { get; init; } = Array.Empty<IncidentHypothesis>();
	public IReadOnlyList<IncidentActionRecommendation> RecommendedActions { get; init; } = Array.Empty<IncidentActionRecommendation>();
	public IReadOnlyList<IncidentAnalysisEvidenceItem> Evidence { get; init; } = Array.Empty<IncidentAnalysisEvidenceItem>();
	public IReadOnlyList<SimilarIncidentResponse> SimilarIncidents { get; init; } = Array.Empty<SimilarIncidentResponse>();
	public AnalysisQualityResponse Quality { get; init; } = new("Low", "Low", "Low", Array.Empty<string>());
	public ProviderTransparencyResponse ProviderTransparency { get; init; } = new("unknown", null, "unknown", "unknown", "unknown", false, null, false, null);
}
