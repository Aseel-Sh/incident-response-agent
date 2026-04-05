using IncidentResponseAgent.Domain.Incidents;

namespace IncidentResponseAgent.Agent.Incidents;

public sealed class IncidentAnalysisAgentInstructions
{
	public string BuildPrompt(Incident incident, IncidentAnalysisAgentProfile profile)
	{
		ArgumentNullException.ThrowIfNull(incident);
		ArgumentNullException.ThrowIfNull(profile);

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
}