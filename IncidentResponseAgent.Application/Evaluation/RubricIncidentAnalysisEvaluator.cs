using IncidentResponseAgent.Application.Incidents;

namespace IncidentResponseAgent.Application.Evaluation;

public sealed class RubricIncidentAnalysisEvaluator : IIncidentAnalysisEvaluator
{
	public IncidentAnalysisEvaluationResult Evaluate(
		IncidentAnalysisEvaluationScenario scenario,
		IncidentAnalysisResult result)
	{
		ArgumentNullException.ThrowIfNull(scenario);
		ArgumentNullException.ThrowIfNull(result);

		var passed = new List<string>();
		var failed = new List<string>();

		Check(
			result.Evidence.Count > 0,
			"analysis includes evidence",
			passed,
			failed);
		Check(
			result.Hypotheses.Count > 0,
			"analysis includes root-cause hypotheses",
			passed,
			failed);
		Check(
			result.RecommendedActions.Count > 0,
			"analysis includes recommended actions",
			passed,
			failed);
		Check(
			!string.IsNullOrWhiteSpace(result.Confidence),
			"analysis includes confidence",
			passed,
			failed);

		foreach (var expectedSignal in scenario.ExpectedEvidenceSignals)
		{
			Check(
				Contains(result.Evidence.Select(evidence => $"{evidence.Source} {evidence.Summary} {evidence.Details}"), expectedSignal),
				$"evidence mentions '{expectedSignal}'",
				passed,
				failed);
		}

		foreach (var expectedTheme in scenario.ExpectedHypothesisThemes)
		{
			Check(
				Contains(result.Hypotheses.Select(hypothesis => $"{hypothesis.Description} {hypothesis.InferenceStrength} {hypothesis.Confidence} {string.Join(' ', hypothesis.SupportingEvidence)} {string.Join(' ', hypothesis.EvidenceReferences)}"), expectedTheme),
				$"hypotheses mention '{expectedTheme}'",
				passed,
				failed);
		}

		foreach (var expectedTheme in scenario.ExpectedActionThemes)
		{
			Check(
				Contains(result.RecommendedActions.Select(action => $"{action.Description} {action.Rationale}"), expectedTheme),
				$"actions mention '{expectedTheme}'",
				passed,
				failed);
		}

		var total = passed.Count + failed.Count;
		var score = total == 0 ? 0 : decimal.Round((decimal)passed.Count / total, 2);

		return new IncidentAnalysisEvaluationResult
		{
			ScenarioName = scenario.Name,
			Score = score,
			PassedChecks = passed,
			FailedChecks = failed
		};
	}

	private static bool Contains(IEnumerable<string> values, string expected)
	{
		return values.Any(value => value.Contains(expected, StringComparison.OrdinalIgnoreCase));
	}

	private static void Check(bool condition, string description, List<string> passed, List<string> failed)
	{
		if (condition)
		{
			passed.Add(description);
		}
		else
		{
			failed.Add(description);
		}
	}
}
