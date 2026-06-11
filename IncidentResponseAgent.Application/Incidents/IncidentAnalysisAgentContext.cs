using IncidentResponseAgent.Application.Runbooks;
using IncidentResponseAgent.Application.Tools;

namespace IncidentResponseAgent.Application.Incidents;

public sealed record IncidentAnalysisAgentContext
{
	public required RunbookRetrievalResult Runbooks { get; init; }

	public required LogSearchResult Logs { get; init; }

	public required MetricsQueryResult Metrics { get; init; }
}
