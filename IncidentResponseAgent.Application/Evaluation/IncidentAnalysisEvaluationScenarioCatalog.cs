using IncidentResponseAgent.Domain.Incidents;

namespace IncidentResponseAgent.Application.Evaluation;

public static class IncidentAnalysisEvaluationScenarioCatalog
{
	public static IReadOnlyList<IncidentAnalysisEvaluationScenario> BuiltInScenarios { get; } =
	[
		Scenario(1, "database-latency-runbook", "Database latency with matching runbook", "Database connection acquisition exceeds 3200ms and checkout requests are queuing.", IncidentSeverity.Sev2, "database-api", ["database", "latency", "pool"], ["rag.runbook", "tool.metrics"], ["database", "latency"], ["pool", "runbook"]),
		Scenario(2, "checkout-5xx-regression", "Checkout 5xx spike", "Customers are seeing intermittent HTTP 500 responses after the latest deployment.", IncidentSeverity.Sev2, "checkout-api", ["checkout", "5xx", "deployment"], ["rag.runbook", "tool.logs", "tool.metrics"], ["regression", "checkout"], ["blast radius", "runbook"]),
		Scenario(3, "missing-logs-useful-metrics", "API latency without logs", "Metrics show sustained p95 latency while the log source is unavailable.", IncidentSeverity.Sev3, "catalog-api", ["latency", "missing-logs"], ["tool.metrics"], ["latency"], ["metric", "logs"]),
		Scenario(4, "conflicting-evidence", "Conflicting checkout signals", "Error-rate metrics are elevated but application logs report successful requests.", IncidentSeverity.Sev3, "checkout-api", ["conflicting", "metrics", "logs"], ["tool.logs", "tool.metrics"], ["conflict"], ["validate", "correlate"]),
		Scenario(5, "false-positive-warning", "Harmless cache warmup warning", "A single expected cache warmup warning occurred during maintenance with no customer impact.", IncidentSeverity.Sev5, "catalog-api", ["warning", "maintenance"], ["tool.logs"], ["maintenance"], ["verify", "monitor"]),
		Scenario(6, "prior-failed-action", "Recurring database pool exhaustion", "Connection acquisition is timing out; restarting the primary failed during a similar incident.", IncidentSeverity.Sev2, "database-api", ["database", "recurring", "failed-action"], ["history.incident", "tool.metrics"], ["pool"], ["failed", "do not repeat"]),
		Scenario(7, "prior-successful-action", "Recurring queue backlog", "Order processing queue depth is rising; scaling consumers worked previously.", IncidentSeverity.Sev3, "orders-worker", ["queue", "successful-action"], ["history.incident", "tool.metrics"], ["queue"], ["scale", "consumer"]),
		Scenario(8, "no-relevant-runbook", "Unknown image processor failure", "The image processor is returning an undocumented codec error.", IncidentSeverity.Sev3, "image-worker", ["codec", "no-runbook"], ["tool.logs"], ["codec"], ["collect", "validate"]),
		Scenario(9, "rag-provider-unavailable", "Checkout dependency degradation", "Checkout latency increased while the embedding provider is unavailable.", IncidentSeverity.Sev2, "checkout-api", ["rag-unavailable", "latency"], ["tool.metrics"], ["dependency"], ["validate", "latency"]),
		Scenario(10, "model-unavailable", "Authentication error burst", "Authentication requests show a sustained error-rate burst while the model provider is unavailable.", IncidentSeverity.Sev2, "auth-api", ["model-unavailable", "error-rate"], ["tool.logs", "tool.metrics"], ["authentication"], ["contain", "validate"]),
		Scenario(11, "insufficient-evidence", "Unverified customer report", "A user reports intermittent slowness but no logs or metrics are available.", IncidentSeverity.Sev4, "unknown-service", ["insufficient-evidence"], ["tool.logs"], ["unverified"], ["collect", "metric"]),
		Scenario(12, "duplicate-active-incident", "Repeated checkout 5xx alert", "The same checkout 5xx condition is already represented by an active incident.", IncidentSeverity.Sev2, "checkout-api", ["duplicate", "checkout"], ["history.incident"], ["duplicate"], ["merge", "existing"])
	];

	private static IncidentAnalysisEvaluationScenario Scenario(int id, string name, string title, string description, IncidentSeverity severity, string service, string[] tags, string[] evidence, string[] hypotheses, string[] actions) =>
		new()
		{
			Name = name,
			Incident = new Incident(new Guid(id, 0, 0, new byte[8]), title, description, severity, service, "production", tags: tags),
			ExpectedEvidenceSignals = evidence,
			ExpectedHypothesisThemes = hypotheses,
			ExpectedActionThemes = actions
		};
}
