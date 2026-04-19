using System.ComponentModel;
using IncidentResponseAgent.Application.Runbooks;
using IncidentResponseAgent.Application.Tools;

namespace IncidentResponseAgent.Agent.Incidents;

public sealed class IncidentAnalysisAgentTools
{
	private readonly ILogSearchProvider _logSearchProvider;
	private readonly IMetricsProvider _metricsProvider;
	private readonly IRunbookRetrievalService _runbookRetrievalService;

	public IncidentAnalysisAgentTools(
		ILogSearchProvider logSearchProvider,
		IMetricsProvider metricsProvider,
		IRunbookRetrievalService runbookRetrievalService)
	{
		_logSearchProvider = logSearchProvider;
		_metricsProvider = metricsProvider;
		_runbookRetrievalService = runbookRetrievalService;
	}

	[Description("Search incident-related log entries for the affected service and time window.")]
	public Task<LogSearchResult> SearchLogsAsync(
		[Description("Search text or incident title.")] string query,
		[Description("The affected service name.")] string? serviceName,
		[Description("The environment name, such as production or staging.")] string? environment,
		[Description("The beginning of the log search time window.")] DateTimeOffset? startTime,
		[Description("The end of the log search time window.")] DateTimeOffset? endTime,
		[Description("Maximum number of log entries to return.")] int maxResults = 3,
		CancellationToken cancellationToken = default)
	{
		var request = new LogSearchRequest
		{
			Query = query,
			ServiceName = serviceName,
			Environment = environment,
			StartTime = startTime,
			EndTime = endTime,
			MaxResults = maxResults
		};

		return _logSearchProvider.SearchAsync(request, cancellationToken);
	}

	[Description("Query relevant metric samples for the incident investigation.")]
	public Task<MetricsQueryResult> QueryMetricsAsync(
		[Description("Name of the metric to query.")] string metricName,
		[Description("The affected service name.")] string? serviceName,
		[Description("The environment name, such as production or staging.")] string? environment,
		[Description("The beginning of the metric time window.")] DateTimeOffset? startTime,
		[Description("The end of the metric time window.")] DateTimeOffset? endTime,
		CancellationToken cancellationToken = default)
	{
		var request = new MetricsQueryRequest
		{
			MetricName = metricName,
			ServiceName = serviceName,
			Environment = environment,
			StartTime = startTime,
			EndTime = endTime
		};

		return _metricsProvider.QueryAsync(request, cancellationToken);
	}

	[Description("Retrieve relevant runbooks for the incident investigation.")]
	public Task<RunbookRetrievalResult> RetrieveRunbooksAsync(
		[Description("Search text or incident title.")] string query,
		[Description("The affected service name.")] string? serviceName,
		[Description("The environment name, such as production or staging.")] string? environment,
		[Description("Maximum number of runbooks to return.")] int maxResults = 3,
		CancellationToken cancellationToken = default)
	{
		var request = new RunbookRetrievalRequest
		{
			Query = query,
			ServiceName = serviceName,
			Environment = environment,
			MaxResults = maxResults
		};

		return _runbookRetrievalService.RetrieveAsync(request, cancellationToken);
	}
}