namespace IncidentResponseAgent.Application.Incidents;

public sealed record IncidentAnalysisSessionContext
{
	public required string SessionId { get; init; }

	public int TurnNumber { get; init; }

	public string? LastIncidentSummary { get; init; }

	public string? LastAnalysisSummary { get; init; }

	public DateTimeOffset UpdatedAtUtc { get; init; }
}