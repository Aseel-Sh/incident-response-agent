using IncidentResponseAgent.Domain.Incidents;
using IncidentResponseAgent.Domain.Runbooks;
using IncidentResponseAgent.Application.Runbooks;
using IncidentResponseAgent.Application.Tools;
using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;

namespace IncidentResponseAgent.Application.Incidents;

public sealed class AnalyzeIncidentUseCase : IAnalyzeIncidentUseCase
{
	private readonly IIncidentAnalysisAgent _incidentAnalysisAgent;
	private readonly IIncidentAnalysisSessionStore _incidentAnalysisSessionStore;
	private readonly IIncidentRecordStore _incidentRecordStore;
	private readonly ILogSearchProvider _logSearchProvider;
	private readonly ILogger<AnalyzeIncidentUseCase> _logger;
	private readonly IMetricsProvider _metricsProvider;
	private readonly IRunbookRetrievalService _runbookRetrievalService;

	public AnalyzeIncidentUseCase(
		IIncidentAnalysisAgent incidentAnalysisAgent,
		IIncidentAnalysisSessionStore incidentAnalysisSessionStore,
		IIncidentRecordStore incidentRecordStore,
		ILogSearchProvider logSearchProvider,
		IMetricsProvider metricsProvider,
		IRunbookRetrievalService runbookRetrievalService,
		ILogger<AnalyzeIncidentUseCase> logger)
	{
		_incidentAnalysisAgent = incidentAnalysisAgent;
		_incidentAnalysisSessionStore = incidentAnalysisSessionStore;
		_incidentRecordStore = incidentRecordStore;
		_logSearchProvider = logSearchProvider;
		_logger = logger;
		_metricsProvider = metricsProvider;
		_runbookRetrievalService = runbookRetrievalService;
	}

	public async Task<IncidentAnalysisResult> AnalyzeAsync(Incident incident, string? sessionId = null, CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(incident);

		_logger.LogInformation(
			"Starting incident analysis for IncidentId={IncidentId} Severity={Severity} Service={ServiceName} Environment={Environment}.",
			incident.Id,
			incident.Severity,
			incident.ServiceName,
			incident.Environment);
		var sessionContextTask = _incidentAnalysisSessionStore.GetOrCreateAsync(sessionId, cancellationToken);
		var runbookResultTask = _runbookRetrievalService.RetrieveAsync(BuildRunbookRetrievalRequest(incident), cancellationToken);
		var logResultTask = _logSearchProvider.SearchAsync(BuildLogSearchRequest(incident), cancellationToken);
		var metricsResultTask = _metricsProvider.QueryAsync(BuildMetricsQueryRequest(incident), cancellationToken);
		var similarIncidentsTask = _incidentRecordStore.FindSimilarAsync(incident, 3, cancellationToken);

		await Task.WhenAll(sessionContextTask, runbookResultTask, logResultTask, metricsResultTask, similarIncidentsTask);

		var sessionContext = await sessionContextTask;
		var runbookResult = await runbookResultTask;
		var logResult = await logResultTask;
		var metricsResult = await metricsResultTask;
		var similarIncidents = await similarIncidentsTask;
		_logger.LogInformation(
			"Incident evidence gathered for IncidentId={IncidentId}: Runbooks={RunbookCount} Logs={LogCount} MetricSamples={MetricSampleCount} SimilarIncidents={SimilarIncidentCount}.",
			incident.Id,
			runbookResult.Runbooks.Count,
			logResult.Entries.Count,
			metricsResult.Samples.Count,
			similarIncidents.Count);
		var agentContext = new IncidentAnalysisAgentContext
		{
			Runbooks = runbookResult,
			Logs = logResult,
			Metrics = metricsResult,
			SimilarIncidents = similarIncidents
		};
		var agentResult = await _incidentAnalysisAgent.AnalyzeAsync(incident, sessionContext, agentContext, cancellationToken);
		var analysisText = agentResult.AnalysisText;
		var structuredAnalysis = AgentStructuredAnalysisParser.TryParse(analysisText);
		var usedDeterministicStructuredFallback = structuredAnalysis is null && !agentResult.UsedFallback;
		var confidence = structuredAnalysis?.Confidence ?? ExtractConfidence(analysisText) ?? "Low";
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
			IncidentSummary = structuredAnalysis?.Summary ?? BuildSummary(incident),
			AnalysisText = analysisText,
			AnalysisProvider = usedDeterministicStructuredFallback
				? $"{agentResult.Provider}/deterministic-structured-fallback"
				: agentResult.Provider,
			AnalysisModel = agentResult.Model,
			UsedFallbackAnalysis = agentResult.UsedFallback || usedDeterministicStructuredFallback,
			FallbackReason = agentResult.FallbackReason
				?? (usedDeterministicStructuredFallback
					? "Model returned unstructured or invalid JSON; deterministic structured fields were used."
					: null),
			Evidence = MergeEvidence(BuildEvidence(incident, runbookResult, logResult, metricsResult, similarIncidents), structuredAnalysis?.Evidence),
			Hypotheses = structuredAnalysis?.Hypotheses.Count > 0
				? structuredAnalysis.Hypotheses
				: BuildHypotheses(incident, runbookResult, logResult, metricsResult, similarIncidents),
			RecommendedActions = MergeRecommendedActions(
				BuildRecommendedActions(incident, runbookResult, logResult, metricsResult, similarIncidents),
				structuredAnalysis?.RecommendedActions),
			Confidence = confidence,
			Notes = BuildNotes(
				structuredAnalysis?.Notes
					?? "Agent returned unstructured analysis, so the application used deterministic evidence, hypotheses, and actions.",
				agentResult)
		};

