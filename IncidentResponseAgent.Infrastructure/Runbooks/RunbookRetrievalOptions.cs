namespace IncidentResponseAgent.Infrastructure.Runbooks;

public sealed class RunbookRetrievalOptions
{
	public string VectorStoreProvider { get; init; } = "Qdrant";

	public string? DatabasePath { get; init; }

	public string? KnowledgeBasePath { get; init; }

	public string? SourceRegistryPath { get; init; }

	public string QdrantEndpoint { get; init; } = "http://localhost:6333";

	public string QdrantCollectionName { get; init; } = "incident_runbook_chunks";

	public string? QdrantApiKey { get; init; }

	public int QdrantTimeoutSeconds { get; init; } = 5;

	public string? Endpoint { get; init; } = "https://router.huggingface.co/hf-inference/models/";

	public string? ApiKey { get; init; }

	public string? Model { get; init; } = "BAAI/bge-small-en-v1.5";

	public int EmbeddingTimeoutSeconds { get; init; } = 30;

	public string? LocalEmbeddingModel { get; init; } = "local-hashing-384";

	public int LocalEmbeddingDimensions { get; init; } = 384;

	public int BatchSize { get; init; } = 8;

	public int MaxResults { get; init; } = 3;

	public double MinimumRelevanceScore { get; init; } = 0.25;

	public double SemanticWeight { get; init; } = 0.75;

	public double LexicalWeight { get; init; } = 0.25;
}
