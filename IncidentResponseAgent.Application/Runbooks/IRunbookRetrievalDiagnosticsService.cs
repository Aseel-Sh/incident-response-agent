namespace IncidentResponseAgent.Application.Runbooks;

public interface IRunbookRetrievalDiagnosticsService
{
	Task<RunbookRetrievalDiagnosticsResult> SearchAsync(
		RunbookRetrievalDiagnosticsRequest request,
		CancellationToken cancellationToken = default);
}
