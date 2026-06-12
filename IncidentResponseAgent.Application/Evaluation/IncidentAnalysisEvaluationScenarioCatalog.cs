using IncidentResponseAgent.Domain.Incidents;

namespace IncidentResponseAgent.Application.Evaluation;

public static class IncidentAnalysisEvaluationScenarioCatalog
{
	public static IReadOnlyList<IncidentAnalysisEvaluationScenario> BuiltInScenarios { get; } =
	[
		new IncidentAnalysisEvaluationScenario
		{
			Name = "checkout-5xx-regression",
			Incident = new Incident(
				Guid.Parse("11111111-1111-1111-1111-111111111111"),
				"Checkout 5xx spike",
				"Customers are seeing intermittent 500 responses during checkout after the latest deployment.",
				IncidentSeverity.High,
				serviceName: "checkout-api",
				environment: "production",
				tags: ["checkout", "5xx", "latency"]),
			ExpectedEvidenceSignals = ["rag.runbook", "tool.logs", "tool.metrics"],
			ExpectedHypothesisThemes = ["regression", "checkout"],
			ExpectedActionThemes = ["blast radius", "runbook"]
		},
		new IncidentAnalysisEvaluationScenario
		{
			Name = "queue-backlog-growth",
			Incident = new Incident(
				Guid.Parse("22222222-2222-2222-2222-222222222222"),
				"Orders queue backlog growing",
				"Order processing is falling behind and queue depth is increasing faster than consumers can drain it.",
				IncidentSeverity.Medium,
				serviceName: "orders-worker",
				environment: "production",
				tags: ["queue", "backlog", "worker"]),
			ExpectedEvidenceSignals = ["rag.runbook", "tool.logs", "tool.metrics"],
			ExpectedHypothesisThemes = ["queue", "orders-worker"],
			ExpectedActionThemes = ["metrics", "runbook"]
		}
	];
}
