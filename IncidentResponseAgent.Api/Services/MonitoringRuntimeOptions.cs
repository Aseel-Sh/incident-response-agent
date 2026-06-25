namespace IncidentResponseAgent.Api.Services;

public sealed class MonitoringRuntimeOptions
{
	public bool Enabled { get; init; } = true;
	public int PollingIntervalSeconds { get; init; } = 30;
	public int StartupDelaySeconds { get; init; } = 5;
	public string? StatePath { get; init; }
}
