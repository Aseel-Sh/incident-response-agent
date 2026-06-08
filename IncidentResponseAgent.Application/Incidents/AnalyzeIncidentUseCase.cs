using IncidentResponseAgent.Domain.Incidents;
using IncidentResponseAgent.Application.Runbooks;
using IncidentResponseAgent.Application.Tools;
using System.Text.RegularExpressions;

namespace IncidentResponseAgent.Application.Incidents;

public sealed class AnalyzeIncidentUseCase : IAnalyzeIncidentUseCase
{
	private readonly IIncidentAnalysisAgent _incidentAnalysisAgent;
	private readonly IIncidentAnalysisSessionStore _incidentAnalysisSessionStore;
	private readonly IIncidentRecordStore _incidentRecordStore;
	private readonly ILogSearchProvider _logSearchProvider;
	private readonly IMetricsProvider _metricsProvider;
	private readonly IRunbookRetrievalService _runbookRetrievalService;

	public AnalyzeIncidentUseCase(
		IIncidentAnalysisAgent incidentAnalysisAgent,
		IIncidentAnalysisSessionStore incidentAnalysisSessionStore,
		IIncidentRecordStore incidentRecordStore,
		ILogSearchProvider logSearchProvider,
		IMetricsProvider metricsProvider,
		IRunbookRetrievalService runbookRetrievalService)
	{
		_incidentAnalysisAgent = incidentAnalysisAgent;
		_incidentAnalysisSessionStore = incidentAnalysisSessionStore;
		_incidentRecordStore = incidentRecordStore;
		_logSearchProvider = logSearchProvider;
		_metricsProvider = metricsProvider;
		_runbookRetrievalService = runbookRetrievalService;
	}

	public async Task<IncidentAnalysisResult> AnalyzeAsync(Incident incident, string? sessionId = null, CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(incident);

		var sessionContext = await _incidentAnalysisSessionStore.GetOrCreateAsync(sessionId, cancellationToken);
		var runbookResult = await _runbookRetrievalService.RetrieveAsync(BuildRunbookRetrievalRequest(incident), cancellationToken);
		var logResult = await _logSearchProvider.SearchAsync(BuildLogSearchRequest(incident), cancellationToken);
		var metricsResult = await _metricsProvider.QueryAsync(BuildMetricsQueryRequest(incident), cancellationToken);
		var analysisText = await _incidentAnalysisAgent.AnalyzeAsync(incident, sessionContext, cancellationToken);
		var confidence = ExtractConfidence(analysisText) ?? "Low";
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
			Evidence = BuildEvidence(incident, runbookResult, logResult, metricsResult),
			Hypotheses = BuildHypotheses(incident, runbookResult, logResult, metricsResult),
			RecommendedActions = BuildRecommendedActions(incident, runbookResult, logResult, metricsResult),
			Confidence = confidence,
			Notes = "Application orchestration captured incident details, RAG runbooks, logs, metrics, session state, and agent analysis."
		};

		await _incidentRecordStore.SaveAsync(incident, result, cancellationToken);

