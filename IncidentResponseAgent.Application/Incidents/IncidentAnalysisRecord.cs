using IncidentResponseAgent.Domain.Incidents;

namespace IncidentResponseAgent.Application.Incidents;

public sealed record IncidentAnalysisRecord
{
	public required Incident Incident { get; init; }

	public required IncidentAnalysisResult AnalysisResult { get; init; }

	public required DateTimeOffset CreatedAtUtc { get; init; }
}