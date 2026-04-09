using IncidentResponseAgent.Domain.Runbooks;

namespace IncidentResponseAgent.Application.Runbooks;

public sealed record RunbookRetrievalResult
{
	public IReadOnlyList<RunbookDocument> Runbooks { get; init; } = Array.Empty<RunbookDocument>();
}