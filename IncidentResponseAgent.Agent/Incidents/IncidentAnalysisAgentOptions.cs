namespace IncidentResponseAgent.Agent.Incidents;

public sealed record IncidentAnalysisAgentOptions
{
	public string Name { get; init; } = "IncidentAnalysisAgent";

	public string Provider { get; init; } = "OpenAI-compatible provider";

	public string Model { get; init; } = string.Empty;

	public string? Endpoint { get; init; }

	public string? ApiKey { get; init; }

	public int AnalysisTimeoutSeconds { get; init; } = 30;

	public int MaxOutputTokens { get; init; } = 1200;

	public double Temperature { get; init; } = 0.1;
}
