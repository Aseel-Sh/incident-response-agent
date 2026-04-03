namespace IncidentResponseAgent.Agent.Incidents;

public sealed record IncidentAnalysisAgentOptions
{
	public string Provider { get; init; } = string.Empty;

	public string Model { get; init; } = string.Empty;

	public string? Endpoint { get; init; }

	public string? ApiKey { get; init; }
}