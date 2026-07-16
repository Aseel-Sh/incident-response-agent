using IncidentResponseAgent.Application.Incidents;
using IncidentResponseAgent.Domain.Incidents;
using IncidentResponseAgent.Infrastructure.Incidents;
using Microsoft.Extensions.Options;

namespace IncidentResponseAgent.Tests;

public sealed class FileIncidentRecordStoreTests : IDisposable
{
	private readonly string _rootPath = Path.Combine(Path.GetTempPath(), "ira-history-tests", Guid.NewGuid().ToString("N"));

	[Fact]
	public async Task GetRecentAsyncUsesConfiguredIncidentRecordsPath()
	{
		var recordsPath = Path.Combine(_rootPath, "incident-records.json");
		var store = new FileIncidentRecordStore(Options.Create(new IncidentStorageOptions { IncidentRecordsPath = recordsPath }));
		var incident = new Incident(
			Guid.NewGuid(),
			"Inventory API errors",
			"Inventory API is returning errors.",
			IncidentSeverity.Sev2,
			"inventory-api",
			"production");
		var analysis = new IncidentAnalysisResult
		{
			IncidentId = incident.Id,
			IncidentSummary = "Inventory API errors.",
			AnalysisText = "{}",
			AnalysisProvider = "local-prompt",
			SessionId = "session-1",
			SessionTurnNumber = 1,
			Confidence = "Medium"
		};

		await store.SaveAsync(incident, analysis);
		var recent = await store.GetRecentAsync(5);

		Assert.True(File.Exists(recordsPath));
		Assert.Single(recent);
		Assert.Equal(incident.Id, recent[0].Incident.Id);
	}

	[Fact]
	public async Task ProjectScopedHistoryAndSimilarityDoNotLeakAcrossProjects()
	{
		var recordsPath = Path.Combine(_rootPath, "project-records.json");
		var store = new FileIncidentRecordStore(Options.Create(new IncidentStorageOptions { IncidentRecordsPath = recordsPath }));
		var projectA = new Incident(Guid.NewGuid(), "Checkout 5xx spike", "Checkout failed after deploy.", IncidentSeverity.Sev2, "checkout-api", "production", tags: ["checkout", "5xx"], projectId: "project-a");
		var projectB = new Incident(Guid.NewGuid(), "Checkout 5xx spike", "Checkout failed after deploy.", IncidentSeverity.Sev2, "checkout-api", "production", tags: ["checkout", "5xx"], projectId: "project-b");

		foreach (var incident in new[] { projectA, projectB })
		{
			await store.SaveAsync(incident, new IncidentAnalysisResult
			{
				IncidentId = incident.Id,
				IncidentSummary = incident.Title,
				AnalysisText = "{}",
				AnalysisProvider = "local-prompt",
				SessionId = $"session-{incident.ProjectId}",
				RecommendedActions = [new IncidentActionRecommendation { Description = $"Rollback for {incident.ProjectId}", Priority = "High" }]
			});
			await store.UpdateStatusAsync(incident.Id, "resolved");
			await store.ReviewKnowledgeUpdateAsync(incident.Id, "approved", null, "Approved in test.");
		}

		var recentA = await store.GetRecentAsync(10, "project-a");
		var query = new Incident(Guid.NewGuid(), "Checkout 500s after deploy", "Customers see checkout HTTP 500 responses.", IncidentSeverity.Sev2, "checkout-api", "production", tags: ["checkout"], projectId: "project-a");
		var similarA = await store.FindSimilarAsync(query, 5, "project-a");

		Assert.Single(recentA);
		Assert.Equal(projectA.Id, recentA[0].Incident.Id);
		Assert.Single(similarA);
		Assert.Equal(projectA.Id, similarA[0].IncidentId);
		Assert.DoesNotContain(similarA, item => item.IncidentId == projectB.Id);
	}

