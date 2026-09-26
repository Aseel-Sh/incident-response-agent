using IncidentResponseAgent.Agent.Incidents;
using IncidentResponseAgent.Application.Incidents;
using IncidentResponseAgent.Infrastructure.Incidents;
using IncidentResponseAgent.Infrastructure.Runbooks;
using IncidentResponseAgent.Infrastructure.Tools;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using IncidentResponseAgent.Api.Security;
using IncidentResponseAgent.Api.Services;

namespace IncidentResponseAgent.Api.Controllers;

[ApiController]
[Route("ready")]
[RequestBudget("Readiness")]
public sealed class ReadinessController(
    IOptions<IncidentAnalysisAgentOptions> agentOptions,
    IOptions<RunbookRetrievalOptions> runbookOptions,
    IOptions<OperationalDataOptions> operationalOptions,
    IOptions<IncidentStorageOptions> storageOptions,
    IOptions<IncidentAuthenticationOptions> authenticationOptions,
    IIncidentMonitoringCoordinator monitoring,
    DependencyReadinessProbe probe,
    IConfiguration configuration) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAsync(CancellationToken cancellationToken)
    {
        var agent = agentOptions.Value;
        var runbooks = runbookOptions.Value;
        var operational = operationalOptions.Value;
        var storage = storageOptions.Value;
        var model = HasValue(agent.ApiKey, "IRA_AGENT_API_KEY", "OPENROUTER_API_KEY") && !string.IsNullOrWhiteSpace(agent.Model) ? "configured" : "unavailable";
        var embeddings = (runbooks.EmbeddingProvider.Equals("OpenRouter", StringComparison.OrdinalIgnoreCase)
            ? HasValue(runbooks.ApiKey, "OPENROUTER_API_KEY") : HasValue(runbooks.ApiKey, "HF_TOKEN", "HF_API_TOKEN")) ? "configured" : "unavailable";
        var vectorStore = runbooks.VectorStoreProvider.Equals("Qdrant", StringComparison.OrdinalIgnoreCase)
            ? await probe.QdrantAsync(runbooks, cancellationToken) : "configured";
        var persistence = DependencyReadinessProbe.Persistence([
            storage.IncidentRecordsPath, storage.SessionDatabasePath, runbooks.DatabasePath, runbooks.SourceRegistryPath,
            operational.ProjectRegistryPath, operational.LogEntriesPath, operational.MetricSamplesPath, configuration["Monitoring:StatePath"],
            runbooks.KnowledgeBasePath is { Length: > 0 } knowledge ? Path.Combine(knowledge, "probe") : null,
            configuration["Storage:DataProtectionKeysPath"] is { Length: > 0 } keys ? Path.Combine(keys, "probe") : null]);
        var monitor = await monitoring.GetStateAsync(cancellationToken: cancellationToken);
        var logs = SourceState(operational.LogEntriesPath);
        var metrics = SourceState(operational.MetricSamplesPath);
        var auth = !authenticationOptions.Value.AllowDevelopmentIdentity && !string.IsNullOrWhiteSpace(authenticationOptions.Value.Authority)
            && !string.IsNullOrWhiteSpace(authenticationOptions.Value.Audience) ? "configured" : "unavailable";
        var unavailable = new[] { model, embeddings, vectorStore, persistence, auth, logs, metrics }.Contains("unavailable");
        // No paid inference, embedding or Auth0 requests in a readiness probe. Configuration is not evidence of working credentials/model access.
        return StatusCode(StatusCodes.Status503ServiceUnavailable, new
        {
            status = unavailable ? "unavailable" : "degraded",
            reason = "External authentication, model and embedding execution are not verified by this probe.",
            components = new
            {
                authentication = new { status = auth },
                model = new { status = model, executionVerified = false },
                embeddings = new { status = embeddings, executionVerified = false },
                vectorStore = new { status = vectorStore, collectionVerified = false },
                persistence = new { status = persistence, databaseIntegrityVerified = false },
                telemetry = new { status = logs == "configured" && metrics == "configured" ? "configured" : "degraded", logs, metrics, provenanceVerified = false },
                monitoring = new { status = monitor.LastError is not null ? "degraded" : monitor.Enabled ? "running" : "paused", monitor.LastScan }
            }
        });
    }

    private static bool HasValue(string? configured, params string[] names) =>
        !string.IsNullOrWhiteSpace(configured) || names.Any(name => !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(name)));
    private static string SourceState(string? path) => string.IsNullOrWhiteSpace(path) ? "demo" : System.IO.File.Exists(path) ? "configured" : "unavailable";
}
