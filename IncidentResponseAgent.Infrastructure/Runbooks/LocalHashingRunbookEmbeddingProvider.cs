using System.Security.Cryptography;
using System.Text;

namespace IncidentResponseAgent.Infrastructure.Runbooks;

internal sealed class LocalHashingRunbookEmbeddingProvider : IRunbookEmbeddingProvider
{
	private readonly int _dimensions;

	public LocalHashingRunbookEmbeddingProvider(RunbookRetrievalOptions options)
	{
		_dimensions = Math.Clamp(options.LocalEmbeddingDimensions, 128, 1536);
		ModelName = string.IsNullOrWhiteSpace(options.LocalEmbeddingModel)
			? "local-hashing-384"
			: options.LocalEmbeddingModel.Trim();
	}

	public string ProviderName => "local";

	public string ModelName { get; }

	public int Dimensions => _dimensions;

	public Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();

		var vector = new float[_dimensions];
		foreach (var token in RunbookTextAnalysis.Tokenize(text))
		{
			AddToken(vector, token);
		}

		Normalize(vector);
		return Task.FromResult(vector);
	}

	private static void AddToken(float[] vector, string token)
	{
		var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
		var bucket = BitConverter.ToUInt32(bytes, 0) % vector.Length;
		var sign = (bytes[4] & 1) == 0 ? 1f : -1f;
		var weight = 1f + Math.Min(token.Length, 12) / 12f;

		vector[bucket] += sign * weight;
	}

	private static void Normalize(float[] vector)
	{
		double magnitude = 0;
		foreach (var value in vector)
		{
			magnitude += value * value;
		}

		if (magnitude <= 0)
		{
			return;
		}

		var scale = 1 / Math.Sqrt(magnitude);
		for (var index = 0; index < vector.Length; index++)
		{
			vector[index] = (float)(vector[index] * scale);
		}
	}
}
