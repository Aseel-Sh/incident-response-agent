namespace IncidentResponseAgent.Agent.Incidents;

public sealed record IncidentAnalysisAgentOptions
{
	public string Name { get; init; } = "IncidentAnalysisAgent";

	public string Provider { get; init; } = "OpenAI-compatible provider";

	public string Model { get; init; } = string.Empty;

	public string? Endpoint { get; init; }

	public string? ApiKey { get; init; }
}