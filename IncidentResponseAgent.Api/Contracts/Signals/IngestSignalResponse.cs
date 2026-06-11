namespace IncidentResponseAgent.Api.Contracts.Signals;

public sealed record IngestSignalResponse
{
	public required string Status { get; init; }

	public required string Location { get; init; }
}
