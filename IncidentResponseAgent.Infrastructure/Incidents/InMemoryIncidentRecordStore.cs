using System.Collections.Concurrent;
using IncidentResponseAgent.Application.Incidents;
using IncidentResponseAgent.Domain.Incidents;

namespace IncidentResponseAgent.Infrastructure.Incidents;

public sealed class InMemoryIncidentRecordStore : IIncidentRecordStore
{
	private readonly ConcurrentDictionary<Guid, IncidentAnalysisRecord> _records = new();

	public Task SaveAsync(Incident incident, IncidentAnalysisResult analysisResult, CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(incident);
		ArgumentNullException.ThrowIfNull(analysisResult);
		cancellationToken.ThrowIfCancellationRequested();

		_records[incident.Id] = new IncidentAnalysisRecord
		{
			Incident = incident,
			AnalysisResult = analysisResult,
			CreatedAtUtc = DateTimeOffset.UtcNow
		};

		return Task.CompletedTask;
	}

	public Task<IncidentAnalysisRecord?> GetByIncidentIdAsync(Guid incidentId, CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();
		_records.TryGetValue(incidentId, out var record);
		return Task.FromResult(record);
	}

	public Task<IReadOnlyList<IncidentAnalysisRecord>> GetRecentAsync(int maxResults, CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();

		var count = maxResults <= 0 ? 1 : maxResults;
		var records = _records.Values
			.OrderByDescending(record => record.CreatedAtUtc)
			.Take(count)
			.ToArray();

		return Task.FromResult<IReadOnlyList<IncidentAnalysisRecord>>(records);
	}
}