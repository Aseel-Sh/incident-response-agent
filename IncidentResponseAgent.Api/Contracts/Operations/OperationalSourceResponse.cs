namespace IncidentResponseAgent.Api.Contracts.Operations;

public sealed record OperationalSourceResponse
{
	public required string Name { get; init; }

	public required string Type { get; init; }

	public required string Mode { get; init; }

	public required string Location { get; init; }

	public required string Status { get; init; }

	public required string Description { get; init; }

	public bool IsDemoMode { get; init; }

	public IReadOnlyList<string> Capabilities { get; init; } = Array.Empty<string>();
}
