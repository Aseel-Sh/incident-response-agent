using System.Net;
using IncidentResponseAgent.Agent.Incidents;
using IncidentResponseAgent.Application.Incidents;
using IncidentResponseAgent.Application.Runbooks;
using IncidentResponseAgent.Application.Tools;
using IncidentResponseAgent.Domain.Incidents;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace IncidentResponseAgent.Tests;

public sealed class ResilientIncidentAnalysisAgentTests
{
	[Fact]
	public async Task SuccessfulModelPathDoesNotInvokeLocalFallback()
	{
		var model = new StubModelAgent(Result("openai-compatible", false));
		var fallback = new StubFallbackAgent(Result("local-prompt", true));
		var agent = CreateAgent(model, fallback);

		var result = await agent.AnalyzeAsync(CreateIncident());

		Assert.Equal("openai-compatible", result.Provider);
		Assert.False(result.UsedFallback);
		Assert.Equal(0, fallback.CallCount);
	}

	[Fact]
	public async Task ModelUnavailableTriggersHonestLocalFallback()
	{
		var model = new StubModelAgent(new HttpRequestException("provider returned 503 Service Unavailable", null, HttpStatusCode.ServiceUnavailable));
		var fallback = new StubFallbackAgent(Result("local-prompt", true));
		var agent = CreateAgent(model, fallback);

		var result = await agent.AnalyzeAsync(CreateIncident());

		Assert.Equal("local-prompt", result.Provider);
		Assert.True(result.UsedFallback);
		Assert.Contains("503", result.FallbackReason);
		Assert.Equal(1, fallback.CallCount);
	}

	[Fact]
	public async Task LocalFallbackRejectsAnalysisWithoutOperationalEvidence()
	{
		var fallback = new PromptBasedIncidentAnalysisAgent(
			new StubAgentFactory(), new UnusedLogProvider(), new UnusedMetricsProvider(), new UnusedRunbookProvider(), NullLogger<PromptBasedIncidentAnalysisAgent>.Instance);
		var emptyContext = new IncidentAnalysisAgentContext
		{
			Runbooks = new RunbookRetrievalResult(), Logs = new LogSearchResult(), Metrics = new MetricsQueryResult { MetricName = "request_error_rate" }
		};

		var exception = await Assert.ThrowsAsync<IncidentAnalysisUnavailableException>(() => fallback.AnalyzeAsync(CreateIncident(), agentContext: emptyContext));

		Assert.Contains("insufficient operational evidence", exception.Message);
	}

	private static ResilientIncidentAnalysisAgent CreateAgent(IModelIncidentAnalysisAgent model, ILocalFallbackIncidentAnalysisAgent fallback) => new(
		model,
		fallback,
		Options.Create(new IncidentAnalysisAgentOptions { Provider = "OpenRouter", Model = "test/model", Endpoint = "https://openrouter.ai/api/v1", ApiKey = "configured", AnalysisTimeoutSeconds = 5 }),
		NullLogger<ResilientIncidentAnalysisAgent>.Instance);

	private static Incident CreateIncident() => new(Guid.NewGuid(), "Provider test", "Provider test incident.", IncidentSeverity.Sev3);

	private static IncidentAgentExecutionResult Result(string provider, bool fallback) => new()
	{
		AnalysisText = """{"summary":"test","severity":"SEV-3","evidence":[],"hypotheses":[],"recommendedActions":[],"confidence":"Low","notes":"test"}""",
		Provider = provider,
		Model = fallback ? "local" : "test/model",
		UsedFallback = fallback
	};

	private sealed class StubModelAgent : IModelIncidentAnalysisAgent
	{
		private readonly IncidentAgentExecutionResult? _result;
		private readonly Exception? _exception;
		public StubModelAgent(IncidentAgentExecutionResult result) => _result = result;
		public StubModelAgent(Exception exception) => _exception = exception;
		public Task<IncidentAgentExecutionResult> AnalyzeAsync(Incident incident, IncidentAnalysisSessionContext? sessionContext = null, IncidentAnalysisAgentContext? agentContext = null, CancellationToken cancellationToken = default) =>
			_exception is null ? Task.FromResult(_result!) : Task.FromException<IncidentAgentExecutionResult>(_exception);
	}

	private sealed class StubFallbackAgent(IncidentAgentExecutionResult result) : ILocalFallbackIncidentAnalysisAgent
	{
		public int CallCount { get; private set; }
		public Task<IncidentAgentExecutionResult> AnalyzeAsync(Incident incident, IncidentAnalysisSessionContext? sessionContext = null, IncidentAnalysisAgentContext? agentContext = null, CancellationToken cancellationToken = default)
		{
			CallCount++;
			return Task.FromResult(result);
		}
	}

	private sealed class StubAgentFactory : IIncidentAnalysisAgentFactory
	{
		public IncidentAnalysisAgentProfile Create() => new() { Name = "test", Provider = "local", Model = "local" };
	}
	private sealed class UnusedLogProvider : ILogSearchProvider { public Task<LogSearchResult> SearchAsync(LogSearchRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException(); }
	private sealed class UnusedMetricsProvider : IMetricsProvider { public Task<MetricsQueryResult> QueryAsync(MetricsQueryRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException(); }
	private sealed class UnusedRunbookProvider : IRunbookRetrievalService { public Task<RunbookRetrievalResult> RetrieveAsync(RunbookRetrievalRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException(); }
}
