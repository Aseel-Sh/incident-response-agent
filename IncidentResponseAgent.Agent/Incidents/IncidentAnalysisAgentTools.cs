using System.ComponentModel;
using IncidentResponseAgent.Application.Runbooks;
using IncidentResponseAgent.Application.Tools;
using IncidentResponseAgent.Application.Incidents;
using IncidentResponseAgent.Domain.Incidents;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System.Diagnostics;

namespace IncidentResponseAgent.Agent.Incidents;

public sealed class IncidentAnalysisAgentTools
{
	private readonly ILogSearchProvider _logSearchProvider;
	private readonly IMetricsProvider _metricsProvider;
	private readonly IRunbookRetrievalService _runbookRetrievalService;
	private readonly IIncidentRecordStore? _incidentRecordStore;
	private readonly ILogger<IncidentAnalysisAgentTools> _logger;

	public IncidentAnalysisAgentTools(
		ILogSearchProvider logSearchProvider,
		IMetricsProvider metricsProvider,
		IRunbookRetrievalService runbookRetrievalService,
		IIncidentRecordStore? incidentRecordStore = null,
		ILogger<IncidentAnalysisAgentTools>? logger = null)
	{
		_logSearchProvider = logSearchProvider;
		_metricsProvider = metricsProvider;
		_runbookRetrievalService = runbookRetrievalService;
		_incidentRecordStore = incidentRecordStore;
		_logger = logger ?? NullLogger<IncidentAnalysisAgentTools>.Instance;
	}

	public IReadOnlyList<AITool> CreateFrameworkTools() =>
	[
		AIFunctionFactory.Create(SearchLogsAsync),
		AIFunctionFactory.Create(QueryMetricsAsync),
		AIFunctionFactory.Create(RetrieveRunbooksAsync),
		AIFunctionFactory.Create(RetrievePriorIncidentsAsync),
		AIFunctionFactory.Create(RetrievePriorActionOutcomesAsync),
		AIFunctionFactory.Create(CheckSimilarIncidentsAsync),
		AIFunctionFactory.Create(DraftProposedKnowledgeUpdate)
	];

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

		return TimeToolAsync("SearchLogs", () => _logSearchProvider.SearchAsync(request, cancellationToken));
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

		return TimeToolAsync("QueryMetrics", () => _metricsProvider.QueryAsync(request, cancellationToken));
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

