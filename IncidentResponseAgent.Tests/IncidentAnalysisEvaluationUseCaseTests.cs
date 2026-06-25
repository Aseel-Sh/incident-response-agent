using IncidentResponseAgent.Application.Evaluation;
using IncidentResponseAgent.Agent.Incidents;
using IncidentResponseAgent.Application.Incidents;
using IncidentResponseAgent.Application.Runbooks;
using IncidentResponseAgent.Application.Tools;
using IncidentResponseAgent.Domain.Incidents;
using IncidentResponseAgent.Domain.Runbooks;
using Microsoft.Extensions.Logging.Abstractions;

namespace IncidentResponseAgent.Tests;

public sealed class IncidentAnalysisEvaluationUseCaseTests
{
	[Fact]
	public async Task RagFailureDoesNotForceModelFallback()
	{
		var incident = IncidentAnalysisEvaluationScenarioCatalog.BuiltInScenarios[0].Incident;
		var useCase = new AnalyzeIncidentUseCase(
			new GroundedModelAgent(), new InMemorySessionStore(), new InMemoryIncidentRecordStore(),
			new ScenarioLogSearchProvider(), new ScenarioMetricsProvider(), new FailingRunbookRetrievalService(), NullLogger<AnalyzeIncidentUseCase>.Instance);

		var result = await useCase.AnalyzeAsync(incident);

		Assert.False(result.UsedFallbackAnalysis);
		Assert.True(result.ProviderTransparency.IsDegraded);
		Assert.Equal("degraded", result.ProviderTransparency.RagStatus);
		Assert.Equal("model-test", result.ProviderTransparency.ModelProvider);
	}

	[Theory]
	[InlineData("embedding-unavailable", "huggingface-failed/local-hashing", "sqlite", "degraded", true)]
	[InlineData("vector-store-unavailable", "local-hashing", "qdrant-unavailable/sqlite", "degraded", true)]
	[InlineData("empty-runbook-matches", "local-hashing", "sqlite", "no matches", false)]
	public async Task RagSubsystemStateDoesNotReplaceWorkingModel(string reason, string embeddingProvider, string vectorStore, string ragStatus, bool degraded)
	{
		var incident = IncidentAnalysisEvaluationScenarioCatalog.BuiltInScenarios[0].Incident;
		var runbooks = new ConfigurableRunbookRetrievalService(new RunbookRetrievalResult
		{
			EmbeddingProvider = embeddingProvider, VectorStoreProvider = vectorStore, RagStatus = ragStatus, IsDegraded = degraded, DegradedReason = degraded ? reason : null
		});
		var useCase = new AnalyzeIncidentUseCase(
			new GroundedModelAgent(), new InMemorySessionStore(), new InMemoryIncidentRecordStore(),
			new ScenarioLogSearchProvider(), new ScenarioMetricsProvider(), runbooks, NullLogger<AnalyzeIncidentUseCase>.Instance);

		var result = await useCase.AnalyzeAsync(incident);

		Assert.False(result.UsedFallbackAnalysis);
		Assert.Equal("model-test", result.ProviderTransparency.ModelProvider);
		Assert.Equal(embeddingProvider, result.ProviderTransparency.EmbeddingProvider);
		Assert.Equal(vectorStore, result.ProviderTransparency.VectorStore);
		Assert.Equal(degraded, result.ProviderTransparency.IsDegraded);
	}