		return result;
	}

	private static RunbookRetrievalRequest BuildRunbookRetrievalRequest(Incident incident)
	{
		return new RunbookRetrievalRequest
		{
			Query = string.Join(' ', new[] { incident.Title, incident.Description }.Concat(incident.Tags)),
			ServiceName = incident.ServiceName,
			Environment = incident.Environment,
			MaxResults = 3
		};
	}

	private static LogSearchRequest BuildLogSearchRequest(Incident incident)
	{
		return new LogSearchRequest
		{
			Query = incident.Title,
			ServiceName = incident.ServiceName,
			Environment = incident.Environment,
			StartTime = incident.Timestamp?.AddHours(-1),
			EndTime = incident.Timestamp,
			MaxResults = 3
		};
	}

	private static MetricsQueryRequest BuildMetricsQueryRequest(Incident incident)
	{
		return new MetricsQueryRequest
		{
			MetricName = "request_error_rate",
			ServiceName = incident.ServiceName,
			Environment = incident.Environment,
			StartTime = incident.Timestamp?.AddHours(-1),
			EndTime = incident.Timestamp
		};
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

	private static string? ExtractConfidence(string analysisText)
	{
		if (string.IsNullOrWhiteSpace(analysisText))
		{
			return null;
		}

		var match = Regex.Match(analysisText, @"(?im)^Confidence\s*$\s*(?<value>.+?)(?:\r?$|\n(?:\S|\s))*?(?:^Notes\s*$|\z)");
		if (match.Success)
		{
			var value = match.Groups["value"].Value.Trim();
			if (value.StartsWith("High", StringComparison.OrdinalIgnoreCase))
			{
				return "High";
			}

			if (value.StartsWith("Medium", StringComparison.OrdinalIgnoreCase))
			{
				return "Medium";
			}

			if (value.StartsWith("Low", StringComparison.OrdinalIgnoreCase))
			{
				return "Low";
			}
		}

		return null;
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

	private static IReadOnlyList<IncidentAnalysisEvidenceItem> BuildEvidence(
		Incident incident,
		RunbookRetrievalResult runbookResult,
		LogSearchResult logResult,
		MetricsQueryResult metricsResult)
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

		foreach (var runbook in runbookResult.Runbooks)
		{
			evidence.Add(new IncidentAnalysisEvidenceItem
			{
				Summary = runbook.Summary,
				Source = $"rag.runbook.{runbook.Id}",
				Details = runbook.Title
			});
		}

		foreach (var entry in logResult.Entries)
		{
			evidence.Add(new IncidentAnalysisEvidenceItem
			{
				Summary = entry.Message,
				Source = "tool.logs",
				Details = $"{entry.Timestamp:O} {entry.Level} {entry.Source}"
			});
		}

		if (metricsResult.Samples.Count > 0)
		{
			var latest = metricsResult.Samples.OrderByDescending(sample => sample.Timestamp).First();
			evidence.Add(new IncidentAnalysisEvidenceItem
			{
				Summary = $"Latest {metricsResult.MetricName} sample is {latest.Value}.",
				Source = "tool.metrics",
				Details = $"{metricsResult.Samples.Count} samples ending at {latest.Timestamp:O}"
			});
		}

		return evidence;
	}

	private static IReadOnlyList<IncidentHypothesis> BuildHypotheses(
		Incident incident,
		RunbookRetrievalResult runbookResult,
		LogSearchResult logResult,
		MetricsQueryResult metricsResult)
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
					$"Retrieved {logResult.Entries.Count} log entries, {metricsResult.Samples.Count} metric samples, and {runbookResult.Runbooks.Count} runbook chunks."
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

		var primaryRunbook = runbookResult.Runbooks.FirstOrDefault();
		if (primaryRunbook is not null)
		{
			hypotheses.Add(new IncidentHypothesis
			{
				Description = $"The incident may match guidance from '{primaryRunbook.Title}'.",
				InferenceStrength = "Medium",
				Confidence = "Medium",
				SupportingEvidence = [primaryRunbook.Summary],
				EvidenceReferences = [$"rag.runbook.{primaryRunbook.Id}"]
			});
		}

		var latestMetric = metricsResult.Samples.OrderByDescending(sample => sample.Timestamp).FirstOrDefault();
		if (latestMetric is not null && latestMetric.Value > 25)
		{
			hypotheses.Add(new IncidentHypothesis
			{
				Description = "The error-rate metric suggests the symptom is measurable and currently elevated.",
				InferenceStrength = "Medium",
				Confidence = "Medium",
				SupportingEvidence = [$"{metricsResult.MetricName} latest sample is {latestMetric.Value}."],
				EvidenceReferences = ["tool.metrics"]
			});
		}

		return hypotheses;
	}

	private static IReadOnlyList<IncidentActionRecommendation> BuildRecommendedActions(
		Incident incident,
		RunbookRetrievalResult runbookResult,
		LogSearchResult logResult,
		MetricsQueryResult metricsResult)
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
			SupportingSignals = logResult.Entries.Count > 0 ? ["incident.description", "tool.logs"] : ["incident.description"]
		});

		actions.Add(new IncidentActionRecommendation
		{
			Description = "Review recent deployments, config changes, and dependency health.",
			Priority = "High",
			Rationale = "This often explains sudden regressions.",
			SupportingSignals = ["rag.runbooks", "tool.logs", "tool.metrics"]
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

		if (runbookResult.Runbooks.Count > 0)
		{
			actions.Add(new IncidentActionRecommendation
			{
				Description = $"Follow the most relevant retrieved runbook: {runbookResult.Runbooks[0].Title}.",
				Priority = "High",
				Rationale = "RAG found operational guidance that matches the incident context.",
				SupportingSignals = [$"rag.runbook.{runbookResult.Runbooks[0].Id}"]
			});
		}

		if (metricsResult.Samples.Count == 0)
		{
			actions.Add(new IncidentActionRecommendation
			{
				Description = "Add or verify metrics for this service path before relying on automated diagnosis.",
				Priority = "Medium",
				Rationale = "A full incident response agent needs measurable signals for confidence.",
				SupportingSignals = ["tool.metrics"]
			});
		}

		return actions;
	}
}
