using IncidentResponseAgent.Application.Incidents;
using IncidentResponseAgent.Application.Runbooks;
using IncidentResponseAgent.Application.Tools;
using IncidentResponseAgent.Domain.Incidents;
using IncidentResponseAgent.Domain.Runbooks;

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

	public async Task<string> AnalyzeAsync(Incident incident, CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(incident);

		var profile = _agentFactory.Create();
		var runbookRequest = BuildRunbookRetrievalRequest(incident);
		var logRequest = BuildLogSearchRequest(incident);
		var metricsRequest = BuildMetricsQueryRequest(incident);

		var runbookResult = await _runbookRetrievalService.RetrieveAsync(runbookRequest, cancellationToken);
		var logResult = await _logSearchProvider.SearchAsync(logRequest, cancellationToken);
		var metricsResult = await _metricsProvider.QueryAsync(metricsRequest, cancellationToken);
		var prompt = _instructions.BuildPrompt(incident, profile, runbookResult.Runbooks);
		var response = BuildAnalysisText(incident, profile, prompt, logResult, metricsResult);

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
		LogSearchResult logResult,
		MetricsQueryResult metricsResult)
	{
		var serviceName = string.IsNullOrWhiteSpace(incident.ServiceName) ? "the impacted service" : incident.ServiceName;
		var promptLength = prompt.Length;
		var logCount = logResult.Entries.Count;
		var metricCount = metricsResult.Samples.Count;
		var runbookCount = CountRunbooksInPrompt(prompt);

		return $"[{profile.Name}] Prompt-based analysis for {serviceName}: start with log review, confirm recent changes, and validate the incident scope. Prompt size: {promptLength} characters. Runbooks retrieved: {runbookCount}. Log evidence: {logCount} entries. Metric samples: {metricCount}.";
	}

	private static int CountRunbooksInPrompt(string prompt)
	{
		return prompt.Split("Runbook ", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Length - 1;
	}
}