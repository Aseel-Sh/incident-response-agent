using IncidentResponseAgent.Application.Incidents;
using IncidentResponseAgent.Domain.Incidents;

namespace IncidentResponseAgent.Agent.Incidents;

public sealed class PromptBasedIncidentAnalysisAgent : IIncidentAnalysisAgent
{
	private readonly IIncidentAnalysisAgentFactory _agentFactory;

	public PromptBasedIncidentAnalysisAgent(IIncidentAnalysisAgentFactory agentFactory)
	{
		_agentFactory = agentFactory;
	}

	public Task<string> AnalyzeAsync(Incident incident, CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(incident);

		var profile = _agentFactory.Create();
		var prompt = BuildPrompt(incident, profile);
		var response = BuildAnalysisText(incident, profile, prompt);

		return Task.FromResult(response);
	}

	private static string BuildPrompt(Incident incident, IncidentAnalysisAgentProfile profile)
	{
        //will probably refine this later
		return $"""
        You are {profile.Name}, an incident analysis agent.
        Provider: {profile.Provider}
        Model: {profile.Model}

        Analyze this incident and return concise operational guidance.

        Title: {incident.Title}
        Description: {incident.Description}
        Severity: {incident.Severity}
        Service: {incident.ServiceName ?? "unknown"}
        Environment: {incident.Environment ?? "unknown"}
        Tags: {string.Join(", ", incident.Tags)}
        """;

	}

	private static string BuildAnalysisText(Incident incident, IncidentAnalysisAgentProfile profile, string prompt)
	{
		var serviceName = string.IsNullOrWhiteSpace(incident.ServiceName) ? "the impacted service" : incident.ServiceName;
		var promptLength = prompt.Length;

		return $"[{profile.Name}] Prompt-based analysis for {serviceName}: start with log review, confirm recent changes, and validate the incident scope. Prompt size: {promptLength} characters.";
	}
}