	[Fact]
	public async Task FindSimilarAsyncReturnsRelatedPriorIncident()
	{
		var recordsPath = Path.Combine(_rootPath, "incident-records.json");
		var store = new FileIncidentRecordStore(Options.Create(new IncidentStorageOptions { IncidentRecordsPath = recordsPath }));
		var priorIncident = new Incident(
			Guid.NewGuid(),
			"Checkout 5xx spike",
			"Checkout API returned HTTP 500 after deployment.",
			IncidentSeverity.Sev2,
			"checkout-api",
			"production",
			tags: ["checkout", "5xx", "deploy"]);
		var priorAnalysis = new IncidentAnalysisResult
		{
			IncidentId = priorIncident.Id,
			IncidentSummary = "Checkout API 5xx spike after deploy.",
			AnalysisText = "{}",
			AnalysisProvider = "local-prompt",
			SessionId = "session-1",
			SessionTurnNumber = 1,
			RecommendedActions =
			[
				new IncidentActionRecommendation
				{
					Description = "Roll back the checkout-api deployment.",
					Priority = "High"
				}
			],
			Confidence = "Medium"
		};
		await store.SaveAsync(priorIncident, priorAnalysis);
		await store.UpdateStatusAsync(priorIncident.Id, "resolved");
		await store.ReviewKnowledgeUpdateAsync(priorIncident.Id, "approved", null, "Approved in test.");

		var currentIncident = new Incident(
			Guid.NewGuid(),
			"Checkout 500s after deploy",
			"Customers see checkout HTTP 500 responses.",
			IncidentSeverity.Sev2,
			"checkout-api",
			"production",
			tags: ["checkout", "5xx"]);

		var matches = await store.FindSimilarAsync(currentIncident, 3);

		Assert.Single(matches);
		Assert.Equal(priorIncident.Id, matches[0].IncidentId);
		Assert.Contains("Roll back", matches[0].ResolutionSummary);
		Assert.Contains("checkout", matches[0].SharedSignals);
	}

	[Fact]
	public async Task DeleteAsyncRemovesSavedIncident()
	{
		var recordsPath = Path.Combine(_rootPath, "incident-records.json");
		var store = new FileIncidentRecordStore(Options.Create(new IncidentStorageOptions { IncidentRecordsPath = recordsPath }));
		var incident = new Incident(Guid.NewGuid(), "Delete me", "Temporary incident.", IncidentSeverity.Sev4);
		var analysis = new IncidentAnalysisResult
		{
			IncidentId = incident.Id,
			IncidentSummary = "Temporary incident.",
			AnalysisText = "{}",
			AnalysisProvider = "local-prompt",
			SessionId = "session-delete",
			SessionTurnNumber = 1,
			Confidence = "Low"
		};

		await store.SaveAsync(incident, analysis);

		Assert.True(await store.DeleteAsync(incident.Id));
		Assert.Null(await store.GetByIncidentIdAsync(incident.Id));
		Assert.False(await store.DeleteAsync(incident.Id));
	}

	[Fact]
	public async Task RepeatedScopeRefreshesCandidateAndSuccessfulClearMarksRecovery()
	{
		var store = new FileIncidentRecordStore(Options.Create(new IncidentStorageOptions { IncidentRecordsPath = Path.Combine(_rootPath, "candidate-records.json") }));
		var first = new DetectedIncidentCandidate { Id = "signal-1", Title = "Errors", Description = "Error threshold", Severity = IncidentSeverity.Sev2, ServiceName = "checkout-api", Environment = "production", DetectedAtUtc = DateTimeOffset.UtcNow, Source = "logs", Signals = ["HTTP 500"] };
		var second = first with { Id = "signal-2", Title = "Errors and latency", Severity = IncidentSeverity.Sev1, Source = "metrics", Signals = ["request_error_rate=80"] };
		var firstScan = new MonitoringScanRecord { StartedAtUtc = DateTimeOffset.UtcNow, CompletedAtUtc = DateTimeOffset.UtcNow, CandidateCount = 1, ScannedSourceCount = 2 };

		await store.SaveCandidatesAsync([first], firstScan);
		await store.SaveCandidatesAsync([second], firstScan with { Id = Guid.NewGuid(), CompletedAtUtc = DateTimeOffset.UtcNow.AddSeconds(1) });
		var refreshed = await store.GetCandidatesAsync();

		Assert.Single(refreshed);
		Assert.Equal("signal-1", refreshed[0].Id);
		Assert.Equal(IncidentSeverity.Sev1, refreshed[0].Severity);
		Assert.Contains("request_error_rate=80", refreshed[0].Signals);
		Assert.Contains(refreshed[0].Timeline, item => item.Summary.Contains("without creating a duplicate", StringComparison.OrdinalIgnoreCase));

		await store.SaveCandidatesAsync([], firstScan with { Id = Guid.NewGuid(), CandidateCount = 0, CompletedAtUtc = DateTimeOffset.UtcNow.AddSeconds(2) });
		var recovered = Assert.Single(await store.GetCandidatesAsync());
		Assert.Equal("recovered", recovered.Status);
		Assert.Contains(recovered.Timeline, item => item.Type == "recovered");
	}

