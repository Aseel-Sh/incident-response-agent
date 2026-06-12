using IncidentResponseAgent.Domain.Incidents;

namespace IncidentResponseAgent.Application.Evaluation;

public sealed record IncidentAnalysisEvaluationScenario
{
	public required string Name { get; init; }

	public required Incident Incident { get; init; }

	public IReadOnlyList<string> ExpectedEvidenceSignals { get; init; } = Array.Empty<string>();

	public IReadOnlyList<string> ExpectedHypothesisThemes { get; init; } = Array.Empty<string>();

	public IReadOnlyList<string> ExpectedActionThemes { get; init; } = Array.Empty<string>();
}
