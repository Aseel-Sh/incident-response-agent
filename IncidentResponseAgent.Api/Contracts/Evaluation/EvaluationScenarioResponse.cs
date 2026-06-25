namespace IncidentResponseAgent.Api.Contracts.Evaluation;

public sealed record EvaluationScenarioResponse
{
	public required string Name { get; init; }

	public required string Title { get; init; }

	public required string Description { get; init; }

	public required string Severity { get; init; }

	public string? ServiceName { get; init; }

	public string? Environment { get; init; }

	public IReadOnlyCollection<string> Tags { get; init; } = Array.Empty<string>();

	public IReadOnlyCollection<string> ExpectedEvidenceSignals { get; init; } = Array.Empty<string>();

	public IReadOnlyCollection<string> ExpectedHypothesisThemes { get; init; } = Array.Empty<string>();

	public IReadOnlyCollection<string> ExpectedActionThemes { get; init; } = Array.Empty<string>();
}