	[Fact]
	public async Task ApprovedKnowledgeIsPublishedAndRemovedWhenIncidentIsDeleted()
	{
		var publisher = new RecordingKnowledgePublisher();
		var store = new FileIncidentRecordStore(
			Options.Create(new IncidentStorageOptions { IncidentRecordsPath = Path.Combine(_rootPath, "published-records.json") }),
			publisher);
		var incident = new Incident(Guid.NewGuid(), "Redis saturation", "Redis latency saturated the catalog API.", IncidentSeverity.Sev2, "catalog-api", "production");
		await store.SaveAsync(incident, new IncidentAnalysisResult { IncidentId = incident.Id, IncidentSummary = incident.Title, AnalysisText = "{}", AnalysisProvider = "test", SessionId = "publish-session" });
		await store.UpdateStatusAsync(incident.Id, "resolved");

		var approved = await store.ReviewKnowledgeUpdateAsync(incident.Id, "approved", "Approved mitigation knowledge.", null);
		Assert.Equal(approved.Id, publisher.PublishedId);
		Assert.Equal("Approved mitigation knowledge.", publisher.PublishedContent);

		await store.DeleteAsync(incident.Id);
		Assert.Equal(approved.Id, publisher.RemovedId);
	}

	[Fact]
	public async Task CandidateDecisionPersistsWithoutCreatingReusableIncident()
	{
		var recordsPath = Path.Combine(_rootPath, "incident-records.json");
		var store = new FileIncidentRecordStore(Options.Create(new IncidentStorageOptions { IncidentRecordsPath = recordsPath }));
		var detectedAt = DateTimeOffset.UtcNow.AddMinutes(-1);
		var candidate = new DetectedIncidentCandidate
		{
			Id = "candidate-false-positive", Title = "Transient warning", Description = "One warning was observed.", Severity = IncidentSeverity.Sev5,
			ServiceName = "inventory-api", Environment = "production", DetectedAtUtc = detectedAt, Source = "logs", Signals = ["warning count=1"]
		};

		await store.SaveCandidatesAsync([candidate], new MonitoringScanRecord { StartedAtUtc = detectedAt, CompletedAtUtc = detectedAt.AddSeconds(1), CandidateCount = 1 });
		await store.DecideCandidateAsync(candidate.Id, "false_positive");

		var saved = Assert.Single(await store.GetCandidatesAsync());
		Assert.Equal("false_positive", saved.Status);
		Assert.Contains(saved.Timeline, item => item.Type == "false positive");
		Assert.Empty(await store.GetRecentAsync(10));
	}

	[Fact]
	public async Task AnalysisFeedbackPersistsWithIncident()
	{
		var recordsPath = Path.Combine(_rootPath, "incident-records.json");
		var store = new FileIncidentRecordStore(Options.Create(new IncidentStorageOptions { IncidentRecordsPath = recordsPath }));
		var incident = new Incident(Guid.NewGuid(), "Feedback test", "A grounded incident.", IncidentSeverity.Sev3);
		await store.SaveAsync(incident, new IncidentAnalysisResult { IncidentId = incident.Id, IncidentSummary = incident.Title, AnalysisText = "{}", AnalysisProvider = "test", SessionId = "feedback-session", SessionTurnNumber = 1 });
		var feedback = new IncidentAnalysisFeedback
		{
			AnalysisUsefulness = "partially useful", RecommendationCorrectness = "wrong", ReasonTags = ["missing evidence", "bad remediation"], SubmittedAtUtc = DateTimeOffset.UtcNow
		};

		await store.AddFeedbackAsync(incident.Id, feedback);

		var saved = await store.GetByIncidentIdAsync(incident.Id);
		Assert.Equal(feedback.Id, Assert.Single(saved!.Feedback).Id);
		Assert.Contains(saved.Timeline, item => item.Type == "analysis feedback recorded");
	}

