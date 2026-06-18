using IncidentResponseAgent.Application.Incidents;
using IncidentResponseAgent.Application.Runbooks;
using IncidentResponseAgent.Application.Tools;
using IncidentResponseAgent.Domain.Incidents;
using IncidentResponseAgent.Domain.Runbooks;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace IncidentResponseAgent.Agent.Incidents;

public sealed class PromptBasedIncidentAnalysisAgent : IIncidentAnalysisAgent
{
	private readonly IIncidentAnalysisAgentFactory _agentFactory;
	private readonly IncidentAnalysisAgentInstructions _instructions;
	private readonly ILogSearchProvider _logSearchProvider;
	private readonly ILogger<PromptBasedIncidentAnalysisAgent> _logger;
	private readonly IMetricsProvider _metricsProvider;
	private readonly IRunbookRetrievalService _runbookRetrievalService;

	public PromptBasedIncidentAnalysisAgent(
		IIncidentAnalysisAgentFactory agentFactory,
		ILogSearchProvider logSearchProvider,
		IMetricsProvider metricsProvider,
		IRunbookRetrievalService runbookRetrievalService,
		ILogger<PromptBasedIncidentAnalysisAgent> logger)
	{
		_agentFactory = agentFactory;
		_instructions = new IncidentAnalysisAgentInstructions();
		_logSearchProvider = logSearchProvider;
		_logger = logger;
		_metricsProvider = metricsProvider;
		_runbookRetrievalService = runbookRetrievalService;
	}