	[Fact]
	public async Task BuiltInEvaluationScenariosPassAgainstDeterministicAnalysis()
	{
		var evaluator = new RubricIncidentAnalysisEvaluator();

		foreach (var scenario in IncidentAnalysisEvaluationScenarioCatalog.BuiltInScenarios.Where(item => item.Name == "checkout-5xx-regression"))
		{
			var useCase = new AnalyzeIncidentUseCase(
				new UnstructuredAgent(),
				new InMemorySessionStore(),
				new InMemoryIncidentRecordStore(),
				new ScenarioLogSearchProvider(),
				new ScenarioMetricsProvider(),
				new ScenarioRunbookRetrievalService(),
				NullLogger<AnalyzeIncidentUseCase>.Instance);

			var result = await useCase.AnalyzeAsync(scenario.Incident, scenario.Name);
			var evaluation = evaluator.Evaluate(scenario, result);

			Assert.True(
				evaluation.Score >= 0.90m,
				$"{scenario.Name} scored {evaluation.Score}. Failed checks: {string.Join(", ", evaluation.FailedChecks)}");
			Assert.NotEmpty(result.KnownFacts);
			Assert.NotEmpty(result.Unknowns);
			Assert.All(result.RecommendedActions, action => Assert.NotEmpty(action.SupportingSignals));
			Assert.Equal("High", result.Quality.EvidenceCoverage);
			Assert.Equal("test-agent", result.ProviderTransparency.ModelProvider);
		}
	}

	[Fact]
	public async Task UserSubmittedMetadataIsContextNotOperationalEvidence()
	{
		var incident = new Incident(
			Guid.NewGuid(),
			"Order fulfillment backlog",
			"Order fulfillment jobs are piling up faster than workers can process them.",
			IncidentSeverity.Sev3,
			"orders-worker",
			"production",
			DateTimeOffset.UtcNow,
			["orders", "queue"]);
		var useCase = new AnalyzeIncidentUseCase(
			new UnstructuredAgent(), new InMemorySessionStore(), new InMemoryIncidentRecordStore(),
			new EmptyLogSearchProvider(), new EmptyMetricsProvider(),
			new ConfigurableRunbookRetrievalService(new RunbookRetrievalResult()),
			NullLogger<AnalyzeIncidentUseCase>.Instance);

		var result = await useCase.AnalyzeAsync(incident);

		Assert.Empty(result.Evidence);
		Assert.Empty(result.KnownFacts);
		Assert.DoesNotContain(result.Evidence, item => item.Source?.StartsWith("incident.", StringComparison.OrdinalIgnoreCase) == true);
		Assert.Contains(result.Unknowns, item => item.Contains("controlled action", StringComparison.OrdinalIgnoreCase));
		Assert.Contains(result.Unknowns, item => item.Contains("no matching orders-worker logs", StringComparison.OrdinalIgnoreCase));
		Assert.Contains(result.Unknowns, item => item.Contains("no queue_depth samples", StringComparison.OrdinalIgnoreCase));
		Assert.All(result.RecommendedActions, item => Assert.Empty(item.SupportingSignals));
	}

	private sealed class UnstructuredAgent : IIncidentAnalysisAgent
	{
		public Task<IncidentAgentExecutionResult> AnalyzeAsync(
			Incident incident,
			IncidentAnalysisSessionContext? sessionContext = null,
			IncidentAnalysisAgentContext? agentContext = null,
			CancellationToken cancellationToken = default)
		{
			return Task.FromResult(new IncidentAgentExecutionResult
			{
				AnalysisText = "unstructured model response",
				Provider = "test-agent",
				Model = "deterministic",
				UsedFallback = false
			});
		}
	}

	private sealed class GroundedModelAgent : IIncidentAnalysisAgent
	{
		public Task<IncidentAgentExecutionResult> AnalyzeAsync(Incident incident, IncidentAnalysisSessionContext? sessionContext = null, IncidentAnalysisAgentContext? agentContext = null, CancellationToken cancellationToken = default) =>
			Task.FromResult(new IncidentAgentExecutionResult
			{
				Provider = "model-test", Model = "model-1", UsedFallback = false,
				AnalysisText = $$"""{"summary":"test","severity":"{{MicrosoftAgentFrameworkIncidentAnalysisAgent.FormatSeverity(incident.Severity)}}","evidence":[],"hypotheses":[],"recommendedActions":[{"description":"Validate the submitted incident details before mitigation.","priority":"High","rationale":"Grounded in user input.","supportingSignals":["incident.description"]}],"confidence":"Low","notes":"RAG unavailable."}"""
			});
	}

	private sealed class FailingRunbookRetrievalService : IRunbookRetrievalService
	{
		public Task<RunbookRetrievalResult> RetrieveAsync(RunbookRetrievalRequest request, CancellationToken cancellationToken = default) => throw new HttpRequestException("Embedding provider unavailable.");
	}

