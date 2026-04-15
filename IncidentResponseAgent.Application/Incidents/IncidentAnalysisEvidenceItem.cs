namespace IncidentResponseAgent.Application.Incidents;

public sealed record IncidentAnalysisEvidenceItem
{
	public required string Summary { get; init; }

	public string? Source { get; init; }

	public string? Details { get; init; }
}