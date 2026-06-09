namespace IncidentResponseAgent.Application.Runbooks;

public sealed record RunbookRetrievalDiagnosticsResult
{
	public required string EmbeddingProvider { get; init; }

	public required string EmbeddingModel { get; init; }

	public required string DatabasePath { get; init; }

	public required string KnowledgeBasePath { get; init; }

	public IReadOnlyList<RunbookRetrievalMatch> Matches { get; init; } = Array.Empty<RunbookRetrievalMatch>();
}
