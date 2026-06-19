namespace IncidentResponseAgent.Application.Incidents;

public sealed record IncidentTimelineEvent
{
	public required string Type { get; init; }

	public required DateTimeOffset OccurredAtUtc { get; init; }

	public required string Summary { get; init; }

	public string Actor { get; init; } = "system";

	public string? EvidenceReference { get; init; }
}
