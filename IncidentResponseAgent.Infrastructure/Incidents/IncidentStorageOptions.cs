namespace IncidentResponseAgent.Infrastructure.Incidents;

public sealed class IncidentStorageOptions
{
	public string? SessionDatabasePath { get; init; }

	public string? IncidentRecordsPath { get; init; }
}
