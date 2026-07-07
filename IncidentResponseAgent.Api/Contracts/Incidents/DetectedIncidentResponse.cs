namespace IncidentResponseAgent.Api.Contracts.Incidents;

public sealed record DetectedIncidentResponse
{
	public required string Id { get; init; }

	public string ProjectId { get; init; } = "default";

	public required string Title { get; init; }

	public required string Description { get; init; }

	public required string Severity { get; init; }

	public string? ServiceName { get; init; }

	public string? Environment { get; init; }

	public required DateTimeOffset DetectedAtUtc { get; init; }

	public required string Source { get; init; }

	public IReadOnlyList<string> Signals { get; init; } = Array.Empty<string>();

	public IReadOnlyList<string> SuggestedTags { get; init; } = Array.Empty<string>();

	public string Status { get; init; } = "candidate";

	public Guid? DuplicateIncidentId { get; init; }

	public IReadOnlyList<SimilarIncidentResponse> SimilarIncidents { get; init; } = Array.Empty<SimilarIncidentResponse>();

	public IReadOnlyList<IncidentTimelineEventResponse> Timeline { get; init; } = Array.Empty<IncidentTimelineEventResponse>();
}
