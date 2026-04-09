using IncidentResponseAgent.Application.Incidents;
using IncidentResponseAgent.Application.Tools;
using IncidentResponseAgent.Domain.Incidents;

namespace IncidentResponseAgent.Agent.Incidents;

public sealed class PromptBasedIncidentAnalysisAgent : IIncidentAnalysisAgent
{
	private readonly IIncidentAnalysisAgentFactory _agentFactory;
	private readonly IncidentAnalysisAgentInstructions _instructions;
	private readonly ILogSearchProvider _logSearchProvider;
	private readonly IMetricsProvider _metricsProvider;

	public PromptBasedIncidentAnalysisAgent(
		IIncidentAnalysisAgentFactory agentFactory,
		ILogSearchProvider logSearchProvider,
		IMetricsProvider metricsProvider)
	{
		_agentFactory = agentFactory;
		_instructions = new IncidentAnalysisAgentInstructions();
		_logSearchProvider = logSearchProvider;
		_metricsProvider = metricsProvider;
	}

	public async Task<string> AnalyzeAsync(Incident incident, CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(incident);

		var profile = _agentFactory.Create();
		var prompt = _instructions.BuildPrompt(incident, profile);
		var logRequest = BuildLogSearchRequest(incident);
		var metricsRequest = BuildMetricsQueryRequest(incident);

		var logResult = await _logSearchProvider.SearchAsync(logRequest, cancellationToken);
		var metricsResult = await _metricsProvider.QueryAsync(metricsRequest, cancellationToken);
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

		return $"[{profile.Name}] Prompt-based analysis for {serviceName}: start with log review, confirm recent changes, and validate the incident scope. Prompt size: {promptLength} characters. Log evidence: {logCount} entries. Metric samples: {metricCount}.";
	}
}