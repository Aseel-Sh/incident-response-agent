namespace IncidentResponseAgent.Application.Runbooks;

public interface IRunbookRetrievalService
{
	Task<RunbookRetrievalResult> RetrieveAsync(RunbookRetrievalRequest request, CancellationToken cancellationToken = default);
}