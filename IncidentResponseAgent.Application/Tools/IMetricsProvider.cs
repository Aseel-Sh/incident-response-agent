namespace IncidentResponseAgent.Application.Tools;

public interface IMetricsProvider
{
	Task<MetricsQueryResult> QueryAsync(MetricsQueryRequest request, CancellationToken cancellationToken = default);
}