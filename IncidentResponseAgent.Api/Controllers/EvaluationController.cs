using IncidentResponseAgent.Api.Contracts.Evaluation;
using IncidentResponseAgent.Application.Evaluation;
using Microsoft.AspNetCore.Mvc;

namespace IncidentResponseAgent.Api.Controllers;

[ApiController]
[Route("api/evaluation")]
public sealed class EvaluationController : ControllerBase
{
	[HttpGet("scenarios")]
	[ProducesResponseType(typeof(IReadOnlyList<EvaluationScenarioResponse>), StatusCodes.Status200OK)]
	public ActionResult<IReadOnlyList<EvaluationScenarioResponse>> GetScenarios()
	{
		var scenarios = IncidentAnalysisEvaluationScenarioCatalog.BuiltInScenarios
			.Select(scenario => new EvaluationScenarioResponse
			{
				Name = scenario.Name,
				Title = scenario.Incident.Title,
				Description = scenario.Incident.Description,
				Severity = scenario.Incident.Severity.ToString(),
				ServiceName = scenario.Incident.ServiceName,
				Environment = scenario.Incident.Environment,
				Tags = scenario.Incident.Tags,
				ExpectedEvidenceSignals = scenario.ExpectedEvidenceSignals,
				ExpectedHypothesisThemes = scenario.ExpectedHypothesisThemes,
				ExpectedActionThemes = scenario.ExpectedActionThemes
			})
			.ToArray();

		return Ok(scenarios);
	}
}
