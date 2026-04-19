namespace IncidentResponseAgent.Api.Contracts.Incidents;

public sealed record RecentIncidentAnalysisResponse
{
	public required Guid IncidentId { get; init; }

	public required string IncidentSummary { get; init; }

	public required string AnalysisText { get; init; }

	public required string SessionId { get; init; }

	public required int SessionTurnNumber { get; init; }

	public string? Confidence { get; init; }

	public string? Notes { get; init; }

	public required DateTimeOffset CreatedAtUtc { get; init; }
}