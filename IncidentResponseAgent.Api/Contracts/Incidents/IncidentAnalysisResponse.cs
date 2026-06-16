namespace IncidentResponseAgent.Api.Contracts.Incidents;

public sealed record IncidentAnalysisResponse
{
    public Guid IncidentId { get; init; }

    public string SessionId { get; init; } = string.Empty;

    public int SessionTurnNumber { get; init; }

    public string? SessionContextSummary { get; init; }

    public string IncidentSummary { get; init; } = string.Empty;

    public string AnalysisText { get; init; } = string.Empty;

    public string AnalysisProvider { get; init; } = string.Empty;

    public string? AnalysisModel { get; init; }

    public bool UsedFallbackAnalysis { get; init; }

    public string? FallbackReason { get; init; }

    public IReadOnlyList<IncidentAnalysisEvidenceItem> RetrievedEvidence { get; init; } = Array.Empty<IncidentAnalysisEvidenceItem>();

    public IReadOnlyList<IncidentHypothesis> RootCauseHypotheses { get; init; } = Array.Empty<IncidentHypothesis>();

    public IReadOnlyList<IncidentActionRecommendation> RecommendedActions { get; init; } = Array.Empty<IncidentActionRecommendation>();

    public IReadOnlyList<ActionOutcomeResponse> ActionOutcomes { get; init; } = Array.Empty<ActionOutcomeResponse>();

    public string? Confidence { get; init; }

    public string? Notes { get; init; }
}
