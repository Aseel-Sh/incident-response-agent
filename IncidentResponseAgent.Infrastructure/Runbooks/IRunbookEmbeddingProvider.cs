namespace IncidentResponseAgent.Infrastructure.Runbooks;

internal interface IRunbookEmbeddingProvider
{
	string ProviderName { get; }

	string ModelName { get; }

	int Dimensions { get; }

	Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default);
}
