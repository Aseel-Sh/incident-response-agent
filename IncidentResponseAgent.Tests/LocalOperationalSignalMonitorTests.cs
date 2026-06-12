using IncidentResponseAgent.Domain.Incidents;
using IncidentResponseAgent.Infrastructure.Incidents;
using IncidentResponseAgent.Infrastructure.Tools;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace IncidentResponseAgent.Tests;

public sealed class LocalOperationalSignalMonitorTests : IDisposable
{
	private readonly string _rootPath = Path.Combine(Path.GetTempPath(), "ira-monitor-tests", Guid.NewGuid().ToString("N"));

	[Fact]
	public async Task DetectAsyncReturnsMetricAndLogCandidates()
	{
		var options = Options.Create(new OperationalDataOptions());
		var metricsProvider = new LocalJsonMetricsProvider(options, NullLogger<LocalJsonMetricsProvider>.Instance);
		var monitor = new LocalOperationalSignalMonitor(
			new LocalJsonLogSearchProvider(options, NullLogger<LocalJsonLogSearchProvider>.Instance),
			metricsProvider,
			options);

		var candidates = await monitor.DetectAsync();

		Assert.Contains(candidates, candidate =>
			candidate.ServiceName == "checkout-api" &&
			candidate.Source.Contains("metrics", StringComparison.OrdinalIgnoreCase) &&
			candidate.Source.Contains("logs", StringComparison.OrdinalIgnoreCase) &&
			candidate.Severity == IncidentSeverity.Critical);
		Assert.Contains(candidates, candidate =>
			candidate.ServiceName == "orders-worker" &&
			candidate.Signals.Any(signal => signal.Contains("queue_depth", StringComparison.OrdinalIgnoreCase)));
		Assert.All(candidates, candidate => Assert.False(string.IsNullOrWhiteSpace(candidate.Id)));
	}

	[Fact]
	public async Task DetectAsyncScansConfiguredMetricSeriesForNewServices()
	{
		Directory.CreateDirectory(_rootPath);
		var logsPath = Path.Combine(_rootPath, "logs.json");
		var metricsPath = Path.Combine(_rootPath, "metrics.json");
		await File.WriteAllTextAsync(logsPath, "[]");
		await File.WriteAllTextAsync(
			metricsPath,
			"""
			[
			  {
			    "metricName": "request_error_rate",
			    "serviceName": "inventory-api",
			    "environment": "production",
			    "samples": [
			      { "timestamp": "2026-06-11T12:00:00+00:00", "value": 44.1 }
			    ]
			  }
			]
			""");

		var options = Options.Create(new OperationalDataOptions
		{
			LogEntriesPath = logsPath,
			MetricSamplesPath = metricsPath
		});
		var metricsProvider = new LocalJsonMetricsProvider(options, NullLogger<LocalJsonMetricsProvider>.Instance);
		var monitor = new LocalOperationalSignalMonitor(
			new LocalJsonLogSearchProvider(options, NullLogger<LocalJsonLogSearchProvider>.Instance),
			metricsProvider,
			options);

		var candidates = await monitor.DetectAsync();

		Assert.Contains(candidates, candidate =>
			candidate.ServiceName == "inventory-api" &&
			candidate.Environment == "production" &&
			candidate.Severity == IncidentSeverity.Critical &&
			candidate.Signals.Any(signal => signal.Contains("request_error_rate=44.1", StringComparison.OrdinalIgnoreCase)));
	}

	public void Dispose()
	{
		if (Directory.Exists(_rootPath))
		{
			Directory.Delete(_rootPath, recursive: true);
		}
	}
}
