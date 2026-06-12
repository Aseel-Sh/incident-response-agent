namespace IncidentResponseAgent.Application.Incidents;

public sealed record GetRecentIncidentAnalysesResult
{
	public required Guid IncidentId { get; init; }

	public required string IncidentSummary { get; init; }

	public required string AnalysisText { get; init; }

	public string AnalysisProvider { get; init; } = string.Empty;

	public string? AnalysisModel { get; init; }

	public bool UsedFallbackAnalysis { get; init; }

	public string? FallbackReason { get; init; }

	public required string SessionId { get; init; }

	public required int SessionTurnNumber { get; init; }

	public string? Confidence { get; init; }

	public string? Notes { get; init; }

	public required DateTimeOffset CreatedAtUtc { get; init; }
}
