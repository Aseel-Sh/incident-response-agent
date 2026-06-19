using IncidentResponseAgent.Domain.Incidents;

namespace IncidentResponseAgent.Application.Incidents;

public interface IIncidentRecordStore
{
	Task SaveAsync(Incident incident, IncidentAnalysisResult analysisResult, CancellationToken cancellationToken = default);

	Task SaveCandidatesAsync(IReadOnlyList<DetectedIncidentCandidate> candidates, MonitoringScanRecord scan, CancellationToken cancellationToken = default);

	Task<IReadOnlyList<DetectedIncidentCandidate>> GetCandidatesAsync(CancellationToken cancellationToken = default);

	Task<Incident> ConfirmCandidateAsync(string candidateId, CancellationToken cancellationToken = default);

	Task<DetectedIncidentCandidate> DecideCandidateAsync(string candidateId, string decision, Guid? mergeIntoIncidentId = null, CancellationToken cancellationToken = default);

	Task<IncidentAnalysisRecord> AddTimelineEventAsync(Guid incidentId, IncidentTimelineEvent timelineEvent, CancellationToken cancellationToken = default);

	Task<ProposedKnowledgeUpdate> ReviewKnowledgeUpdateAsync(Guid incidentId, string decision, string? content, string? notes, CancellationToken cancellationToken = default);

	Task<MonitoringScanRecord?> GetLastScanAsync(CancellationToken cancellationToken = default);

	Task<IncidentAnalysisRecord?> GetByIncidentIdAsync(Guid incidentId, CancellationToken cancellationToken = default);

	Task<string> UpdateStatusAsync(Guid incidentId, string status, CancellationToken cancellationToken = default);

	Task<bool> DeleteAsync(Guid incidentId, CancellationToken cancellationToken = default);

	Task<IncidentActionOutcome> AddActionOutcomeAsync(Guid incidentId, string description, string status, CancellationToken cancellationToken = default);

	Task<IncidentAnalysisFeedback> AddFeedbackAsync(Guid incidentId, IncidentAnalysisFeedback feedback, CancellationToken cancellationToken = default);

	Task<IReadOnlyList<IncidentAnalysisRecord>> GetRecentAsync(int maxResults, CancellationToken cancellationToken = default);

	Task<IReadOnlyList<SimilarIncidentMatch>> FindSimilarAsync(Incident incident, int maxResults, CancellationToken cancellationToken = default);
}
