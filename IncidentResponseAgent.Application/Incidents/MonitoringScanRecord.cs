namespace IncidentResponseAgent.Application.Incidents;

public sealed record MonitoringScanRecord
{
	public Guid Id { get; init; } = Guid.NewGuid();

	public required DateTimeOffset StartedAtUtc { get; init; }

	public required DateTimeOffset CompletedAtUtc { get; init; }

	public int CandidateCount { get; init; }

	public string Status { get; init; } = "completed";
}
