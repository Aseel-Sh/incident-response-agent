using IncidentResponseAgent.Application.Runbooks;
using IncidentResponseAgent.Domain.Runbooks;

namespace IncidentResponseAgent.Infrastructure.Runbooks;

public sealed class InMemoryRunbookRetrievalService : IRunbookRetrievalService
{
	private static readonly IReadOnlyList<RunbookDocument> Runbooks =
	[
		new RunbookDocument(
			"runbook-checkout-errors",
			"Checkout API 5xx Triage",
			"Steps for investigating checkout failures and server-side errors.",
			"Check recent deployments, inspect gateway and downstream dependency logs, and validate database health.",
			["checkout", "payments", "errors", "production"]),
		new RunbookDocument(
			"runbook-latency-spike",
			"API Latency Spike Investigation",
			"Guidance for investigating elevated request latency.",
			"Review saturation metrics, compare latency by dependency, and confirm whether cache or database contention is present.",
			["latency", "performance", "production"]),
		new RunbookDocument(
			"runbook-dependency-outage",
			"Dependency Outage Response",
			"Playbook for downstream service outages.",
			"Identify impacted dependencies, confirm retry behavior, and decide whether failover or feature flag mitigation is needed.",
			["dependency", "outage", "mitigation"])
	];

	public Task<RunbookRetrievalResult> RetrieveAsync(RunbookRetrievalRequest request, CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(request);
		cancellationToken.ThrowIfCancellationRequested();

		if (string.IsNullOrWhiteSpace(request.Query))
		{
			throw new ArgumentException("Runbook query cannot be empty.", nameof(request));
		}

		var maxResults = request.MaxResults <= 0 ? 1 : Math.Min(request.MaxResults, 5);
		var query = request.Query.Trim();
		var serviceName = request.ServiceName?.Trim();
		var environment = request.Environment?.Trim();

		var matches = Runbooks
			.Where(runbook => IsMatch(runbook, query, serviceName, environment))
			.Take(maxResults)
			.ToArray();

		if (matches.Length == 0)
		{
			matches = Runbooks
				.Take(maxResults)
				.ToArray();
		}

		return Task.FromResult(new RunbookRetrievalResult
		{
			Runbooks = matches
		});
	}

	private static bool IsMatch(RunbookDocument runbook, string query, string? serviceName, string? environment)
	{
		var haystack = $"{runbook.Id} {runbook.Title} {runbook.Summary} {string.Join(' ', runbook.Tags)}".ToLowerInvariant();
		var queryMatch = haystack.Contains(query.ToLowerInvariant(), StringComparison.OrdinalIgnoreCase)
			|| runbook.Tags.Any(tag => tag.Contains(query, StringComparison.OrdinalIgnoreCase));

		var serviceMatch = string.IsNullOrWhiteSpace(serviceName)
			|| runbook.Tags.Any(tag => tag.Contains(serviceName, StringComparison.OrdinalIgnoreCase))
			|| haystack.Contains(serviceName, StringComparison.OrdinalIgnoreCase);

		var environmentMatch = string.IsNullOrWhiteSpace(environment)
			|| haystack.Contains(environment, StringComparison.OrdinalIgnoreCase)
			|| runbook.Tags.Any(tag => tag.Contains(environment, StringComparison.OrdinalIgnoreCase));

		return queryMatch && serviceMatch && environmentMatch;
	}
}