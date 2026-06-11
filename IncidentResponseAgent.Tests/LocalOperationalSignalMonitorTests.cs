using IncidentResponseAgent.Domain.Incidents;
using IncidentResponseAgent.Infrastructure.Incidents;
using IncidentResponseAgent.Infrastructure.Tools;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace IncidentResponseAgent.Tests;

public sealed class LocalOperationalSignalMonitorTests
{
	[Fact]
	public async Task DetectAsyncReturnsMetricAndLogCandidates()
	{
		var options = Options.Create(new OperationalDataOptions());
		var monitor = new LocalOperationalSignalMonitor(
			new LocalJsonLogSearchProvider(options, NullLogger<LocalJsonLogSearchProvider>.Instance),
			new LocalJsonMetricsProvider(options, NullLogger<LocalJsonMetricsProvider>.Instance),
			options);

		var candidates = await monitor.DetectAsync();

		Assert.Contains(candidates, candidate =>
			candidate.ServiceName == "checkout-api" &&
			candidate.Source.Contains("metrics", StringComparison.OrdinalIgnoreCase) &&
			candidate.Severity == IncidentSeverity.Critical);
		Assert.Contains(candidates, candidate =>
			candidate.ServiceName == "orders-worker" &&
			candidate.Signals.Any(signal => signal.Contains("queue_depth", StringComparison.OrdinalIgnoreCase)));
		Assert.All(candidates, candidate => Assert.False(string.IsNullOrWhiteSpace(candidate.Id)));
	}
}