	[Fact]
	public async Task FallbackProviderAndReasonPersistHonestly()
	{
		var recordsPath = Path.Combine(_rootPath, "incident-records.json");
		var store = new FileIncidentRecordStore(Options.Create(new IncidentStorageOptions { IncidentRecordsPath = recordsPath }));
		var incident = new Incident(Guid.NewGuid(), "Fallback persistence", "Model provider was unavailable.", IncidentSeverity.Sev4);
		var analysis = new IncidentAnalysisResult
		{
			IncidentId = incident.Id, IncidentSummary = incident.Title, AnalysisText = "{}", AnalysisProvider = "local-prompt", AnalysisModel = "local",
			UsedFallbackAnalysis = true, FallbackReason = "OpenAI-compatible provider returned 503 Service Unavailable.", SessionId = "fallback-session", SessionTurnNumber = 1,
			ProviderTransparency = new AnalysisProviderTransparency { ModelProvider = "local-prompt", Model = "local", UsedModelFallback = true, FallbackReason = "OpenAI-compatible provider returned 503 Service Unavailable." }
		};

		await store.SaveAsync(incident, analysis);
		var saved = await store.GetByIncidentIdAsync(incident.Id);

		Assert.True(saved!.AnalysisResult.UsedFallbackAnalysis);
		Assert.Equal("local-prompt", saved.AnalysisResult.ProviderTransparency.ModelProvider);
		Assert.Contains("503", saved.AnalysisResult.ProviderTransparency.FallbackReason);
	}

	[Fact]
	public async Task AssignmentAndAcknowledgementPersistWithAuditTimeline()
	{
		var store = new FileIncidentRecordStore(Options.Create(new IncidentStorageOptions { IncidentRecordsPath = Path.Combine(_rootPath, "coordination-records.json") }));
		var incident = new Incident(Guid.NewGuid(), "Owned incident", "Requires a responder.", IncidentSeverity.Sev2);
		await store.SaveAsync(incident, new IncidentAnalysisResult { IncidentId = incident.Id, IncidentSummary = incident.Title, AnalysisText = "{}", AnalysisProvider = "test", SessionId = "coordination" });

		var updated = await store.UpdateCoordinationAsync(incident.Id, "payments-oncall", "aseel", acknowledge: true);

		Assert.Equal("payments-oncall", updated.Assignee);
		Assert.Equal("aseel", updated.AcknowledgedBy);
		Assert.NotNull(updated.AcknowledgedAtUtc);
		Assert.Contains(updated.Timeline, item => item.Type == "assignment changed");
		Assert.Contains(updated.Timeline, item => item.Type == "acknowledged" && item.Actor == "aseel");
	}

	[Fact]
	public async Task AnalysisFailureDoesNotRemoveConfirmedIncident()
	{
		var store = new FileIncidentRecordStore(Options.Create(new IncidentStorageOptions { IncidentRecordsPath = Path.Combine(_rootPath, "failed-analysis-records.json") }));
		var candidate = new DetectedIncidentCandidate { Id = "candidate-analysis-failure", Title = "Provider outage", Description = "The model provider is unavailable.", Severity = IncidentSeverity.Sev2, DetectedAtUtc = DateTimeOffset.UtcNow, Source = "manual" };
		await store.SaveCandidatesAsync([candidate], new MonitoringScanRecord { StartedAtUtc = DateTimeOffset.UtcNow, CompletedAtUtc = DateTimeOffset.UtcNow, CandidateCount = 1 });
		var incident = await store.ConfirmCandidateAsync(candidate.Id);

		var failed = await store.MarkAnalysisFailedAsync(incident.Id, "OpenRouter unavailable");

		Assert.Equal("failed", failed.AnalysisResult.AnalysisState);
		Assert.Equal("new", failed.Status);
		Assert.Contains(failed.Timeline, item => item.Type == "incident confirmed");
		Assert.Contains(failed.Timeline, item => item.Type == "analysis failed");
	}

	public void Dispose()
	{
		if (Directory.Exists(_rootPath))
		{
			Directory.Delete(_rootPath, recursive: true);
		}
	}

	private sealed class RecordingKnowledgePublisher : IApprovedKnowledgePublisher
	{
		public Guid? PublishedId { get; private set; }
		public Guid? RemovedId { get; private set; }
		public string? PublishedContent { get; private set; }
		public Task PublishAsync(Guid proposalId, Guid incidentId, string title, string content, CancellationToken cancellationToken = default)
		{
			PublishedId = proposalId;
			PublishedContent = content;
			return Task.CompletedTask;
		}
		public Task RemoveAsync(Guid proposalId, CancellationToken cancellationToken = default)
		{
			RemovedId = proposalId;
			return Task.CompletedTask;
		}
	}
}
