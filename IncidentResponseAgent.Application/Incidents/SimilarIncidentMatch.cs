namespace IncidentResponseAgent.Application.Incidents;

public sealed record SimilarIncidentMatch
{
	public required Guid IncidentId { get; init; }

	public required string IncidentSummary { get; init; }

	public required string ServiceName { get; init; }

	public required string Environment { get; init; }

	public required string ResolutionSummary { get; init; }

	public required double Score { get; init; }

	public required DateTimeOffset CreatedAtUtc { get; init; }

	public IReadOnlyList<string> SharedSignals { get; init; } = Array.Empty<string>();
}
