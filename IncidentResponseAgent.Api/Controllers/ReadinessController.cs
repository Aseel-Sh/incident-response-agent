using IncidentResponseAgent.Agent.Incidents;
using IncidentResponseAgent.Application.Incidents;
using IncidentResponseAgent.Infrastructure.Incidents;
using IncidentResponseAgent.Infrastructure.Runbooks;
using IncidentResponseAgent.Infrastructure.Tools;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using IncidentResponseAgent.Api.Security;

namespace IncidentResponseAgent.Api.Controllers;

[ApiController]
[Route("ready")]
public sealed class ReadinessController(
	IOptions<IncidentAnalysisAgentOptions> agentOptions,
	IOptions<RunbookRetrievalOptions> runbookOptions,
	IOptions<OperationalDataOptions> operationalOptions,
	IOptions<IncidentStorageOptions> storageOptions,
	IOptions<IncidentAuthenticationOptions> authenticationOptions,
	IIncidentMonitoringCoordinator monitoring) : ControllerBase
{
	[HttpGet]
	public async Task<IActionResult> GetAsync(CancellationToken cancellationToken)
	{
		var agent = agentOptions.Value;
		var runbooks = runbookOptions.Value;
		var operational = operationalOptions.Value;
		var storage = storageOptions.Value;
		var modelConfigured = HasValue(agent.ApiKey, "IRA_AGENT_API_KEY", "OPENROUTER_API_KEY") && !string.IsNullOrWhiteSpace(agent.Model);
		var embeddingsConfigured = HasValue(runbooks.ApiKey, "HF_TOKEN", "HF_API_TOKEN");
		var vectorStore = runbooks.VectorStoreProvider.Equals("Qdrant", StringComparison.OrdinalIgnoreCase)
			? (!string.IsNullOrWhiteSpace(runbooks.QdrantEndpoint) ? "configured" : "unconfigured")
			: "local-selected";
		var logs = SourceState(operational.LogEntriesPath, Path.Combine(AppContext.BaseDirectory, "Tools", "SampleData", "logs.json"));
		var metrics = SourceState(operational.MetricSamplesPath, Path.Combine(AppContext.BaseDirectory, "Tools", "SampleData", "metrics.json"));
		var monitor = await monitoring.GetStateAsync(cancellationToken: cancellationToken);
		var persistenceConfigured = !string.IsNullOrWhiteSpace(storage.IncidentRecordsPath) && !string.IsNullOrWhiteSpace(storage.SessionDatabasePath);
		var authenticationConfigured = authenticationOptions.Value.Users.Count > 0 && !authenticationOptions.Value.AllowDevelopmentIdentity;
		var ready = modelConfigured && embeddingsConfigured && vectorStore != "unconfigured" && logs != "missing" && metrics != "missing" && persistenceConfigured && authenticationConfigured;
		var body = new
		{
			status = ready ? "ready" : "not-ready",
			components = new
			{
				authentication = new { status = authenticationConfigured ? "configured" : authenticationOptions.Value.AllowDevelopmentIdentity ? "development-identity" : "unconfigured" },
				model = new { status = modelConfigured ? "configured" : "unconfigured", provider = agent.Provider, agent.Model },
				embeddings = new { status = embeddingsConfigured ? "configured" : "unconfigured", runbooks.Model },
				vectorStore = new { status = vectorStore, provider = runbooks.VectorStoreProvider, endpoint = runbooks.VectorStoreProvider.Equals("Qdrant", StringComparison.OrdinalIgnoreCase) ? runbooks.QdrantEndpoint : null },
				persistence = new { status = persistenceConfigured ? "configured" : "default-local", incidentRecords = storage.IncidentRecordsPath, sessions = storage.SessionDatabasePath },
				telemetry = new { status = logs == "available" && metrics == "available" ? "available" : "degraded", logs, metrics },
				monitoring = new { status = monitor.LastError is null ? (monitor.Enabled ? "running" : "paused") : "degraded", monitor.LastError, monitor.LastScan }
			}
		};
		return StatusCode(ready ? StatusCodes.Status200OK : StatusCodes.Status503ServiceUnavailable, body);
	}

	private static bool HasValue(string? configured, params string[] environmentVariables) =>
		!string.IsNullOrWhiteSpace(configured) || environmentVariables.Any(name => !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(name)));

	private static string SourceState(string? configured, string fallback)
	{
		var path = string.IsNullOrWhiteSpace(configured) ? fallback : Path.GetFullPath(Environment.ExpandEnvironmentVariables(configured));
		return System.IO.File.Exists(path) ? "available" : "missing";
	}
}
