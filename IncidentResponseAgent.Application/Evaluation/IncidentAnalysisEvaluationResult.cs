namespace IncidentResponseAgent.Application.Evaluation;

public sealed record IncidentAnalysisEvaluationResult
{
	public required string ScenarioName { get; init; }

	public required decimal Score { get; init; }

	public IReadOnlyList<string> PassedChecks { get; init; } = Array.Empty<string>();

	public IReadOnlyList<string> FailedChecks { get; init; } = Array.Empty<string>();
}