		return TimeToolAsync("RetrieveRunbooks", () => _runbookRetrievalService.RetrieveAsync(request, cancellationToken));
	}

	[Description("Retrieve resolved prior incidents whose knowledge update was approved by a human.")]
	public async Task<IReadOnlyList<TrustedPriorIncident>> RetrievePriorIncidentsAsync(
		[Description("The affected service name.")] string? serviceName,
		[Description("The environment name.")] string? environment,
		[Description("Maximum number of trusted incidents to return.")] int maxResults = 5,
		CancellationToken cancellationToken = default)
	{
		if (_incidentRecordStore is null) return Array.Empty<TrustedPriorIncident>();
		return await TimeToolAsync<IReadOnlyList<TrustedPriorIncident>>("RetrievePriorIncidents", async () =>
		{
			var records = await _incidentRecordStore.GetRecentAsync(Math.Clamp(maxResults * 4, 5, 100), cancellationToken);
			return records
				.Where(record => record.Status == "resolved" && record.ProposedKnowledgeUpdate?.Status == "approved")
				.Where(record => string.IsNullOrWhiteSpace(serviceName) || string.Equals(record.Incident.ServiceName, serviceName, StringComparison.OrdinalIgnoreCase))
				.Where(record => string.IsNullOrWhiteSpace(environment) || string.Equals(record.Incident.Environment, environment, StringComparison.OrdinalIgnoreCase))
				.Take(Math.Clamp(maxResults, 1, 20))
				.Select(record => new TrustedPriorIncident(record.Incident.Id, record.Incident.Title, record.Incident.ServiceName, record.Incident.Environment, record.AnalysisResult.Notes))
				.ToArray();
		});
	}

	[Description("Retrieve worked, partial, and failed action outcomes only from human-approved resolved incidents.")]
	public async Task<IReadOnlyList<TrustedActionOutcome>> RetrievePriorActionOutcomesAsync(
		[Description("The affected service name.")] string? serviceName,
		[Description("Maximum number of outcomes to return.")] int maxResults = 10,
		CancellationToken cancellationToken = default)
	{
		if (_incidentRecordStore is null) return Array.Empty<TrustedActionOutcome>();
		return await TimeToolAsync<IReadOnlyList<TrustedActionOutcome>>("RetrievePriorActionOutcomes", async () =>
		{
			var records = await _incidentRecordStore.GetRecentAsync(100, cancellationToken);
			return records
				.Where(record => record.Status == "resolved" && record.ProposedKnowledgeUpdate?.Status == "approved")
				.Where(record => string.IsNullOrWhiteSpace(serviceName) || string.Equals(record.Incident.ServiceName, serviceName, StringComparison.OrdinalIgnoreCase))
				.SelectMany(record => record.AnalysisResult.ActionOutcomes.Select(outcome => new TrustedActionOutcome(record.Incident.Id, outcome.Description, outcome.Status, outcome.EvidenceReference)))
				.Take(Math.Clamp(maxResults, 1, 50))
				.ToArray();
		});
	}

	[Description("Find trusted similar incidents. False positives, ignored candidates, deleted incidents, and unapproved knowledge are excluded.")]
	public Task<IReadOnlyList<SimilarIncidentMatch>> CheckSimilarIncidentsAsync(
		string title,
		string description,
		string severity,
		string? serviceName,
		string? environment,
		IReadOnlyList<string>? tags,
		int maxResults = 3,
		CancellationToken cancellationToken = default)
	{
		if (_incidentRecordStore is null) return Task.FromResult<IReadOnlyList<SimilarIncidentMatch>>(Array.Empty<SimilarIncidentMatch>());
		if (!Enum.TryParse<IncidentSeverity>(severity.Replace("-", string.Empty), true, out var parsedSeverity)) parsedSeverity = IncidentSeverity.Sev3;
		var incident = new Incident(Guid.NewGuid(), title, description, parsedSeverity, serviceName, environment, DateTimeOffset.UtcNow, tags ?? Array.Empty<string>());
		return TimeToolAsync("CheckSimilarIncidents", () => _incidentRecordStore.FindSimilarAsync(incident, maxResults, cancellationToken));
	}

	[Description("Draft a proposed runbook or postmortem update. The result is a proposal only and requires human approval before reuse.")]
	public string DraftProposedKnowledgeUpdate(
		string title,
		string severity,
		string serviceName,
		string environment,
		IReadOnlyList<string> evidence,
		IReadOnlyList<string> actionOutcomes,
		IReadOnlyList<string> futureSteps) =>
		TimeTool("DraftProposedKnowledgeUpdate", () => string.Join(Environment.NewLine,
			new[] { $"# {title}", "", "## Incident context", $"- Severity: {severity}", $"- Service: {serviceName}", $"- Environment: {environment}", "", "## Evidence" }
				.Concat(evidence.Select(item => $"- {item}"))
				.Concat(["", "## Action outcomes"])
				.Concat(actionOutcomes.Select(item => $"- {item}"))
				.Concat(["", "## Recommended future steps"])
				.Concat(futureSteps.Select(item => $"- {item}"))));

	private async Task<T> TimeToolAsync<T>(string toolName, Func<Task<T>> action)
	{
		var stopwatch = Stopwatch.StartNew();
		try { return await action().ConfigureAwait(false); }
		finally
		{
			stopwatch.Stop();
			_logger.LogInformation("Agent tool execution completed. Tool={ToolName} DurationMs={DurationMs}.", toolName, stopwatch.ElapsedMilliseconds);
		}
	}

	private T TimeTool<T>(string toolName, Func<T> action)
	{
		var stopwatch = Stopwatch.StartNew();
		try { return action(); }
		finally
		{
			stopwatch.Stop();
			_logger.LogInformation("Agent tool execution completed. Tool={ToolName} DurationMs={DurationMs}.", toolName, stopwatch.ElapsedMilliseconds);
		}
	}
}

public sealed record TrustedPriorIncident(Guid IncidentId, string Title, string? ServiceName, string? Environment, string? Notes);

public sealed record TrustedActionOutcome(Guid IncidentId, string Description, string Status, string? EvidenceReference);
