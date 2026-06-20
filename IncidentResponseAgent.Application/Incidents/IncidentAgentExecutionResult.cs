namespace IncidentResponseAgent.Application.Incidents;

public sealed record IncidentAgentExecutionResult
{
	public required string AnalysisText { get; init; }

	public required string Provider { get; init; }

	public string? Model { get; init; }

	public string? AttemptedProvider { get; init; }

	public string? AttemptedModel { get; init; }

	public bool UsedFallback { get; init; }

	public string? FallbackReason { get; init; }

	public bool UsedStructuredOutputRetry { get; init; }

	public string? StructuredOutputRetryReason { get; init; }

	public long ModelDurationMilliseconds { get; init; }

	public string? FallbackStage { get; init; }

	public string? TimeoutSource { get; init; }
}
