namespace IncidentResponseAgent.Infrastructure.Runbooks;

public sealed class RunbookRetrievalOptions
{
	public string? Endpoint { get; init; } = "https://api-inference.huggingface.co/pipeline/feature-extraction/";

	public string? ApiKey { get; init; }

	public string? Model { get; init; } = "thenlper/gte-large";

	public int BatchSize { get; init; } = 8;

	public int MaxResults { get; init; } = 3;

	public double MinimumRelevanceScore { get; init; } = 0.25;
}