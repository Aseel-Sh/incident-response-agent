namespace IncidentResponseAgent.Application.Incidents;

public interface IGetRecentIncidentAnalysesUseCase
{
	Task<IReadOnlyList<GetRecentIncidentAnalysesResult>> ExecuteAsync(int maxResults = 10, CancellationToken cancellationToken = default);
}