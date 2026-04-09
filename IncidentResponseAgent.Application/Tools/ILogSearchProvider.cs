namespace IncidentResponseAgent.Application.Tools;

public interface ILogSearchProvider
{
	Task<LogSearchResult> SearchAsync(LogSearchRequest request, CancellationToken cancellationToken = default);
}