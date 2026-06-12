using IncidentResponseAgent.Application.Evaluation;

namespace IncidentResponseAgent.Tests;

public sealed class IncidentAnalysisEvaluationScenarioCatalogTests
{
	[Fact]
	public void BuiltInScenariosContainExpectedOperationalSignals()
	{
		var scenarios = IncidentAnalysisEvaluationScenarioCatalog.BuiltInScenarios;

		Assert.NotEmpty(scenarios);
		Assert.Contains(scenarios, scenario => scenario.Name == "checkout-5xx-regression");
		Assert.All(scenarios, scenario =>
		{
			Assert.NotEmpty(scenario.ExpectedEvidenceSignals);
			Assert.NotEmpty(scenario.ExpectedHypothesisThemes);
			Assert.NotEmpty(scenario.ExpectedActionThemes);
			Assert.NotEqual(Guid.Empty, scenario.Incident.Id);
		});
	}
}
