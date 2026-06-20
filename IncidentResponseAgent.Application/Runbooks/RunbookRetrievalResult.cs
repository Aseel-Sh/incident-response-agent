using IncidentResponseAgent.Domain.Runbooks;

namespace IncidentResponseAgent.Application.Runbooks;

public sealed record RunbookRetrievalResult
{
	public IReadOnlyList<RunbookDocument> Runbooks { get; init; } = Array.Empty<RunbookDocument>();

	public string EmbeddingProvider { get; init; } = "unknown";

	public string VectorStoreProvider { get; init; } = "unknown";

	public string RagStatus { get; init; } = "available";

	public bool IsDegraded { get; init; }

	public string? DegradedReason { get; init; }

	public long DurationMilliseconds { get; init; }
}
