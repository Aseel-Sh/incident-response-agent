namespace IncidentResponseAgent.Infrastructure.Runbooks;

internal interface IRunbookEmbeddingProvider
{
	string ProviderName { get; }

	string ModelName { get; }

	int Dimensions { get; }

	bool IsDegraded => false;

	string? DegradedReason => null;

	Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default);
}
