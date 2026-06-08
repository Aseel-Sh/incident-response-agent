namespace IncidentResponseAgent.Infrastructure.Runbooks;

public sealed class RunbookRetrievalOptions
{
	public string? DatabasePath { get; init; }

	public string? KnowledgeBasePath { get; init; }

	public string? Endpoint { get; init; } = "https://api-inference.huggingface.co/pipeline/feature-extraction/";

	public string? ApiKey { get; init; }

	public string? Model { get; init; } = "thenlper/gte-large";

	public string? LocalEmbeddingModel { get; init; } = "local-hashing-384";

	public int LocalEmbeddingDimensions { get; init; } = 384;

	public int BatchSize { get; init; } = 8;

	public int MaxResults { get; init; } = 3;

	public double MinimumRelevanceScore { get; init; } = 0.25;

	public double SemanticWeight { get; init; } = 0.75;

	public double LexicalWeight { get; init; } = 0.25;
}
