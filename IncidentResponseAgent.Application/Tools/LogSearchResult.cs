namespace IncidentResponseAgent.Application.Tools;

public sealed record LogSearchResult
{
	public IReadOnlyList<LogSearchEntry> Entries { get; init; } = Array.Empty<LogSearchEntry>();
}