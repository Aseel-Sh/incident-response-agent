namespace IncidentResponseAgent.Api.Contracts.Incidents;

public sealed record IncidentAnalysisResponse
{
    public Guid IncidentId { get; init; }

    public string SessionId { get; init; } = string.Empty;

    public int SessionTurnNumber { get; init; }

    public string? SessionContextSummary { get; init; }

    public string IncidentSummary { get; init; } = string.Empty;

    public string Severity { get; init; } = string.Empty;

    public string AnalysisText { get; init; } = string.Empty;

    public string AnalysisProvider { get; init; } = string.Empty;

    public string? AnalysisModel { get; init; }

    public bool UsedFallbackAnalysis { get; init; }

    public string? FallbackReason { get; init; }

    public IReadOnlyList<IncidentAnalysisEvidenceItem> RetrievedEvidence { get; init; } = Array.Empty<IncidentAnalysisEvidenceItem>();

    public IReadOnlyList<GroundedClaimResponse> KnownFacts { get; init; } = Array.Empty<GroundedClaimResponse>();

    public IReadOnlyList<string> Unknowns { get; init; } = Array.Empty<string>();

    public IReadOnlyList<RunbookMatchResponse> RunbookMatches { get; init; } = Array.Empty<RunbookMatchResponse>();

    public IReadOnlyList<IncidentHypothesis> RootCauseHypotheses { get; init; } = Array.Empty<IncidentHypothesis>();

    public IReadOnlyList<IncidentActionRecommendation> RecommendedActions { get; init; } = Array.Empty<IncidentActionRecommendation>();

    public IReadOnlyList<ActionOutcomeResponse> ActionOutcomes { get; init; } = Array.Empty<ActionOutcomeResponse>();

    public IReadOnlyList<SimilarIncidentResponse> SimilarIncidents { get; init; } = Array.Empty<SimilarIncidentResponse>();

    public AnalysisQualityResponse Quality { get; init; } = new("Low", "Low", "Low", Array.Empty<string>());

    public ProviderTransparencyResponse ProviderTransparency { get; init; } = new("unknown", null, "unknown", "unknown", "unknown", false, null, false, null);

    public string? Confidence { get; init; }

    public string? Notes { get; init; }
}
