namespace IncidentResponseAgent.Agent.Incidents;

public sealed record IncidentAnalysisAgentProfile
{
	public required string Provider { get; init; }

	public required string Model { get; init; }

	public string? Endpoint { get; init; }

	public string? ApiKey { get; init; }

	public required string Name { get; init; }
}