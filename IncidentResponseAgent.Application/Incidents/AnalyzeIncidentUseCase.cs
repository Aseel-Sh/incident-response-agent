using IncidentResponseAgent.Domain.Incidents;

namespace IncidentResponseAgent.Application.Incidents;

public sealed class AnalyzeIncidentUseCase : IAnalyzeIncidentUseCase
{
	public Task<IncidentAnalysisResult> AnalyzeAsync(Incident incident, CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(incident);

		var result = new IncidentAnalysisResult
		{
			IncidentId = incident.Id,
			IncidentSummary = BuildSummary(incident),
			Evidence = BuildEvidence(incident),
			Hypotheses = BuildHypotheses(incident),
			RecommendedActions = BuildRecommendedActions(incident),
			Confidence = "Low",
			Notes = "Initial application-layer orchestration placeholder. AI analysis will be added in a later step."
		};

		return Task.FromResult(result);
	}

	private static string BuildSummary(Incident incident)
	{
		var servicePart = string.IsNullOrWhiteSpace(incident.ServiceName)
			? "an unspecified service"
			: incident.ServiceName;

		var environmentPart = string.IsNullOrWhiteSpace(incident.Environment)
			? "an unspecified environment"
			: incident.Environment;

		return $"{incident.Severity} incident reported for {servicePart} in {environmentPart}: {incident.Title}.";
	}

	private static IReadOnlyList<string> BuildEvidence(Incident incident)
	{
		var evidence = new List<string>
		{
			incident.Description
		};

		if (incident.Tags.Count > 0)
		{
			evidence.Add($"Tags: {string.Join(", ", incident.Tags)}.");
		}

		if (incident.Timestamp is not null)
		{
			evidence.Add($"Reported at: {incident.Timestamp:O}.");
		}

		return evidence;
	}

	private static IReadOnlyList<string> BuildHypotheses(Incident incident)
	{
		var hypotheses = new List<string>
		{
			$"Investigate recent changes affecting {incident.ServiceName ?? "the impacted service"}."
		};

		if (incident.Severity is IncidentSeverity.High or IncidentSeverity.Critical)
		{
			hypotheses.Add("The incident may be driven by a production regression or downstream dependency failure.");
		}

		return hypotheses;
	}

	private static IReadOnlyList<string> BuildRecommendedActions(Incident incident)
	{
		var actions = new List<string>
		{
			"Confirm current blast radius and affected users.",
			"Review recent deployments, config changes, and dependency health.",
			"Collect supporting logs and metrics before making a remediation decision."
		};

		if (incident.Severity is IncidentSeverity.Critical)
		{
			actions.Insert(0, "Escalate the incident and begin mitigation immediately.");
		}

		return actions;
	}
}