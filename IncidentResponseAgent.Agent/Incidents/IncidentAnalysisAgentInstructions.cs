using IncidentResponseAgent.Application.Incidents;
using IncidentResponseAgent.Domain.Incidents;
using IncidentResponseAgent.Domain.Runbooks;

namespace IncidentResponseAgent.Agent.Incidents;

public sealed class IncidentAnalysisAgentInstructions
{
	public string BuildPrompt(
		Incident incident,
		IncidentAnalysisAgentProfile profile,
		IncidentAnalysisSessionContext? sessionContext,
		IReadOnlyCollection<RunbookDocument> runbooks,
		IReadOnlyList<string> logHighlights,
		IReadOnlyList<string> metricHighlights,
		IReadOnlyList<SimilarIncidentMatch> similarIncidents)
	{
		ArgumentNullException.ThrowIfNull(incident);
		ArgumentNullException.ThrowIfNull(profile);
		ArgumentNullException.ThrowIfNull(runbooks);
		ArgumentNullException.ThrowIfNull(logHighlights);
		ArgumentNullException.ThrowIfNull(metricHighlights);
		ArgumentNullException.ThrowIfNull(similarIncidents);

		return $$"""
You are {{profile.Name}}, an incident analysis agent.
Provider: {{profile.Provider}}
Model: {{profile.Model}}

Analyze this incident and return concise operational guidance.
Use short sentences and avoid filler.
Return valid JSON only. Do not wrap it in Markdown.
Use this exact shape:
{
  "summary": "short incident summary",
  "severity": "SEV-1 | SEV-2 | SEV-3 | SEV-4 | SEV-5",
  "evidence": [
    {
      "summary": "what the evidence says",
      "source": "incident.description | rag.runbook.<id> | tool.logs | tool.metrics",
      "details": "specific supporting detail"
    }
  ],
  "hypotheses": [
    {
      "description": "likely root cause hypothesis",
      "inferenceStrength": "Strong | Medium | Weak",
      "confidence": "High | Medium | Low",
      "supportingEvidence": ["short evidence note"],
      "evidenceReferences": ["source reference"]
    }
  ],
  "recommendedActions": [
    {
      "description": "specific next action",
      "priority": "Critical | High | Medium | Low",
      "rationale": "why this action matters",
      "supportingSignals": ["source reference"]
    }
  ],
  "confidence": "High | Medium | Low",
  "notes": "short caveats"
}

Keep each section short and specific.
Use the retrieved evidence instead of inventing new facts.
Prefer operational language over generic commentary.
Call the available tools when you need logs, metrics, or runbooks.
Only use High confidence if you have explicit tool-backed evidence from logs or metrics and the evidence strongly agrees.
If tool evidence is thin, missing, or generic, keep confidence Low or Medium.
Do not state a confidence level that contradicts the evidence sections.

Session context:
{{BuildSessionSection(sessionContext)}}

Title: {{incident.Title}}
Description: {{incident.Description}}
Severity: {{FormatSeverity(incident.Severity)}}
Service: {{incident.ServiceName ?? "unknown"}}
Environment: {{incident.Environment ?? "unknown"}}
Tags: {{string.Join(", ", incident.Tags)}}

Relevant runbooks:
{{BuildRunbookSection(runbooks)}}

Log evidence:
{{BuildBulletSection(logHighlights)}}

Metric evidence:
{{BuildBulletSection(metricHighlights)}}

Similar previous incidents:
{{BuildSimilarIncidentSection(similarIncidents)}}
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

	private static string FormatSeverity(IncidentSeverity severity) => severity switch
	{
		IncidentSeverity.Sev1 => "SEV-1",
		IncidentSeverity.Sev2 => "SEV-2",
		IncidentSeverity.Sev3 => "SEV-3",
		IncidentSeverity.Sev4 => "SEV-4",
		IncidentSeverity.Sev5 => "SEV-5",
		_ => "SEV-5"
	};

	private static string BuildSimilarIncidentSection(IReadOnlyList<SimilarIncidentMatch> similarIncidents)
	{
		if (similarIncidents.Count == 0)
		{
			return "- None found.";
		}

		return string.Join(Environment.NewLine, similarIncidents.Select(incident =>
			$"- {incident.IncidentSummary} ({incident.ServiceName}/{incident.Environment}, score {incident.Score:0.00}); successful actions: {string.Join(" | ", incident.SuccessfulActions)}; failed actions: {string.Join(" | ", incident.FailedActions)}"));
	}

	private static string BuildSessionSection(IncidentAnalysisSessionContext? sessionContext)
	{
		if (sessionContext is null)
		{
			return "- New session.";
		}

		var lastIncident = string.IsNullOrWhiteSpace(sessionContext.LastIncidentSummary)
			? "none"
			: sessionContext.LastIncidentSummary;
		var lastAnalysis = string.IsNullOrWhiteSpace(sessionContext.LastAnalysisSummary)
			? "none"
			: sessionContext.LastAnalysisSummary;

		return string.Join(Environment.NewLine, new[]
		{
			$"- Session id: {sessionContext.SessionId}",
			$"- Current turn: {sessionContext.TurnNumber + 1}",
			$"- Previous incident summary: {lastIncident}",
			$"- Previous analysis summary: {lastAnalysis}"
		});
	}
}
