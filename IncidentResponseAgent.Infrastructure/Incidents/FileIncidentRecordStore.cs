using System.Text.Json;
using IncidentResponseAgent.Application.Incidents;
using IncidentResponseAgent.Domain.Incidents;

namespace IncidentResponseAgent.Infrastructure.Incidents;

public sealed class FileIncidentRecordStore : IIncidentRecordStore
{
	private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
	{
		WriteIndented = true
	};

	private readonly SemaphoreSlim _fileLock = new(1, 1);
	private readonly string _filePath;

	public FileIncidentRecordStore()
	{
		var rootFolder = Path.Combine(
			Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
			"IncidentResponseAgent");

		Directory.CreateDirectory(rootFolder);
		_filePath = Path.Combine(rootFolder, "incident-records.json");
	}

	public async Task SaveAsync(Incident incident, IncidentAnalysisResult analysisResult, CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(incident);
		ArgumentNullException.ThrowIfNull(analysisResult);
		cancellationToken.ThrowIfCancellationRequested();

		await _fileLock.WaitAsync(cancellationToken);
		try
		{
			var records = await ReadRecordsAsync(cancellationToken);
			records[incident.Id] = new IncidentAnalysisRecord
			{
				Incident = incident,
				AnalysisResult = analysisResult,
				CreatedAtUtc = DateTimeOffset.UtcNow
			};

			await WriteRecordsAsync(records.Values, cancellationToken);
		}
		finally
		{
			_fileLock.Release();
		}
	}

	public async Task<IncidentAnalysisRecord?> GetByIncidentIdAsync(Guid incidentId, CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();

		await _fileLock.WaitAsync(cancellationToken);
		try
		{
			var records = await ReadRecordsAsync(cancellationToken);
			return records.TryGetValue(incidentId, out var record) ? record : null;
		}
		finally
		{
			_fileLock.Release();
		}
	}

	public async Task<IReadOnlyList<IncidentAnalysisRecord>> GetRecentAsync(int maxResults, CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();

		await _fileLock.WaitAsync(cancellationToken);
		try
		{
			var count = maxResults <= 0 ? 1 : maxResults;
			var records = (await ReadRecordsAsync(cancellationToken)).Values
				.OrderByDescending(record => record.CreatedAtUtc)
				.Take(count)
				.ToArray();

			return records;
		}
		finally
		{
			_fileLock.Release();
		}
	}

	private async Task<Dictionary<Guid, IncidentAnalysisRecord>> ReadRecordsAsync(CancellationToken cancellationToken)
	{
		if (!File.Exists(_filePath))
		{
			return new Dictionary<Guid, IncidentAnalysisRecord>();
		}

		await using var stream = File.Open(_filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
		var records = await JsonSerializer.DeserializeAsync<List<IncidentAnalysisRecord>>(stream, SerializerOptions, cancellationToken)
			?? [];

		return records.ToDictionary(record => record.Incident.Id, record => record);
	}

	private async Task WriteRecordsAsync(IEnumerable<IncidentAnalysisRecord> records, CancellationToken cancellationToken)
	{
		await using var stream = File.Open(_filePath, FileMode.Create, FileAccess.Write, FileShare.None);
		await JsonSerializer.SerializeAsync(stream, records, SerializerOptions, cancellationToken);
	}
}