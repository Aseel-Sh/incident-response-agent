namespace IncidentResponseAgent.Application.Incidents;

public sealed record IncidentAgentExecutionResult
{
	public required string AnalysisText { get; init; }

	public required string Provider { get; init; }

	public string? Model { get; init; }

	public bool UsedFallback { get; init; }

	public string? FallbackReason { get; init; }
}
