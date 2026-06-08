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
			IncidentSeverity.High,
			serviceName: "checkout-api",
			environment: "production");
		var scenario = new IncidentAnalysisEvaluationScenario
		{
			Name = "checkout failure",
			Incident = incident,
			ExpectedEvidenceSignals = ["rag.runbook", "tool.logs"],
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
}
