namespace IncidentResponseAgent.Api.Contracts.Incidents;

public sealed record ActionOutcomeRequest
{
	public string Description { get; init; } = string.Empty;

	public string Status { get; init; } = "worked";
}
