using IncidentResponseAgent.Domain.Incidents;

namespace IncidentResponseAgent.Application.Incidents;

public interface IAnalyzeIncidentUseCase
{
	Task<IncidentAnalysisResult> AnalyzeAsync(Incident incident, CancellationToken cancellationToken = default);
}