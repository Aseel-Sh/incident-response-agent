using IncidentResponseAgent.Application.Incidents;
using IncidentResponseAgent.Application.Runbooks;
using IncidentResponseAgent.Application.Tools;
using IncidentResponseAgent.Domain.Incidents;
using IncidentResponseAgent.Domain.Runbooks;
using System.Text.Json;

namespace IncidentResponseAgent.Agent.Incidents;

public sealed class PromptBasedIncidentAnalysisAgent : IIncidentAnalysisAgent
{
	private readonly IIncidentAnalysisAgentFactory _agentFactory;
	private readonly IncidentAnalysisAgentInstructions _instructions;
	private readonly ILogSearchProvider _logSearchProvider;
	private readonly IMetricsProvider _metricsProvider;
	private readonly IRunbookRetrievalService _runbookRetrievalService;

	public PromptBasedIncidentAnalysisAgent(
		IIncidentAnalysisAgentFactory agentFactory,
		ILogSearchProvider logSearchProvider,
		IMetricsProvider metricsProvider,
		IRunbookRetrievalService runbookRetrievalService)
	{
		_agentFactory = agentFactory;
		_instructions = new IncidentAnalysisAgentInstructions();
		_logSearchProvider = logSearchProvider;
		_metricsProvider = metricsProvider;
		_runbookRetrievalService = runbookRetrievalService;
	}

	public async Task<string> AnalyzeAsync(Incident incident, IncidentAnalysisSessionContext? sessionContext = null, CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(incident);

		var profile = _agentFactory.Create();
		var runbookRequest = BuildRunbookRetrievalRequest(incident);
		var logRequest = BuildLogSearchRequest(incident);
		var metricsRequest = BuildMetricsQueryRequest(incident);

		var runbookResult = await _runbookRetrievalService.RetrieveAsync(runbookRequest, cancellationToken);
		var logResult = await _logSearchProvider.SearchAsync(logRequest, cancellationToken);
		var metricsResult = await _metricsProvider.QueryAsync(metricsRequest, cancellationToken);
		var prompt = _instructions.BuildPrompt(
			incident,
			profile,
			sessionContext,
			runbookResult.Runbooks,
			BuildLogHighlights(logResult),
			BuildMetricHighlights(metricsResult));
		var response = BuildAnalysisText(incident, profile, prompt, sessionContext, runbookResult.Runbooks, logResult, metricsResult);

		return response;
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
		MetricsQueryResult metricsResult)
	{
		var serviceName = string.IsNullOrWhiteSpace(incident.ServiceName) ? "the impacted service" : incident.ServiceName;
		var promptLength = prompt.Length;
		var logCount = logResult.Entries.Count;
		var metricCount = metricsResult.Samples.Count;
		var runbookCount = runbooks.Count;
		var primaryRunbook = runbooks.FirstOrDefault()?.Title ?? "none";
		var primaryLogMessage = logResult.Entries.FirstOrDefault()?.Message ?? "none";
		var primaryMetric = metricsResult.Samples.FirstOrDefault()?.Value;
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
					Summary = $"{logCount} log entries, {metricCount} metric samples, and {runbookCount} runbook chunks were gathered.",
					Source = "agent.local",
					Details = $"{sessionLine} Primary runbook: {primaryRunbook}. Primary log: {primaryLogMessage}. Primary metric: {metricText}."
				}
			],
			Hypotheses =
			[
				new IncidentHypothesis
				{
					Description = $"Recent service or dependency change may be affecting {serviceName}.",
					InferenceStrength = "Medium",
					Confidence = "Low",
					SupportingEvidence = [$"Prompt size: {promptLength} characters.", $"Primary runbook: {primaryRunbook}."],
					EvidenceReferences = ["agent.local", "rag.runbooks", "tool.logs", "tool.metrics"]
				}
			],
			RecommendedActions =
			[
				new IncidentActionRecommendation
				{
					Description = "Confirm blast radius, review recent changes, and follow the most relevant runbook.",
					Priority = "High",
					Rationale = "The local fallback can summarize gathered signals but cannot perform model reasoning.",
					SupportingSignals = ["rag.runbooks", "tool.logs", "tool.metrics"]
				}
			],
			Confidence = "Low",
			Notes = "Local prompt-based fallback used because no agent API key is configured."
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