		await _incidentRecordStore.SaveAsync(incident, result, cancellationToken);
		_logger.LogInformation(
			"Completed incident analysis for IncidentId={IncidentId} SessionId={SessionId} Turn={TurnNumber} Confidence={Confidence}.",
			incident.Id,
			result.SessionId,
			result.SessionTurnNumber,
			result.Confidence);

		return result;
	}

	private static string BuildNotes(string notes, IncidentAgentExecutionResult agentResult)
	{
		if (!agentResult.UsedFallback || string.IsNullOrWhiteSpace(agentResult.FallbackReason))
		{
			return notes;
		}

		if (notes.Contains(agentResult.FallbackReason, StringComparison.OrdinalIgnoreCase))
		{
			return notes;
		}

		return $"{notes} Fallback reason: {agentResult.FallbackReason}";
	}

	private static IReadOnlyList<IncidentAnalysisEvidenceItem> MergeEvidence(
		IReadOnlyList<IncidentAnalysisEvidenceItem> deterministicEvidence,
		IReadOnlyList<IncidentAnalysisEvidenceItem>? agentEvidence)
	{
		if (agentEvidence is not { Count: > 0 })
		{
			return deterministicEvidence;
		}

		return deterministicEvidence.Concat(agentEvidence).ToArray();
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
			Query = BuildOperationalQuery(incident),
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
			MetricName = SelectMetricName(incident),
			ServiceName = incident.ServiceName,
			Environment = incident.Environment,
			StartTime = incident.Timestamp?.AddHours(-1),
			EndTime = incident.Timestamp
		};
	}

	private static string BuildOperationalQuery(Incident incident)
	{
		var parts = new List<string> { incident.Title, incident.Description };
		parts.AddRange(incident.Tags);
		return string.Join(' ', parts.Where(part => !string.IsNullOrWhiteSpace(part)));
	}

	private static string SelectMetricName(Incident incident)
	{
		var query = BuildOperationalQuery(incident);
		return ContainsAny(query, "queue", "backlog", "consumer", "worker")
			? "queue_depth"
			: "request_error_rate";
	}

	private static bool ContainsAny(string value, params string[] terms)
	{
		return terms.Any(term => value.Contains(term, StringComparison.OrdinalIgnoreCase));
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
		MetricsQueryResult metricsResult,
		IReadOnlyList<SimilarIncidentMatch> similarIncidents)
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

		foreach (var similar in similarIncidents)
		{
			evidence.Add(new IncidentAnalysisEvidenceItem
			{
				Summary = $"Similar previous incident: {similar.IncidentSummary}",
				Source = $"history.incident.{similar.IncidentId}",
				Details = $"Score {similar.Score:0.00}; {similar.ServiceName}/{similar.Environment}; prior action: {similar.ResolutionSummary}"
			});
		}

		return evidence;
	}

	private static IReadOnlyList<IncidentHypothesis> BuildHypotheses(
		Incident incident,
		RunbookRetrievalResult runbookResult,
		LogSearchResult logResult,
		MetricsQueryResult metricsResult,
		IReadOnlyList<SimilarIncidentMatch> similarIncidents)
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

		var strongestSimilar = similarIncidents.FirstOrDefault();
		if (strongestSimilar is not null)
		{
			hypotheses.Add(new IncidentHypothesis
			{
				Description = $"This incident resembles previous incident '{strongestSimilar.IncidentSummary}'.",
				InferenceStrength = strongestSimilar.Score >= 0.55 ? "Strong" : "Medium",
				Confidence = strongestSimilar.Score >= 0.55 ? "Medium" : "Low",
				SupportingEvidence =
				[
					$"Shared signals: {string.Join(", ", strongestSimilar.SharedSignals.Take(6))}.",
					$"Prior action: {strongestSimilar.ResolutionSummary}."
				],
				EvidenceReferences = [$"history.incident.{strongestSimilar.IncidentId}"]
			});
		}

		return hypotheses;
	}

	private static IReadOnlyList<IncidentActionRecommendation> BuildRecommendedActions(
		Incident incident,
		RunbookRetrievalResult runbookResult,
		LogSearchResult logResult,
		MetricsQueryResult metricsResult,
		IReadOnlyList<SimilarIncidentMatch> similarIncidents)
	{
		var actions = new List<IncidentActionRecommendation>();
		var isQueueIncident = metricsResult.MetricName.Contains("queue", StringComparison.OrdinalIgnoreCase)
			|| incident.Tags.Any(tag => tag.Contains("queue", StringComparison.OrdinalIgnoreCase) || tag.Contains("backlog", StringComparison.OrdinalIgnoreCase));

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

		if (isQueueIncident)
		{
			actions.Add(new IncidentActionRecommendation
			{
				Description = "Check worker pod readiness, restart count, and resource saturation; then compare message arrival rate with consumer throughput.",
				Priority = "High",
				Rationale = "The queue is growing faster than consumers are draining it, so this separates unhealthy workers from insufficient capacity.",
				SupportingSignals = ["tool.logs", "tool.metrics", "rag.runbooks"]
			});
			actions.Add(new IncidentActionRecommendation
			{
				Description = "If workers are healthy but saturated, scale the worker deployment incrementally and watch queue depth until it decreases for two consecutive polling windows.",
				Priority = "High",
				Rationale = "Scaling is appropriate only when consumers are healthy and capacity, rather than a downstream failure, is the constraint.",
				SupportingSignals = ["tool.metrics", "rag.runbooks"]
			});
		}

		if (runbookResult.Runbooks.Count > 0)
		{
			actions.AddRange(BuildRunbookStepActions(runbookResult.Runbooks));
		}

		var similar = similarIncidents.FirstOrDefault();
		if (similar is not null && HasReusableResolution(similar.ResolutionSummary))
		{
			actions.Add(new IncidentActionRecommendation
			{
				Description = BuildSimilarIncidentAction(similar),
				Priority = "High",
				Rationale = $"Automatically matched previous incident '{similar.IncidentSummary}' with score {similar.Score:0.00}.",
				SupportingSignals = [$"history.incident.{similar.IncidentId}"]
			});
		}

		if (logResult.Entries.Count > 0)
		{
			var entry = logResult.Entries
				.OrderByDescending(log => log.Timestamp)
				.First();
			actions.Add(new IncidentActionRecommendation
			{
				Description = $"Use the newest {entry.Source} log signal to validate whether the failure is still active: {entry.Message}.",
				Priority = "High",
				Rationale = "The newest matching log is the freshest operational clue available to the agent.",
				SupportingSignals = ["tool.logs"]
			});
		}

		if (metricsResult.Samples.Count > 0)
		{
			var latest = metricsResult.Samples
				.OrderByDescending(sample => sample.Timestamp)
				.First();
			actions.Add(new IncidentActionRecommendation
			{
				Description = $"Verify the current {metricsResult.MetricName} trend after mitigation; latest sample is {latest.Value:0.##}.",
				Priority = "High",
				Rationale = "Resolution should be checked against the live metric, not only the initial symptom.",
				SupportingSignals = ["tool.metrics"]
			});
		}

		if (actions.Count == 0)
		{
			actions.Add(new IncidentActionRecommendation
			{
				Description = "Collect a current error log and the primary service health metric before choosing a mitigation.",
				Priority = "High",
				Rationale = "No concrete operational evidence or actionable runbook step was available.",
				SupportingSignals = ["incident.description"]
			});
		}

		return actions;
	}

	private static IReadOnlyList<IncidentActionRecommendation> MergeRecommendedActions(
		IReadOnlyList<IncidentActionRecommendation> deterministicActions,
		IReadOnlyList<IncidentActionRecommendation>? agentActions)
	{
		var merged = new List<IncidentActionRecommendation>();
		if (agentActions is { Count: > 0 })
		{
			merged.AddRange(agentActions.Where(action => !IsLowValueFallbackAction(action.Description)));
		}

		merged.AddRange(deterministicActions);

		return merged
			.GroupBy(action => NormalizeAction(action.Description), StringComparer.OrdinalIgnoreCase)
			.Select(group => group.First())
			.Take(10)
			.ToArray();
	}

	private static IReadOnlyList<IncidentActionRecommendation> BuildRunbookStepActions(IReadOnlyList<RunbookDocument> runbooks)
	{
		return runbooks
			.SelectMany(runbook => ExtractActionPhrases(runbook)
				.Where(action => !IsLowValueFallbackAction(action))
				.Select(action => new IncidentActionRecommendation
				{
					Description = action,
					Priority = IsMitigationRunbook(runbook) ? "High" : "Medium",
					Rationale = $"Concrete step extracted from retrieved runbook '{runbook.Title}'.",
					SupportingSignals = [$"rag.runbook.{runbook.Id}"]
				}))
			.Take(4)
			.ToArray();
	}

	private static bool HasReusableResolution(string? resolutionSummary)
	{
		if (string.IsNullOrWhiteSpace(resolutionSummary)) return false;
		var normalized = NormalizeAction(resolutionSummary);
		return normalized.Length >= 18
			&& !normalized.Contains("confirm blast radius review recent changes", StringComparison.Ordinal)
			&& !normalized.Contains("follow the most relevant runbook", StringComparison.Ordinal);
	}

	private static string BuildSimilarIncidentAction(SimilarIncidentMatch similar)
	{
		var priorAction = string.IsNullOrWhiteSpace(similar.ResolutionSummary)
			? "reuse the previously successful mitigation pattern"
			: similar.ResolutionSummary;

		if (priorAction.StartsWith("Worked:", StringComparison.OrdinalIgnoreCase))
		{
			return $"Apply the prior successful mitigation from '{similar.IncidentSummary}': {priorAction["Worked:".Length..].Trim()}";
		}

		return $"Use the prior response pattern from '{similar.IncidentSummary}': {priorAction}";
	}

	private static IEnumerable<string> ExtractActionPhrases(RunbookDocument runbook)
	{
		var lines = runbook.Content
			.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
			.Select(line => line.Trim('-', '*', ' ', '\t'))
			.Select(line => Regex.Replace(line, @"^\d+\.\s*", string.Empty))
			.Where(line => line.Length is >= 12 and <= 150)
			.Where(line => !line.Contains("...", StringComparison.Ordinal) && !line.Contains('…'))
			.Where(line => StartsWithActionVerb(line))
			.Select(line => line.EndsWith('.') ? line : $"{line}.")
			.Distinct(StringComparer.OrdinalIgnoreCase);

		foreach (var line in lines)
		{
			yield return line;
		}
	}

	private static bool StartsWithActionVerb(string value)
	{
		var verbs = new[]
		{
			"Check", "Confirm", "Review", "Identify", "Compare", "Roll back", "Restart", "Scale",
			"Disable", "Enable", "Escalate", "Notify", "Inspect", "Query", "Validate", "Collect"
		};

		return verbs.Any(verb => value.StartsWith(verb, StringComparison.OrdinalIgnoreCase));
	}

	private static bool IsMitigationRunbook(RunbookDocument runbook)
	{
		return runbook.Title.Contains("Mitigation", StringComparison.OrdinalIgnoreCase)
			|| runbook.Content.Contains("roll back", StringComparison.OrdinalIgnoreCase)
			|| runbook.Content.Contains("restart", StringComparison.OrdinalIgnoreCase);
	}

	private static string NormalizeAction(string description)
	{
		return Regex.Replace(description.ToLowerInvariant(), "[^a-z0-9]+", " ").Trim();
	}

	private static bool IsLowValueFallbackAction(string description)
	{
		var normalized = NormalizeAction(description);
		return normalized is "confirm blast radius review recent changes and follow the most relevant runbook"
			or "confirm system is stable"
			or "prioritize investigation of the most affected service path first"
			|| normalized.StartsWith("compare against previous similar incident", StringComparison.Ordinal)
			|| normalized.StartsWith("use the prior response pattern", StringComparison.Ordinal);
	}
}
