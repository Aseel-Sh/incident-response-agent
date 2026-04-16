namespace IncidentResponseAgent.Application.Incidents;

public interface IIncidentAnalysisSessionStore
{
	Task<IncidentAnalysisSessionContext> GetOrCreateAsync(string? sessionId, CancellationToken cancellationToken = default);

	Task SaveAsync(IncidentAnalysisSessionContext sessionContext, CancellationToken cancellationToken = default);
}