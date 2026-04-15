using IncidentResponseAgent.Domain.Incidents;

namespace IncidentResponseAgent.Application.Incidents;

public sealed class AnalyzeIncidentUseCase : IAnalyzeIncidentUseCase
{
	private readonly IIncidentAnalysisAgent _incidentAnalysisAgent;

	public AnalyzeIncidentUseCase(IIncidentAnalysisAgent incidentAnalysisAgent)
	{
		_incidentAnalysisAgent = incidentAnalysisAgent;
	}

	public async Task<IncidentAnalysisResult> AnalyzeAsync(Incident incident, CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(incident);

		var analysisText = await _incidentAnalysisAgent.AnalyzeAsync(incident, cancellationToken);

		var result = new IncidentAnalysisResult
		{
			IncidentId = incident.Id,
			IncidentSummary = BuildSummary(incident),
			AnalysisText = analysisText,
			Evidence = BuildEvidence(incident),
			Hypotheses = BuildHypotheses(incident),
			RecommendedActions = BuildRecommendedActions(incident),
			Confidence = "Low",
			Notes = "Initial application-layer orchestration now calls a prompt-based agent service."
		};

		return result;
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

	private static IReadOnlyList<IncidentAnalysisEvidenceItem> BuildEvidence(Incident incident)
	{
		var evidence = new List<IncidentAnalysisEvidenceItem>
		{
			new IncidentAnalysisEvidenceItem
			{
				Summary = incident.Description,
				Source = "incident.description",
				Details = incident.Title
			}
		};

		if (incident.Tags.Count > 0)
		{
			evidence.Add(new IncidentAnalysisEvidenceItem
			{
				Summary = "Incident tags indicate impacted area and context.",
				Source = "incident.tags",
				Details = string.Join(", ", incident.Tags)
			});
		}

		if (incident.Timestamp is not null)
		{
			evidence.Add(new IncidentAnalysisEvidenceItem
			{
				Summary = "Incident timestamp available for time-based correlation.",
				Source = "incident.timestamp",
				Details = incident.Timestamp.Value.ToString("O")
			});
		}

		return evidence;
	}

	private static IReadOnlyList<IncidentHypothesis> BuildHypotheses(Incident incident)
	{
		var hypotheses = new List<IncidentHypothesis>
		{
			new IncidentHypothesis
			{
				Description = $"Investigate recent changes affecting {incident.ServiceName ?? "the impacted service"}.",
				Confidence = "Medium",
				SupportingEvidence =
				[
					"Incident description indicates active failures.",
					"Current flow already retrieved logs, metrics, and runbooks."
				]
			}
		};

		if (incident.Severity is IncidentSeverity.High or IncidentSeverity.Critical)
		{
			hypotheses.Add(new IncidentHypothesis
			{
				Description = "The incident may be driven by a production regression or downstream dependency failure.",
				Confidence = "Low",
				SupportingEvidence = ["Severity is high enough to suggest a broad impact."]
			});
		}

		return hypotheses;
	}

	private static IReadOnlyList<IncidentActionRecommendation> BuildRecommendedActions(Incident incident)
	{
		var actions = new List<IncidentActionRecommendation>
		{
			new IncidentActionRecommendation
			{
				Description = "Confirm current blast radius and affected users.",
				Priority = "High",
				Rationale = "You need impact scope before choosing remediation."
			},
			new IncidentActionRecommendation
			{
				Description = "Review recent deployments, config changes, and dependency health.",
				Priority = "High",
				Rationale = "This often explains sudden regressions."
			},
			new IncidentActionRecommendation
			{
				Description = "Collect supporting logs and metrics before making a remediation decision.",
				Priority = "Medium",
				Rationale = "Evidence should drive the next action."
			}
		};

		if (incident.Severity is IncidentSeverity.Critical)
		{
			actions.Insert(0, new IncidentActionRecommendation
			{
				Description = "Escalate the incident and begin mitigation immediately.",
				Priority = "Critical",
				Rationale = "Critical incidents require immediate response coordination."
			});
		}

		return actions;
	}
}