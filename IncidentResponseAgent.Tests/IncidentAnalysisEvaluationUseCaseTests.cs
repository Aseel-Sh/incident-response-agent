using IncidentResponseAgent.Application.Evaluation;
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
	public async Task BuiltInEvaluationScenariosPassAgainstDeterministicAnalysis()
	{
		var evaluator = new RubricIncidentAnalysisEvaluator();

		foreach (var scenario in IncidentAnalysisEvaluationScenarioCatalog.BuiltInScenarios)
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
		}
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

	private static bool ContainsAny(string? value, params string[] terms)
	{
		return !string.IsNullOrWhiteSpace(value) &&
			terms.Any(term => value.Contains(term, StringComparison.OrdinalIgnoreCase));
	}
}