	public async Task<IncidentAgentExecutionResult> AnalyzeAsync(
		Incident incident,
		IncidentAnalysisSessionContext? sessionContext = null,
		IncidentAnalysisAgentContext? agentContext = null,
		CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(incident);

		var profile = _agentFactory.Create();
		_logger.LogInformation("Running local prompt-based incident analysis fallback for IncidentId={IncidentId}.", incident.Id);
		var runbookResult = agentContext?.Runbooks ?? await _runbookRetrievalService.RetrieveAsync(BuildRunbookRetrievalRequest(incident), cancellationToken);
		var logResult = agentContext?.Logs ?? await _logSearchProvider.SearchAsync(BuildLogSearchRequest(incident), cancellationToken);
		var metricsResult = agentContext?.Metrics ?? await _metricsProvider.QueryAsync(BuildMetricsQueryRequest(incident), cancellationToken);
		var prompt = _instructions.BuildPrompt(
			incident,
			profile,
			sessionContext,
			runbookResult.Runbooks,
			BuildLogHighlights(logResult),
			BuildMetricHighlights(metricsResult),
			agentContext?.SimilarIncidents ?? Array.Empty<SimilarIncidentMatch>());
		var response = BuildAnalysisText(
			incident,
			profile,
			prompt,
			sessionContext,
			runbookResult.Runbooks,
			logResult,
			metricsResult,
			agentContext?.SimilarIncidents ?? Array.Empty<SimilarIncidentMatch>());
		_logger.LogInformation("Local prompt-based incident analysis completed for IncidentId={IncidentId}.", incident.Id);

		return new IncidentAgentExecutionResult
		{
			AnalysisText = response,
			Provider = "local-prompt",
			Model = "local",
			UsedFallback = true,
			FallbackReason = "No model-backed analysis was used."
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

	private static RunbookRetrievalRequest BuildRunbookRetrievalRequest(Incident incident)
	{
		return new RunbookRetrievalRequest
		{
			Query = incident.Title,
			ServiceName = incident.ServiceName,
			Environment = incident.Environment,
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

	private static string BuildAnalysisText(
		Incident incident,
		IncidentAnalysisAgentProfile profile,
		string prompt,
		IncidentAnalysisSessionContext? sessionContext,
		IReadOnlyCollection<RunbookDocument> runbooks,
		LogSearchResult logResult,
		MetricsQueryResult metricsResult,
		IReadOnlyList<SimilarIncidentMatch> similarIncidents)
	{
		var serviceName = string.IsNullOrWhiteSpace(incident.ServiceName) ? "the impacted service" : incident.ServiceName;
		var promptLength = prompt.Length;
		var logCount = logResult.Entries.Count;
		var metricCount = metricsResult.Samples.Count;
		var runbookCount = runbooks.Count;
		var similarIncidentCount = similarIncidents.Count;
		var primaryRunbook = runbooks.FirstOrDefault()?.Title ?? "none";
		var primaryLogMessage = logResult.Entries.FirstOrDefault()?.Message ?? "none";
		var primaryMetric = metricsResult.Samples.FirstOrDefault()?.Value;
		var primarySimilar = similarIncidents.FirstOrDefault();
		var sessionLine = sessionContext is null
			? "Session: new investigation"
			: $"Session: {sessionContext.SessionId} turn {sessionContext.TurnNumber + 1} (previous turn {sessionContext.TurnNumber}).";

		var metricText = primaryMetric is null ? "none" : primaryMetric.Value.ToString("0.##");

		var structured = new IncidentAnalysisResult
		{
			IncidentId = incident.Id,
			IncidentSummary = $"Investigate {serviceName}: {incident.Title}.",
			AnalysisText = string.Empty,
			Evidence =
			[
				new IncidentAnalysisEvidenceItem
				{
					Summary = $"{logCount} log entries, {metricCount} metric samples, {runbookCount} runbook chunks, and {similarIncidentCount} similar incidents were gathered.",
					Source = "agent.local",
					Details = $"{sessionLine} Primary runbook: {primaryRunbook}. Primary log: {primaryLogMessage}. Primary metric: {metricText}. Similar incident: {primarySimilar?.IncidentSummary ?? "none"}."
				}
			],
			Hypotheses =
			[
				new IncidentHypothesis
				{
					Description = $"Recent service or dependency change may be affecting {serviceName}.",
					InferenceStrength = "Medium",
					Confidence = "Low",
					SupportingEvidence = [$"Prompt size: {promptLength} characters.", $"Primary runbook: {primaryRunbook}.", $"Similar incidents: {similarIncidentCount}."],
					EvidenceReferences = ["agent.local", "rag.runbooks", "tool.logs", "tool.metrics", "history.incidents"]
				}
			],
			RecommendedActions = BuildLocalRecommendations(runbooks, logResult, metricsResult, similarIncidents),
			Confidence = "Low",
			Notes = "Local prompt-based fallback produced this analysis."
		};

		return JsonSerializer.Serialize(new
		{
			summary = structured.IncidentSummary,
			evidence = structured.Evidence,
			hypotheses = structured.Hypotheses,
			recommendedActions = structured.RecommendedActions,
			confidence = structured.Confidence,
			notes = structured.Notes
		}, new JsonSerializerOptions(JsonSerializerDefaults.Web));
	}

	private static IReadOnlyList<IncidentActionRecommendation> BuildLocalRecommendations(
		IReadOnlyCollection<RunbookDocument> runbooks,
		LogSearchResult logResult,
		MetricsQueryResult metricsResult,
		IReadOnlyList<SimilarIncidentMatch> similarIncidents)
	{
		var actions = new List<IncidentActionRecommendation>();

		foreach (var runbook in runbooks.Take(2))
		{
			foreach (var step in ExtractRunbookSteps(runbook).Where(IsConcreteAction).Take(2))
			{
				actions.Add(new IncidentActionRecommendation
				{
					Description = step,
					Priority = "High",
					Rationale = $"Retrieved from matching runbook '{runbook.Title}'.",
					SupportingSignals = [$"rag.runbook.{runbook.Id}"]
				});
			}
		}

		var latestLog = logResult.Entries.OrderByDescending(entry => entry.Timestamp).FirstOrDefault();
		if (latestLog is not null)
		{
			actions.Add(new IncidentActionRecommendation
			{
				Description = $"Validate whether the latest {latestLog.Source} log signal is still active: {latestLog.Message}.",
				Priority = "High",
				Rationale = "The local fallback found a concrete matching log signal.",
				SupportingSignals = ["tool.logs"]
			});
		}

		var latestMetric = metricsResult.Samples.OrderByDescending(sample => sample.Timestamp).FirstOrDefault();
		if (latestMetric is not null)
		{
			actions.Add(new IncidentActionRecommendation
			{
				Description = $"Re-check {metricsResult.MetricName} after each mitigation; latest sample is {latestMetric.Value:0.##}.",
				Priority = "High",
				Rationale = "Mitigation should be verified against the metric that triggered or supports the incident.",
				SupportingSignals = ["tool.metrics"]
			});
		}

		var similar = similarIncidents.FirstOrDefault();
		if (similar is not null && HasReusableResolution(similar.ResolutionSummary))
		{
			actions.Add(new IncidentActionRecommendation
			{
				Description = BuildSimilarIncidentAction(similar),
				Priority = "High",
				Rationale = $"Automatically matched a previous incident with score {similar.Score:0.00}.",
				SupportingSignals = [$"history.incident.{similar.IncidentId}"]
			});
		}

		if (actions.Count == 0)
		{
			actions.Add(new IncidentActionRecommendation
			{
				Description = "Gather at least one current log or metric signal before choosing mitigation.",
				Priority = "High",
				Rationale = "No concrete operational signals were available to the local fallback.",
				SupportingSignals = ["incident.description"]
			});
		}

		return actions.Take(6).ToArray();
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

	private static bool HasReusableResolution(string? resolutionSummary)
	{
		if (string.IsNullOrWhiteSpace(resolutionSummary)) return false;
		var normalized = resolutionSummary.ToLowerInvariant();
		return normalized.Length >= 18
			&& !normalized.Contains("confirm blast radius", StringComparison.Ordinal)
			&& !normalized.Contains("follow the most relevant runbook", StringComparison.Ordinal);
	}

	private static bool IsConcreteAction(string action)
	{
		var normalized = action.Trim().TrimEnd('.').ToLowerInvariant();
		return normalized is not "confirm system is stable"
			&& !normalized.StartsWith("compare against previous similar incident", StringComparison.Ordinal);
	}

	private static IEnumerable<string> ExtractRunbookSteps(RunbookDocument runbook)
	{
		var verbs = new[] { "Check", "Confirm", "Review", "Identify", "Compare", "Roll back", "Restart", "Scale", "Disable", "Enable", "Escalate", "Inspect", "Query", "Validate", "Collect" };
		return runbook.Content
			.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
			.Select(line => line.Trim('-', '*', '#', ' ', '\t'))
			.Select(line => StripNumberPrefix(line))
			.Where(line => line.Length is >= 12 and <= 150)
			.Where(line => !line.Contains("...", StringComparison.Ordinal) && !line.Contains('…'))
			.Where(line => verbs.Any(verb => line.StartsWith(verb, StringComparison.OrdinalIgnoreCase)))
			.Select(line => line.EndsWith('.') ? line : $"{line}.")
			.Distinct(StringComparer.OrdinalIgnoreCase);
	}

	private static string StripNumberPrefix(string value)
	{
		var index = 0;
		while (index < value.Length && char.IsDigit(value[index]))
		{
			index++;
		}

		return index < value.Length && value[index] == '.'
			? value[(index + 1)..].Trim()
			: value;
	}

	private static IReadOnlyList<string> BuildLogHighlights(LogSearchResult logResult)
	{
		return logResult.Entries
			.Select(entry => $"[{entry.Level}] {entry.Source}: {entry.Message}")
			.Take(3)
			.ToArray();
	}

	private static IReadOnlyList<string> BuildMetricHighlights(MetricsQueryResult metricsResult)
	{
		return metricsResult.Samples
			.Take(3)
			.Select(sample => $"{sample.Timestamp:O} -> {sample.Value}")
			.ToArray();
	}
}
