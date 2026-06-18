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
			IncidentSeverity.High,
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
	public async Task FindSimilarAsyncReturnsRelatedPriorIncident()
	{
		var recordsPath = Path.Combine(_rootPath, "incident-records.json");
		var store = new FileIncidentRecordStore(Options.Create(new IncidentStorageOptions { IncidentRecordsPath = recordsPath }));
		var priorIncident = new Incident(
			Guid.NewGuid(),
			"Checkout 5xx spike",
			"Checkout API returned HTTP 500 after deployment.",
			IncidentSeverity.High,
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

		var currentIncident = new Incident(
			Guid.NewGuid(),
			"Checkout 500s after deploy",
			"Customers see checkout HTTP 500 responses.",
			IncidentSeverity.High,
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
		var incident = new Incident(Guid.NewGuid(), "Delete me", "Temporary incident.", IncidentSeverity.Low);
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

	public void Dispose()
	{
		if (Directory.Exists(_rootPath))
		{
			Directory.Delete(_rootPath, recursive: true);
		}
	}
}
