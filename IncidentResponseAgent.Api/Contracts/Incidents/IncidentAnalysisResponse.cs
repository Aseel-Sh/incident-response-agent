namespace IncidentResponseAgent.Api.Contracts.Incidents;

public sealed record IncidentAnalysisResponse
{
    public string IncidentSummary { get; init; } = string.Empty;

    public string AnalysisText { get; init; } = string.Empty;

    public IReadOnlyList<string> RetrievedEvidence { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> RootCauseHypotheses { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> RecommendedActions { get; init; } = Array.Empty<string>();

    public string? Confidence { get; init; }

    public string? Notes { get; init; }
}