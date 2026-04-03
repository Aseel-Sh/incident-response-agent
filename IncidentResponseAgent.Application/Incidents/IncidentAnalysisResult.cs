namespace IncidentResponseAgent.Application.Incidents;

public sealed record IncidentAnalysisResult
{
	public Guid IncidentId { get; init; }

	public string IncidentSummary { get; init; } = string.Empty;

	public IReadOnlyList<string> Evidence { get; init; } = Array.Empty<string>();

	public IReadOnlyList<string> Hypotheses { get; init; } = Array.Empty<string>();

	public IReadOnlyList<string> RecommendedActions { get; init; } = Array.Empty<string>();

	public string? Confidence { get; init; }

	public string? Notes { get; init; }
}