using IncidentResponseAgent.Domain.Incidents;
using IncidentResponseAgent.Domain.Runbooks;

namespace IncidentResponseAgent.Agent.Incidents;

public sealed class IncidentAnalysisAgentInstructions
{
	public string BuildPrompt(
		Incident incident,
		IncidentAnalysisAgentProfile profile,
		IReadOnlyCollection<RunbookDocument> runbooks)
	{
		ArgumentNullException.ThrowIfNull(incident);
		ArgumentNullException.ThrowIfNull(profile);
		ArgumentNullException.ThrowIfNull(runbooks);

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

Relevant runbooks:
{BuildRunbookSection(runbooks)}
""";
	}

	private static string BuildRunbookSection(IReadOnlyCollection<RunbookDocument> runbooks)
	{
		if (runbooks.Count == 0)
		{
			return "None found.";
		}

		return string.Join(Environment.NewLine, runbooks.Select(runbook => $"- {runbook.Title}: {runbook.Summary}"));
	}
}