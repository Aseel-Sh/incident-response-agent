namespace IncidentResponseAgent.Application.Incidents;

public interface IIncidentSignalMonitor
{
	Task<IReadOnlyList<DetectedIncidentCandidate>> DetectAsync(CancellationToken cancellationToken = default);
}
