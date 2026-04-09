using IncidentResponseAgent.Application.Tools;

namespace IncidentResponseAgent.Infrastructure.Tools;

public sealed class FakeMetricsProvider : IMetricsProvider
{
	public Task<MetricsQueryResult> QueryAsync(MetricsQueryRequest request, CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(request);
		cancellationToken.ThrowIfCancellationRequested();

		if (string.IsNullOrWhiteSpace(request.MetricName))
		{
			throw new ArgumentException("Metric name cannot be empty.", nameof(request));
		}

		var endTime = request.EndTime ?? DateTimeOffset.UtcNow;
		var serviceName = string.IsNullOrWhiteSpace(request.ServiceName) ? "platform" : request.ServiceName;
		var environment = string.IsNullOrWhiteSpace(request.Environment) ? "unspecified" : request.Environment;

		var samples = new List<MetricSample>
		{
			new()
			{
				Timestamp = endTime.AddMinutes(-3),
				Value = 18.4m
			},
			new()
			{
				Timestamp = endTime.AddMinutes(-2),
				Value = 27.1m
			},
			new()
			{
				Timestamp = endTime.AddMinutes(-1),
				Value = 41.7m
			}
		};

		return Task.FromResult(new MetricsQueryResult
		{
			MetricName = $"{request.MetricName} ({serviceName}/{environment})",
			Samples = samples
		});
	}
}