using IncidentResponseAgent.Domain.Incidents;

namespace IncidentResponseAgent.Application.Incidents;

public interface IIncidentRecordStore
{
	Task SaveAsync(Incident incident, IncidentAnalysisResult analysisResult, CancellationToken cancellationToken = default);

	Task<IncidentAnalysisRecord?> GetByIncidentIdAsync(Guid incidentId, CancellationToken cancellationToken = default);

	Task<IncidentActionOutcome> AddActionOutcomeAsync(Guid incidentId, string description, string status, CancellationToken cancellationToken = default);

	Task<IReadOnlyList<IncidentAnalysisRecord>> GetRecentAsync(int maxResults, CancellationToken cancellationToken = default);

	Task<IReadOnlyList<SimilarIncidentMatch>> FindSimilarAsync(Incident incident, int maxResults, CancellationToken cancellationToken = default);
}
