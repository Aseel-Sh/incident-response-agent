using IncidentResponseAgent.Domain.Incidents;
using IncidentResponseAgent.Domain.Runbooks;

namespace IncidentResponseAgent.Agent.Incidents;

public sealed class IncidentAnalysisAgentInstructions
{
	public string BuildPrompt(
		Incident incident,
		IncidentAnalysisAgentProfile profile,
		IReadOnlyCollection<RunbookDocument> runbooks,
		IReadOnlyList<string> logHighlights,
		IReadOnlyList<string> metricHighlights)
	{
		ArgumentNullException.ThrowIfNull(incident);
		ArgumentNullException.ThrowIfNull(profile);
		ArgumentNullException.ThrowIfNull(runbooks);
		ArgumentNullException.ThrowIfNull(logHighlights);
		ArgumentNullException.ThrowIfNull(metricHighlights);

		return $"""
You are {profile.Name}, an incident analysis agent.
Provider: {profile.Provider}
Model: {profile.Model}

Analyze this incident and return concise operational guidance.
Use short sentences and avoid filler.
Return the analysis in these sections only:
1. Summary
2. Evidence
3. Hypotheses
4. Recommended actions
5. Confidence
6. Notes

Keep each section short and specific.
Use the retrieved evidence instead of inventing new facts.
Prefer operational language over generic commentary.

Title: {incident.Title}
Description: {incident.Description}
Severity: {incident.Severity}
Service: {incident.ServiceName ?? "unknown"}
Environment: {incident.Environment ?? "unknown"}
Tags: {string.Join(", ", incident.Tags)}

Relevant runbooks:
{BuildRunbookSection(runbooks)}

Log evidence:
{BuildBulletSection(logHighlights)}

Metric evidence:
{BuildBulletSection(metricHighlights)}
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

	private static string BuildBulletSection(IReadOnlyList<string> items)
	{
		if (items.Count == 0)
		{
			return "- None.";
		}

		return string.Join(Environment.NewLine, items.Select(item => $"- {item}"));
	}
}