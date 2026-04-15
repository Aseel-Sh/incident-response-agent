namespace IncidentResponseAgent.Api.Contracts.Incidents;

public sealed record IncidentAnalysisResponse
{
    public string IncidentSummary { get; init; } = string.Empty;

    public string AnalysisText { get; init; } = string.Empty;

    public IReadOnlyList<IncidentAnalysisEvidenceItem> RetrievedEvidence { get; init; } = Array.Empty<IncidentAnalysisEvidenceItem>();

    public IReadOnlyList<IncidentHypothesis> RootCauseHypotheses { get; init; } = Array.Empty<IncidentHypothesis>();

    public IReadOnlyList<IncidentActionRecommendation> RecommendedActions { get; init; } = Array.Empty<IncidentActionRecommendation>();

    public string? Confidence { get; init; }

    public string? Notes { get; init; }
}