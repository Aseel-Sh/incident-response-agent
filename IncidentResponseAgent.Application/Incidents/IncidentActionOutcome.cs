namespace IncidentResponseAgent.Application.Incidents;

public sealed record IncidentActionOutcome
{
	public required string Description { get; init; }

	public required string Status { get; init; }

	public required DateTimeOffset LoggedAtUtc { get; init; }
}
