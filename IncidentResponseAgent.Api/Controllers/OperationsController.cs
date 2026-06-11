using IncidentResponseAgent.Api.Contracts.Operations;
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

	public OperationsController(
		IOptions<OperationalDataOptions> operationalDataOptions,
		IOptions<RunbookRetrievalOptions> runbookRetrievalOptions)
	{
		_operationalDataOptions = operationalDataOptions.Value ?? new OperationalDataOptions();
		_runbookRetrievalOptions = runbookRetrievalOptions.Value ?? new RunbookRetrievalOptions();
	}

	[HttpGet("sources")]
	[ProducesResponseType(typeof(IReadOnlyList<OperationalSourceResponse>), StatusCodes.Status200OK)]
	public ActionResult<IReadOnlyList<OperationalSourceResponse>> GetSources()
	{
		var logPath = ResolveFilePath(_operationalDataOptions.LogEntriesPath, Path.Combine("Tools", "SampleData", "logs.json"));
		var metricPath = ResolveFilePath(_operationalDataOptions.MetricSamplesPath, Path.Combine("Tools", "SampleData", "metrics.json"));
		var runbookPath = ResolveDirectoryPath(_runbookRetrievalOptions.KnowledgeBasePath, Path.Combine("Runbooks", "KnowledgeBase"));

		return Ok(new[]
		{
			new OperationalSourceResponse
			{
				Name = "Logs",
				Type = "file",
				Mode = string.IsNullOrWhiteSpace(_operationalDataOptions.LogEntriesPath) ? "sample" : "configured",
				Location = logPath,
				Status = System.IO.File.Exists(logPath) ? "connected" : "missing",
				Description = "The detector searches this JSON file for errors, warnings, timeouts, latency, backlog, failures, and HTTP 500 signals."
			},
			new OperationalSourceResponse
			{
				Name = "Metrics",
				Type = "file",
				Mode = string.IsNullOrWhiteSpace(_operationalDataOptions.MetricSamplesPath) ? "sample" : "configured",
				Location = metricPath,
				Status = System.IO.File.Exists(metricPath) ? "connected" : "missing",
				Description = "The detector reads this JSON file for request error rate and queue depth threshold breaches."
			},
			new OperationalSourceResponse
			{
				Name = "Runbooks",
				Type = "directory",
				Mode = string.IsNullOrWhiteSpace(_runbookRetrievalOptions.KnowledgeBasePath) ? "bundled" : "configured",
				Location = runbookPath,
				Status = Directory.Exists(runbookPath) ? "connected" : "missing",
				Description = "RAG chunks Markdown runbooks from this folder and retrieves matching guidance during analysis."
			},
			new OperationalSourceResponse
			{
				Name = "Vector Search",
				Type = "database",
				Mode = _runbookRetrievalOptions.VectorStoreProvider,
				Location = $"{_runbookRetrievalOptions.QdrantEndpoint} / {_runbookRetrievalOptions.QdrantCollectionName}",
				Status = "configured",
				Description = "The app tries Qdrant first and falls back to local SQLite vector search when Qdrant is unavailable."
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
}
