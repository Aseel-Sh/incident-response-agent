namespace IncidentResponseAgent.Api.Contracts.Incidents;

public sealed record IncidentTimelineEventResponse(string Type, DateTimeOffset OccurredAtUtc, string Summary, string Actor, string? EvidenceReference);

public sealed record SimilarIncidentResponse(Guid IncidentId, string IncidentSummary, string ServiceName, string Environment, DateTimeOffset CreatedAtUtc, double Score, string ResolutionSummary, IReadOnlyList<string> SharedSignals, IReadOnlyList<string> SuccessfulActions, IReadOnlyList<string> FailedActions);

public sealed record ProposedKnowledgeUpdateResponse(Guid Id, string Title, string Content, string Status, DateTimeOffset GeneratedAtUtc, DateTimeOffset? ReviewedAtUtc, string? ReviewNotes);

public sealed record CandidateDecisionRequest
{
	public string Decision { get; init; } = string.Empty;
	public Guid? MergeIntoIncidentId { get; init; }
}

public sealed record KnowledgeReviewRequest
{
	public string Decision { get; init; } = string.Empty;
	public string? Content { get; init; }
	public string? Notes { get; init; }
}

public sealed record AnalysisFeedbackRequest
{
	public string AnalysisUsefulness { get; init; } = string.Empty;
	public string RecommendationCorrectness { get; init; } = string.Empty;
	public IReadOnlyList<string> ReasonTags { get; init; } = Array.Empty<string>();
	public string? RecommendationDescription { get; init; }
	public string? Comments { get; init; }
}

public sealed record AnalysisFeedbackResponse(Guid Id, string AnalysisUsefulness, string RecommendationCorrectness, IReadOnlyList<string> ReasonTags, string? RecommendationDescription, string? Comments, DateTimeOffset SubmittedAtUtc);

public sealed record GroundedClaimResponse(string Claim, IReadOnlyList<string> EvidenceReferences);
public sealed record RunbookMatchResponse(string Id, string Title, string Summary);
public sealed record AnalysisQualityResponse(string EvidenceCoverage, string RunbookMatchQuality, string RecommendationSpecificity, IReadOnlyList<string> MissingData, string ProviderUsed = "unknown", string FallbackStatus = "not used");
public sealed record ProviderTransparencyResponse(string ModelProvider, string? Model, string EmbeddingProvider, string VectorStore, string RagStatus, bool UsedModelFallback, string? FallbackReason, bool IsDegraded, string? DegradedReason);
