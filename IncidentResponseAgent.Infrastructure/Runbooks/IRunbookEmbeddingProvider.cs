namespace IncidentResponseAgent.Infrastructure.Runbooks;

internal interface IRunbookEmbeddingProvider
{
	string ProviderName { get; }

	string ModelName { get; }

	int Dimensions { get; }

	bool IsDegraded => false;

	string? DegradedReason => null;

	Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default);

	async Task<IReadOnlyList<float[]>> GenerateEmbeddingsAsync(IReadOnlyList<string> texts, CancellationToken cancellationToken = default)
	{
		var vectors = new List<float[]>();
		foreach (var text in texts) vectors.Add(await GenerateEmbeddingAsync(text, cancellationToken));
		return vectors;
	}
}
