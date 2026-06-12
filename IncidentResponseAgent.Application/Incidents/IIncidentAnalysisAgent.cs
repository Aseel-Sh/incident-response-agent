using IncidentResponseAgent.Domain.Incidents;

namespace IncidentResponseAgent.Application.Incidents;

public interface IIncidentAnalysisAgent
{
	Task<IncidentAgentExecutionResult> AnalyzeAsync(
		Incident incident,
		IncidentAnalysisSessionContext? sessionContext = null,
		IncidentAnalysisAgentContext? agentContext = null,
		CancellationToken cancellationToken = default);
}
