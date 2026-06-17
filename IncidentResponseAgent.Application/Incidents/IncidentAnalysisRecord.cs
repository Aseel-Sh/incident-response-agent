using IncidentResponseAgent.Domain.Incidents;

namespace IncidentResponseAgent.Application.Incidents;

public sealed record IncidentAnalysisRecord
{
	public required Incident Incident { get; init; }

	public required IncidentAnalysisResult AnalysisResult { get; init; }

	public string Status { get; init; } = "new";

	public required DateTimeOffset CreatedAtUtc { get; init; }
}
