namespace IncidentResponseAgent.Api.Contracts.Incidents;

public sealed record IncidentStatusRequest
{
	public string Status { get; init; } = "active";
}
