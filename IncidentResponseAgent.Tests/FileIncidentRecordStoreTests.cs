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

	public void Dispose()
	{
		if (Directory.Exists(_rootPath))
		{
			Directory.Delete(_rootPath, recursive: true);
		}
	}
}
