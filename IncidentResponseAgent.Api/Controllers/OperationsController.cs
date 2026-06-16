using IncidentResponseAgent.Api.Contracts.Operations;
using IncidentResponseAgent.Infrastructure.Incidents;
using IncidentResponseAgent.Infrastructure.Runbooks;
using IncidentResponseAgent.Infrastructure.Tools;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace IncidentResponseAgent.Api.Controllers;

[ApiController]
[Route("api/operations")]
public sealed class OperationsController : ControllerBase
{
	private readonly OperationalDataOptions _operationalDataOptions;
	private readonly RunbookRetrievalOptions _runbookRetrievalOptions;
	private readonly IncidentStorageOptions _incidentStorageOptions;

	public OperationsController(
		IOptions<OperationalDataOptions> operationalDataOptions,
		IOptions<RunbookRetrievalOptions> runbookRetrievalOptions,
		IOptions<IncidentStorageOptions> incidentStorageOptions)
	{
		_operationalDataOptions = operationalDataOptions.Value ?? new OperationalDataOptions();
		_runbookRetrievalOptions = runbookRetrievalOptions.Value ?? new RunbookRetrievalOptions();
		_incidentStorageOptions = incidentStorageOptions.Value ?? new IncidentStorageOptions();
	}

	[HttpGet("sources")]
	[ProducesResponseType(typeof(IReadOnlyList<OperationalSourceResponse>), StatusCodes.Status200OK)]
	public ActionResult<IReadOnlyList<OperationalSourceResponse>> GetSources()
	{
		var logPath = ResolveFilePath(_operationalDataOptions.LogEntriesPath, Path.Combine("Tools", "SampleData", "logs.json"));
		var metricPath = ResolveFilePath(_operationalDataOptions.MetricSamplesPath, Path.Combine("Tools", "SampleData", "metrics.json"));
		var runbookPath = ResolveDirectoryPath(_runbookRetrievalOptions.KnowledgeBasePath, Path.Combine("Runbooks", "KnowledgeBase"));
		var runbookDatabasePath = ResolveLocalDataFilePath(_runbookRetrievalOptions.DatabasePath, "runbook-rag.sqlite");
		var qdrantEnabled = string.Equals(_runbookRetrievalOptions.VectorStoreProvider, "Qdrant", StringComparison.OrdinalIgnoreCase);
		var sessionDatabasePath = ResolveLocalDataFilePath(_incidentStorageOptions.SessionDatabasePath, "incident-sessions.sqlite");
		var incidentRecordsPath = ResolveLocalDataFilePath(_incidentStorageOptions.IncidentRecordsPath, "incident-records.json");

		return Ok(new[]
		{
			new OperationalSourceResponse
			{
				Name = "Logs",
				Type = "file",
				Mode = string.IsNullOrWhiteSpace(_operationalDataOptions.LogEntriesPath) ? "sample" : "configured",
				Location = logPath,
				Status = System.IO.File.Exists(logPath) ? "connected" : "missing",
				Description = "The detector searches this JSON file for errors, warnings, timeouts, latency, backlog, failures, and HTTP 500 signals.",
				IsDemoMode = string.IsNullOrWhiteSpace(_operationalDataOptions.LogEntriesPath),
				Capabilities = ["file override", "HTTP ingestion", "polling detection"]
			},
			new OperationalSourceResponse
			{
				Name = "Metrics",
				Type = "file",
				Mode = string.IsNullOrWhiteSpace(_operationalDataOptions.MetricSamplesPath) ? "sample" : "configured",
				Location = metricPath,
				Status = System.IO.File.Exists(metricPath) ? "connected" : "missing",
				Description = "The detector reads this JSON file for request error rate and queue depth threshold breaches.",
				IsDemoMode = string.IsNullOrWhiteSpace(_operationalDataOptions.MetricSamplesPath),
				Capabilities = ["file override", "HTTP ingestion", "threshold detection"]
			},
			new OperationalSourceResponse
			{
				Name = "Runbooks",
				Type = "directory",
				Mode = string.IsNullOrWhiteSpace(_runbookRetrievalOptions.KnowledgeBasePath) ? "bundled" : "configured",
				Location = runbookPath,
				Status = Directory.Exists(runbookPath) ? "connected" : "missing",
				Description = "RAG chunks Markdown runbooks from this folder and retrieves matching guidance during analysis.",
				IsDemoMode = string.IsNullOrWhiteSpace(_runbookRetrievalOptions.KnowledgeBasePath),
				Capabilities = ["markdown RAG", "configurable folder", "SQLite vector cache"]
			},
			new OperationalSourceResponse
			{
				Name = "Vector Search",
				Type = "database",
				Mode = _runbookRetrievalOptions.VectorStoreProvider,
				Location = qdrantEnabled
					? $"{_runbookRetrievalOptions.QdrantEndpoint} / {_runbookRetrievalOptions.QdrantCollectionName}"
					: runbookDatabasePath,
				Status = qdrantEnabled ? "configured" : (System.IO.File.Exists(runbookDatabasePath) ? "connected" : "pending"),
				Description = qdrantEnabled
					? "The app tries this Qdrant collection first when the provider is enabled."
					: "SQLite is the configured local vector store for runbook retrieval.",
				IsDemoMode = false,
				Capabilities = qdrantEnabled ? ["Qdrant primary", "SQLite fallback"] : ["SQLite primary", "local embeddings", "semantic retrieval cache"]
			},
			new OperationalSourceResponse
			{
				Name = "Runbook Vector Cache",
				Type = "database",
				Mode = "sqlite-fallback",
				Location = runbookDatabasePath,
				Status = System.IO.File.Exists(runbookDatabasePath) ? "connected" : "pending",
				Description = "SQLite stores indexed runbook chunks and embeddings, and is the persistent fallback when Qdrant is unavailable.",
				IsDemoMode = false,
				Capabilities = ["local embeddings", "semantic retrieval cache"]
			},
			new OperationalSourceResponse
			{
				Name = "Investigation Sessions",
				Type = "database",
				Mode = string.IsNullOrWhiteSpace(_incidentStorageOptions.SessionDatabasePath) ? "local" : "configured",
				Location = sessionDatabasePath,
				Status = System.IO.File.Exists(sessionDatabasePath) ? "connected" : "pending",
				Description = "Multi-turn analysis uses this SQLite database to remember the previous incident and analysis summary for each session ID.",
				IsDemoMode = false,
				Capabilities = ["multi-turn context", "session continuity"]
			},
			new OperationalSourceResponse
			{
				Name = "Incident History",
				Type = "file",
				Mode = string.IsNullOrWhiteSpace(_incidentStorageOptions.IncidentRecordsPath) ? "local" : "configured",
				Location = incidentRecordsPath,
				Status = System.IO.File.Exists(incidentRecordsPath) ? "connected" : "pending",
				Description = "Recent analyses are saved here so the History tab can show previous runs and analysis can retrieve similar past incidents.",
				IsDemoMode = false,
				Capabilities = ["recent history", "similar incident retrieval"]
			}
		});
	}

	private static string ResolveFilePath(string? configuredPath, string defaultRelativePath)
	{
		if (!string.IsNullOrWhiteSpace(configuredPath))
		{
			return Path.GetFullPath(Environment.ExpandEnvironmentVariables(configuredPath));
		}

		return Path.Combine(AppContext.BaseDirectory, defaultRelativePath);
	}

	private static string ResolveDirectoryPath(string? configuredPath, string defaultRelativePath)
	{
		if (!string.IsNullOrWhiteSpace(configuredPath))
		{
			return Path.GetFullPath(Environment.ExpandEnvironmentVariables(configuredPath));
		}

		return Path.Combine(AppContext.BaseDirectory, defaultRelativePath);
	}

	private static string ResolveLocalDataFilePath(string? configuredPath, string defaultFileName)
	{
		if (!string.IsNullOrWhiteSpace(configuredPath))
		{
			return Path.GetFullPath(Environment.ExpandEnvironmentVariables(configuredPath));
		}

		return Path.Combine(
			Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
			"IncidentResponseAgent",
			defaultFileName);
	}
}