	private sealed class ConfigurableRunbookRetrievalService(RunbookRetrievalResult result) : IRunbookRetrievalService
	{
		public Task<RunbookRetrievalResult> RetrieveAsync(RunbookRetrievalRequest request, CancellationToken cancellationToken = default) => Task.FromResult(result);
	}

	private sealed class InMemorySessionStore : IIncidentAnalysisSessionStore
	{
		private readonly Dictionary<string, IncidentAnalysisSessionContext> _sessions = new(StringComparer.OrdinalIgnoreCase);

		public Task<IncidentAnalysisSessionContext> GetOrCreateAsync(string? sessionId, CancellationToken cancellationToken = default)
		{
			var key = string.IsNullOrWhiteSpace(sessionId) ? Guid.NewGuid().ToString("N") : sessionId.Trim();
			if (!_sessions.TryGetValue(key, out var context))
			{
				context = new IncidentAnalysisSessionContext
				{
					SessionId = key,
					UpdatedAtUtc = DateTimeOffset.UtcNow
				};
				_sessions[key] = context;
			}

			return Task.FromResult(context);
		}

		public Task SaveAsync(IncidentAnalysisSessionContext sessionContext, CancellationToken cancellationToken = default)
		{
			_sessions[sessionContext.SessionId] = sessionContext;
			return Task.CompletedTask;
		}
	}

	private sealed class InMemoryIncidentRecordStore : IIncidentRecordStore
	{
		private readonly List<IncidentAnalysisRecord> _records = [];

		public Task SaveAsync(Incident incident, IncidentAnalysisResult analysisResult, CancellationToken cancellationToken = default)
		{
			_records.Add(new IncidentAnalysisRecord
			{
				Incident = incident,
				AnalysisResult = analysisResult,
				CreatedAtUtc = DateTimeOffset.UtcNow
			});
			return Task.CompletedTask;
		}

		public Task SaveCandidatesAsync(IReadOnlyList<DetectedIncidentCandidate> candidates, MonitoringScanRecord scan, CancellationToken cancellationToken = default) => Task.CompletedTask;

		public Task<IReadOnlyList<DetectedIncidentCandidate>> GetCandidatesAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<DetectedIncidentCandidate>>(Array.Empty<DetectedIncidentCandidate>());

		public Task<Incident> ConfirmCandidateAsync(string candidateId, CancellationToken cancellationToken = default) => throw new NotSupportedException();

		public Task<DetectedIncidentCandidate> DecideCandidateAsync(string candidateId, string decision, Guid? mergeIntoIncidentId = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();

		public Task<IncidentAnalysisRecord> AddTimelineEventAsync(Guid incidentId, IncidentTimelineEvent timelineEvent, CancellationToken cancellationToken = default) => throw new NotSupportedException();

		public Task<ProposedKnowledgeUpdate> ReviewKnowledgeUpdateAsync(Guid incidentId, string decision, string? content, string? notes, CancellationToken cancellationToken = default) => throw new NotSupportedException();

		public Task<MonitoringScanRecord?> GetLastScanAsync(CancellationToken cancellationToken = default) => Task.FromResult<MonitoringScanRecord?>(null);

		public Task<IncidentAnalysisRecord?> GetByIncidentIdAsync(Guid incidentId, CancellationToken cancellationToken = default)
		{
			return Task.FromResult(_records.FirstOrDefault(record => record.Incident.Id == incidentId));
		}

		public Task<string> UpdateStatusAsync(Guid incidentId, string status, CancellationToken cancellationToken = default)
		{
			return Task.FromResult(status);
		}

		public Task<bool> DeleteAsync(Guid incidentId, CancellationToken cancellationToken = default)
		{
			return Task.FromResult(_records.RemoveAll(record => record.Incident.Id == incidentId) > 0);
		}

		public Task<IncidentActionOutcome> AddActionOutcomeAsync(Guid incidentId, string description, string status, CancellationToken cancellationToken = default)
		{
			var outcome = new IncidentActionOutcome
			{
				Description = description,
				Status = status,
				LoggedAtUtc = DateTimeOffset.UtcNow
			};
			return Task.FromResult(outcome);
		}

		public Task<IncidentAnalysisFeedback> AddFeedbackAsync(Guid incidentId, IncidentAnalysisFeedback feedback, CancellationToken cancellationToken = default) => Task.FromResult(feedback);

		public Task<IReadOnlyList<IncidentAnalysisRecord>> GetRecentAsync(int maxResults, CancellationToken cancellationToken = default)
		{
			return Task.FromResult<IReadOnlyList<IncidentAnalysisRecord>>(_records.Take(maxResults).ToArray());
		}

		public Task<IReadOnlyList<SimilarIncidentMatch>> FindSimilarAsync(Incident incident, int maxResults, CancellationToken cancellationToken = default)
		{
			return Task.FromResult<IReadOnlyList<SimilarIncidentMatch>>(Array.Empty<SimilarIncidentMatch>());
		}
	}

