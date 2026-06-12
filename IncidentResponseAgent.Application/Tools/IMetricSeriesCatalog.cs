namespace IncidentResponseAgent.Application.Tools;

public interface IMetricSeriesCatalog
{
	Task<IReadOnlyList<MetricSeries>> ListSeriesAsync(CancellationToken cancellationToken = default);
}
