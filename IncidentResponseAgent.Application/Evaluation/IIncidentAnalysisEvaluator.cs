using IncidentResponseAgent.Application.Incidents;

namespace IncidentResponseAgent.Application.Evaluation;

public interface IIncidentAnalysisEvaluator
{
	IncidentAnalysisEvaluationResult Evaluate(
		IncidentAnalysisEvaluationScenario scenario,
		IncidentAnalysisResult result);
}