	private sealed class ScenarioRunbookRetrievalService : IRunbookRetrievalService
	{
		public Task<RunbookRetrievalResult> RetrieveAsync(RunbookRetrievalRequest request, CancellationToken cancellationToken = default)
		{
			var isQueue = ContainsAny(request.Query, "queue", "backlog", "worker");
			var runbook = isQueue
				? new RunbookDocument(
					"queue-backlog-growth",
					"Queue backlog runbook",
					"Queue backlog guidance for orders-worker incidents.",
					"Check queue depth, worker health, downstream latency, and rollback options.",
					["queue", "orders-worker", "metrics"])
				: new RunbookDocument(
					"checkout-5xx-triage",
					"Checkout 5xx regression runbook",
					"Checkout 500 triage guidance for checkout-api incidents.",
					"Check deploy health, dependency failures, logs, metrics, and rollback options.",
					["checkout", "5xx", "regression"]);

			return Task.FromResult(new RunbookRetrievalResult { Runbooks = [runbook] });
		}
	}

	private sealed class ScenarioLogSearchProvider : ILogSearchProvider
	{
		public Task<LogSearchResult> SearchAsync(LogSearchRequest request, CancellationToken cancellationToken = default)
		{
			var message = ContainsAny(request.Query, "queue", "backlog", "worker")
				? "orders-worker backlog warning: queue depth remains above threshold"
				: "checkout-api error: HTTP 500 responses increased after deployment";

			return Task.FromResult(new LogSearchResult
			{
				Entries =
				[
					new LogSearchEntry
					{
						Timestamp = DateTimeOffset.UtcNow,
						Source = request.ServiceName ?? "service",
						Level = "Error",
						Message = message
					}
				]
			});
		}
	}

	private sealed class ScenarioMetricsProvider : IMetricsProvider
	{
		public Task<MetricsQueryResult> QueryAsync(MetricsQueryRequest request, CancellationToken cancellationToken = default)
		{
			return Task.FromResult(new MetricsQueryResult
			{
				MetricName = request.MetricName,
				Samples =
				[
					new MetricSample
					{
						Timestamp = DateTimeOffset.UtcNow.AddMinutes(-1),
						Value = string.Equals(request.MetricName, "queue_depth", StringComparison.OrdinalIgnoreCase) ? 950 : 42
					}
				]
			});
		}
	}

	private sealed class EmptyLogSearchProvider : ILogSearchProvider
	{
		public Task<LogSearchResult> SearchAsync(LogSearchRequest request, CancellationToken cancellationToken = default) => Task.FromResult(new LogSearchResult());
	}

	private sealed class EmptyMetricsProvider : IMetricsProvider
	{
		public Task<MetricsQueryResult> QueryAsync(MetricsQueryRequest request, CancellationToken cancellationToken = default) =>
			Task.FromResult(new MetricsQueryResult { MetricName = request.MetricName });
	}

	private static bool ContainsAny(string? value, params string[] terms)
	{
		return !string.IsNullOrWhiteSpace(value) &&
			terms.Any(term => value.Contains(term, StringComparison.OrdinalIgnoreCase));
	}
}
