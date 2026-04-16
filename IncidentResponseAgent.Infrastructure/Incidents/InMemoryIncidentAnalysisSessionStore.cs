using System.Collections.Concurrent;
using IncidentResponseAgent.Application.Incidents;

namespace IncidentResponseAgent.Infrastructure.Incidents;

public sealed class InMemoryIncidentAnalysisSessionStore : IIncidentAnalysisSessionStore
{
	private readonly ConcurrentDictionary<string, IncidentAnalysisSessionContext> _sessions = new();

	public Task<IncidentAnalysisSessionContext> GetOrCreateAsync(string? sessionId, CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();

		var key = string.IsNullOrWhiteSpace(sessionId)
			? Guid.NewGuid().ToString("N")
			: sessionId.Trim();

		var session = _sessions.GetOrAdd(key, static id => new IncidentAnalysisSessionContext
		{
			SessionId = id,
			TurnNumber = 0,
			UpdatedAtUtc = DateTimeOffset.UtcNow
		});

		return Task.FromResult(session);
	}

	public Task SaveAsync(IncidentAnalysisSessionContext sessionContext, CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(sessionContext);
		cancellationToken.ThrowIfCancellationRequested();

		_sessions[sessionContext.SessionId] = sessionContext;
		return Task.CompletedTask;
	}
}