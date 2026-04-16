using IncidentResponseAgent.Domain.Incidents;

namespace IncidentResponseAgent.Application.Incidents;

public sealed class AnalyzeIncidentUseCase : IAnalyzeIncidentUseCase
{
	private readonly IIncidentAnalysisAgent _incidentAnalysisAgent;
	private readonly IIncidentAnalysisSessionStore _incidentAnalysisSessionStore;

	public AnalyzeIncidentUseCase(
		IIncidentAnalysisAgent incidentAnalysisAgent,
		IIncidentAnalysisSessionStore incidentAnalysisSessionStore)
	{
		_incidentAnalysisAgent = incidentAnalysisAgent;
		_incidentAnalysisSessionStore = incidentAnalysisSessionStore;
	}

	public async Task<IncidentAnalysisResult> AnalyzeAsync(Incident incident, string? sessionId = null, CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(incident);

		var sessionContext = await _incidentAnalysisSessionStore.GetOrCreateAsync(sessionId, cancellationToken);
		var analysisText = await _incidentAnalysisAgent.AnalyzeAsync(incident, sessionContext, cancellationToken);
		var nextSessionContext = sessionContext with
		{
			TurnNumber = sessionContext.TurnNumber + 1,
			LastIncidentSummary = BuildSummary(incident),
			LastAnalysisSummary = SummarizeAnalysisText(analysisText),
			UpdatedAtUtc = DateTimeOffset.UtcNow
		};

		await _incidentAnalysisSessionStore.SaveAsync(nextSessionContext, cancellationToken);

		var result = new IncidentAnalysisResult
		{
			SessionId = nextSessionContext.SessionId,
			SessionTurnNumber = nextSessionContext.TurnNumber,
			SessionContextSummary = BuildSessionSummary(nextSessionContext),
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

	private static string BuildSessionSummary(IncidentAnalysisSessionContext sessionContext)
	{
		var lastIncident = string.IsNullOrWhiteSpace(sessionContext.LastIncidentSummary)
			? "no previous incident context"
			: sessionContext.LastIncidentSummary;

		return $"Session {sessionContext.SessionId} turn {sessionContext.TurnNumber} with {lastIncident}.";
	}

	private static string SummarizeAnalysisText(string analysisText)
	{
		if (string.IsNullOrWhiteSpace(analysisText))
		{
			return "no analysis text";
		}

		var firstLine = analysisText.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault();
		return string.IsNullOrWhiteSpace(firstLine) ? "no analysis text" : firstLine;
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
				InferenceStrength = "Strong",
				Confidence = "Medium",
				SupportingEvidence =
				[
					"Incident description indicates active failures.",
					"Current flow already retrieved logs, metrics, and runbooks."
				],
				EvidenceReferences =
				[
					"incident.description",
					"tool.logs",
					"tool.metrics",
					"tool.runbooks"
				]
			}
		};

		if (incident.Severity is IncidentSeverity.High or IncidentSeverity.Critical)
		{
			hypotheses.Add(new IncidentHypothesis
			{
				Description = "The incident may be driven by a production regression or downstream dependency failure.",
				InferenceStrength = "Weak",
				Confidence = "Low",
				SupportingEvidence = ["Severity is high enough to suggest a broad impact."],
				EvidenceReferences = ["incident.severity"]
			});
		}

		if (!string.IsNullOrWhiteSpace(incident.ServiceName))
		{
			hypotheses.Add(new IncidentHypothesis
			{
				Description = $"The incident may align with service-specific operational guidance for {incident.ServiceName}.",
				InferenceStrength = "Medium",
				Confidence = "Medium",
				SupportingEvidence = ["A service name was provided on the incident.", "Relevant runbooks were retrieved for the service context."],
				EvidenceReferences = ["incident.serviceName", "tool.runbooks"]
			});
		}

		return hypotheses;
	}

	private static IReadOnlyList<IncidentActionRecommendation> BuildRecommendedActions(Incident incident)
	{
		var actions = new List<IncidentActionRecommendation>();

		if (incident.Severity is IncidentSeverity.Critical)
		{
			actions.Add(new IncidentActionRecommendation
			{
				Description = "Escalate the incident and begin mitigation immediately.",
				Priority = "Critical",
				Rationale = "Critical incidents require immediate response coordination and explicit ownership.",
				SupportingSignals = ["incident.severity", "response.comms"]
			});
		}

		actions.Add(new IncidentActionRecommendation
		{
			Description = "Confirm current blast radius and affected users.",
			Priority = "High",
			Rationale = "You need impact scope before choosing remediation.",
			SupportingSignals = ["incident.description", "tool.logs"]
		});

		actions.Add(new IncidentActionRecommendation
		{
			Description = "Review recent deployments, config changes, and dependency health.",
			Priority = "High",
			Rationale = "This often explains sudden regressions.",
			SupportingSignals = ["tool.runbooks", "tool.logs", "tool.metrics"]
		});

		actions.Add(new IncidentActionRecommendation
		{
			Description = "Collect supporting logs and metrics before making a remediation decision.",
			Priority = "Medium",
			Rationale = "Evidence should drive the next action.",
			SupportingSignals = ["tool.logs", "tool.metrics"]
		});

		if (incident.Severity is IncidentSeverity.High)
		{
			actions.Insert(0, new IncidentActionRecommendation
			{
				Description = "Prioritize investigation of the most affected service path first.",
				Priority = "High",
				Rationale = "High severity incidents still need quick triage of the highest-impact surface.",
				SupportingSignals = ["incident.severity", "tool.runbooks"]
			});
		}

		return actions;
	}
}