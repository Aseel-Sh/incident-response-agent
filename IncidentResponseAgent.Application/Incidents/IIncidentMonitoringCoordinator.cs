namespace IncidentResponseAgent.Application.Incidents;

public interface IIncidentMonitoringCoordinator
{
	Task<IncidentMonitoringState> GetStateAsync(string? projectId = null, CancellationToken cancellationToken = default);
	Task<IncidentMonitoringState> PauseAsync(CancellationToken cancellationToken = default);
	Task<IncidentMonitoringState> ResumeAsync(CancellationToken cancellationToken = default);
	Task<IncidentMonitoringState> SetPollingIntervalAsync(int seconds, CancellationToken cancellationToken = default);
	Task<IncidentMonitoringState> ScanNowAsync(string? projectId = null, CancellationToken cancellationToken = default);
}

public sealed record IncidentMonitoringState
{
	public bool Enabled { get; init; }
	public int PollingIntervalSeconds { get; init; }
	public bool ScanInProgress { get; init; }
	public MonitoringScanRecord? LastScan { get; init; }
	public string? LastError { get; init; }
}
