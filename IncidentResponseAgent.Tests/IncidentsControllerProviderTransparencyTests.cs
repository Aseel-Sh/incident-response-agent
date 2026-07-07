using IncidentResponseAgent.Api.Contracts.Incidents;
using IncidentResponseAgent.Api.Controllers;
using IncidentResponseAgent.Application.Incidents;
using IncidentResponseAgent.Domain.Incidents;
using IncidentResponseAgent.Infrastructure.Incidents;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace IncidentResponseAgent.Tests;

public sealed class IncidentsControllerProviderTransparencyTests : IDisposable
{
	private readonly string _root = Path.Combine(Path.GetTempPath(), "ira-provider-contract", Guid.NewGuid().ToString("N"));

	[Fact]
	public async Task AnalyzeResponseDisplaysActualFinalProviderAndFallbackReason()
	{
		var expected = new IncidentAnalysisResult
		{
			IncidentId = Guid.NewGuid(), IncidentSummary = "Local evidence analysis", AnalysisText = "{}", AnalysisProvider = "local-prompt", AnalysisModel = "local",
			Severity = "SEV-3",
			UsedFallbackAnalysis = true, FallbackReason = "Model returned 503 Service Unavailable.", SessionId = "session", SessionTurnNumber = 1,
			ProviderTransparency = new AnalysisProviderTransparency { ModelProvider = "local-prompt", Model = "local", AttemptedModelProvider = "OpenRouter", AttemptedModel = "test/model", UsedModelFallback = true, FallbackReason = "Model returned 503 Service Unavailable.", RagStatus = "available", EvidenceGatheringDurationMilliseconds = 12, RagDurationMilliseconds = 7, ModelDurationMilliseconds = 45_000, FallbackStage = "during_model_execution", TimeoutSource = "ResilientIncidentAnalysisAgent.CancelAfter(45s)" }
		};
		var store = new FileIncidentRecordStore(Options.Create(new IncidentStorageOptions { IncidentRecordsPath = Path.Combine(_root, "records.json") }));
		var controller = new IncidentsController(new StubAnalyzeUseCase(expected), new StubRecentUseCase(), new StubMonitor(), store);

		var action = await controller.AnalyzeAsync(new IncidentSubmissionRequest { Title = "Provider contract", Description = "Verify honest provider response.", Severity = "sev3" }, CancellationToken.None);
		var response = Assert.IsType<IncidentAnalysisResponse>(Assert.IsType<OkObjectResult>(action.Result).Value);

		Assert.Equal("local-prompt", response.AnalysisProvider);
		Assert.Equal("SEV-3", response.Severity);
		Assert.True(response.UsedFallbackAnalysis);
		Assert.Equal("local-prompt", response.ProviderTransparency.ModelProvider);
		Assert.True(response.ProviderTransparency.UsedModelFallback);
		Assert.Contains("503", response.ProviderTransparency.FallbackReason);
		Assert.Equal("OpenRouter", response.ProviderTransparency.AttemptedModelProvider);
		Assert.Equal(45_000, response.ProviderTransparency.ModelDurationMilliseconds);
		Assert.Equal("during_model_execution", response.ProviderTransparency.FallbackStage);
	}

	public void Dispose()
	{
		if (Directory.Exists(_root)) Directory.Delete(_root, true);
	}

	private sealed class StubAnalyzeUseCase(IncidentAnalysisResult result) : IAnalyzeIncidentUseCase
	{
		public Task<IncidentAnalysisResult> AnalyzeAsync(Incident incident, string? sessionId = null, CancellationToken cancellationToken = default) => Task.FromResult(result with { IncidentId = incident.Id });
	}
	private sealed class StubRecentUseCase : IGetRecentIncidentAnalysesUseCase
	{
		public Task<IReadOnlyList<GetRecentIncidentAnalysesResult>> ExecuteAsync(int maxResults = 10, string? projectId = null, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<GetRecentIncidentAnalysesResult>>(Array.Empty<GetRecentIncidentAnalysesResult>());
	}
	private sealed class StubMonitor : IIncidentSignalMonitor
	{
		public Task<IReadOnlyList<DetectedIncidentCandidate>> DetectAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<DetectedIncidentCandidate>>(Array.Empty<DetectedIncidentCandidate>());
	}
}
