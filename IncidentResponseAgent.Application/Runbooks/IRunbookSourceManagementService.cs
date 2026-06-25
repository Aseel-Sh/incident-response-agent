namespace IncidentResponseAgent.Application.Runbooks;

public interface IRunbookSourceManagementService
{
	Task<IReadOnlyList<RunbookSourceStatus>> GetSourcesAsync(CancellationToken cancellationToken = default);
	Task<RunbookSourceStatus> AddSourceAsync(RunbookSourceInput input, CancellationToken cancellationToken = default);
	Task<RunbookSourceStatus> SetEnabledAsync(string sourceId, bool enabled, CancellationToken cancellationToken = default);
	Task<RunbookSourceStatus> SynchronizeAsync(string sourceId, CancellationToken cancellationToken = default);
	Task<bool> RemoveSourceAsync(string sourceId, CancellationToken cancellationToken = default);
}

public sealed record RunbookSourceInput
{
	public required string Name { get; init; }
	public required string Type { get; init; }
	public required string Path { get; init; }
}

public sealed record RunbookSourceStatus
{
	public required string Id { get; init; }
	public required string Name { get; init; }
	public required string Type { get; init; }
	public required string Path { get; init; }
	public bool Enabled { get; init; }
	public bool Reachable { get; init; }
	public bool Removable { get; init; }
	public int DocumentCount { get; init; }
	public int SectionCount { get; init; }
	public DateTimeOffset? LastSynchronizedAtUtc { get; init; }
	public string? LastError { get; init; }
}
