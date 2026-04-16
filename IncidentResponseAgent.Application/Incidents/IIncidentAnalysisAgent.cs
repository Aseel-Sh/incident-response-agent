using IncidentResponseAgent.Domain.Incidents;

namespace IncidentResponseAgent.Application.Incidents;

public interface IIncidentAnalysisAgent
{
	Task<string> AnalyzeAsync(
		Incident incident,
		IncidentAnalysisSessionContext? sessionContext = null,
		CancellationToken cancellationToken = default);
}