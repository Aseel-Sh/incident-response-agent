using IncidentResponseAgent.Domain.Incidents;

namespace IncidentResponseAgent.Application.Incidents;

public interface IIncidentAnalysisAgent
{
	Task<string> AnalyzeAsync(Incident incident, CancellationToken cancellationToken = default);
}