using IncidentResponseAgent.Domain.Incidents;

namespace IncidentResponseAgent.Application.Incidents;

public interface IIncidentRecordStore
{
	Task SaveAsync(Incident incident, IncidentAnalysisResult analysisResult, CancellationToken cancellationToken = default);

	Task<IncidentAnalysisRecord?> GetByIncidentIdAsync(Guid incidentId, CancellationToken cancellationToken = default);

	Task<IReadOnlyList<IncidentAnalysisRecord>> GetRecentAsync(int maxResults, CancellationToken cancellationToken = default);
}