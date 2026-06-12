using IncidentResponseAgent.Application.Tools;
using IncidentResponseAgent.Infrastructure.Tools;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace IncidentResponseAgent.Tests;

public sealed class LocalJsonOperationalDataProviderTests : IDisposable
{
	private readonly string _rootPath = Path.Combine(Path.GetTempPath(), "ira-operational-tests", Guid.NewGuid().ToString("N"));

	[Fact]
	public async Task LogSearchReturnsEmptyWhenNoMatchesAndFallbacksDisabled()
	{
		var logsPath = Path.Combine(_rootPath, "logs.json");
		Directory.CreateDirectory(_rootPath);
		await File.WriteAllTextAsync(logsPath, "[]");
		var provider = CreateLogProvider(logsPath);

		var result = await provider.SearchAsync(new LogSearchRequest
		{
			Query = "checkout 500",
			MaxResults = 5
		});

		Assert.Empty(result.Entries);
	}

	[Fact]
	public async Task LogSearchIncludesRelatedDependencyLogsWhenServiceTokenMatchesMessage()
	{
		var logsPath = Path.Combine(_rootPath, "related-logs.json");
		Directory.CreateDirectory(_rootPath);
		await File.WriteAllTextAsync(
			logsPath,
			"""
			[
			  {
			    "timestamp": "2026-06-09T12:33:00+00:00",
			    "source": "payment-gateway-client",
			    "level": "Error",
			    "message": "production dependency timeout during checkout payment authorization",
			    "correlationId": "corr-checkout-003"
			  }
			]
			""");
		var provider = CreateLogProvider(logsPath);

		var result = await provider.SearchAsync(new LogSearchRequest
		{
			Query = "checkout payment timeout",
			ServiceName = "checkout-api",
			Environment = "production",
			MaxResults = 5
		});

		Assert.Single(result.Entries);
		Assert.Equal("payment-gateway-client", result.Entries[0].Source);
	}

	[Fact]
	public async Task MetricQueryReturnsEmptyWhenExplicitWindowHasNoSamples()
	{
		var metricsPath = Path.Combine(_rootPath, "metrics.json");
		Directory.CreateDirectory(_rootPath);
		await File.WriteAllTextAsync(
			metricsPath,
			"""
			[
			  {
			    "metricName": "request_error_rate",
			    "serviceName": "checkout-api",
			    "environment": "production",
			    "samples": [
			      { "timestamp": "2026-06-09T12:31:00+00:00", "value": 4.2 },
			      { "timestamp": "2026-06-09T12:32:00+00:00", "value": 9.8 }
			    ]
			  }
			]
			""");
		var provider = CreateMetricsProvider(metricsPath);

		var result = await provider.QueryAsync(new MetricsQueryRequest
		{
			MetricName = "request_error_rate",
			ServiceName = "checkout-api",
			Environment = "production",
			StartTime = DateTimeOffset.Parse("2026-06-10T12:31:00+00:00"),
			EndTime = DateTimeOffset.Parse("2026-06-10T12:32:00+00:00")
		});

		Assert.Empty(result.Samples);
	}

	public void Dispose()
	{
		if (Directory.Exists(_rootPath))
		{
			Directory.Delete(_rootPath, recursive: true);
		}
	}

	private static LocalJsonLogSearchProvider CreateLogProvider(string path)
	{
		return new LocalJsonLogSearchProvider(
			Options.Create(new OperationalDataOptions { LogEntriesPath = path }),
			NullLogger<LocalJsonLogSearchProvider>.Instance);
	}

	private static LocalJsonMetricsProvider CreateMetricsProvider(string path)
	{
		return new LocalJsonMetricsProvider(
			Options.Create(new OperationalDataOptions { MetricSamplesPath = path }),
			NullLogger<LocalJsonMetricsProvider>.Instance);
	}
}
