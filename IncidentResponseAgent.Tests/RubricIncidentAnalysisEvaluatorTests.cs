using IncidentResponseAgent.Application.Evaluation;
using IncidentResponseAgent.Application.Incidents;
using IncidentResponseAgent.Domain.Incidents;

namespace IncidentResponseAgent.Tests;

public sealed class RubricIncidentAnalysisEvaluatorTests
{
	[Fact]
	public void EvaluateScoresExpectedSignalsAndThemes()
	{
		var incident = new Incident(
			Guid.NewGuid(),
			"Checkout 5xx spike",
			"Customers see intermittent 500 responses.",
			IncidentSeverity.Sev2,
			serviceName: "checkout-api",
			environment: "production");
		var scenario = new IncidentAnalysisEvaluationScenario
		{
			Name = "checkout failure",
			Incident = incident,
			ExpectedEvidenceSignals = ["rag.runbook", "tool.logs"],
			ExpectedHypothesisThemes = ["regression"],
			ExpectedActionThemes = ["blast radius"]
		};
		var result = new IncidentAnalysisResult
		{
			IncidentId = incident.Id,
			IncidentSummary = "High incident reported for checkout-api.",
			AnalysisText = "Summary",
			Evidence =
			[
				new IncidentAnalysisEvidenceItem { Source = "rag.runbook.checkout", Summary = "Checkout triage", Details = "runbook" },
				new IncidentAnalysisEvidenceItem { Source = "tool.logs", Summary = "500 response", Details = "error" }
			],
			Hypotheses = [new IncidentHypothesis { Description = "Recent regression", Confidence = "Medium", InferenceStrength = "Medium" }],
			RecommendedActions = [new IncidentActionRecommendation { Description = "Confirm blast radius", Priority = "High", Rationale = "Scope first." }],
			Confidence = "Medium"
		};

		var evaluation = new RubricIncidentAnalysisEvaluator().Evaluate(scenario, result);

		Assert.Equal(1.00m, evaluation.Score);
		Assert.Empty(evaluation.FailedChecks);
	}

	[Fact]
	public void EvaluateFailsMissingExpectedHypothesisThemes()
	{
		var incident = new Incident(
			Guid.NewGuid(),
			"Orders queue backlog growing",
			"Queue depth is increasing.",
			IncidentSeverity.Sev3,
			serviceName: "orders-worker",
			environment: "production");
		var scenario = new IncidentAnalysisEvaluationScenario
		{
			Name = "queue backlog",
			Incident = incident,
			ExpectedEvidenceSignals = ["tool.metrics"],
			ExpectedHypothesisThemes = ["queue"],
			ExpectedActionThemes = ["runbook"]
		};
		var result = new IncidentAnalysisResult
		{
			IncidentId = incident.Id,
			IncidentSummary = "Medium incident reported for orders-worker.",
			AnalysisText = "Summary",
			Evidence = [new IncidentAnalysisEvidenceItem { Source = "tool.metrics", Summary = "queue_depth sample", Details = "900" }],
			Hypotheses = [new IncidentHypothesis { Description = "Generic operational issue", Confidence = "Low", InferenceStrength = "Weak" }],
			RecommendedActions = [new IncidentActionRecommendation { Description = "Follow the runbook", Priority = "High", Rationale = "Use known steps." }],
			Confidence = "Low"
		};

		var evaluation = new RubricIncidentAnalysisEvaluator().Evaluate(scenario, result);

		Assert.Contains("hypotheses mention 'queue'", evaluation.FailedChecks);
		Assert.True(evaluation.Score < 1.00m);
	}
}
