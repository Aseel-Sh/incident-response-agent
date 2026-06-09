using IncidentResponseAgent.Infrastructure.Runbooks;
using Microsoft.Extensions.Logging.Abstractions;

namespace IncidentResponseAgent.Tests;

public sealed class ResilientRunbookEmbeddingProviderTests
{
	[Fact]
	public async Task GenerateEmbeddingAsyncFallsBackWhenPrimaryFails()
	{
		var primary = new ThrowingEmbeddingProvider();
		var fallback = new TestEmbeddingProvider();
		var provider = new ResilientRunbookEmbeddingProvider(primary, fallback, NullLogger<ResilientRunbookEmbeddingProvider>.Instance);

		var vector = await provider.GenerateEmbeddingAsync("checkout failure");

		Assert.Equal([1f, 0f, 0f], vector);
		Assert.Contains("failed", provider.ProviderName);
	}

	private sealed class ThrowingEmbeddingProvider : IRunbookEmbeddingProvider
	{
		public string ProviderName => "throwing";

		public string ModelName => "throwing-model";

		public int Dimensions => 0;

		public Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default)
		{
			throw new HttpRequestException("provider unavailable");
		}
	}

	private sealed class TestEmbeddingProvider : IRunbookEmbeddingProvider
	{
		public string ProviderName => "fallback";

		public string ModelName => "fallback-model";

		public int Dimensions => 3;

		public Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default)
		{
			return Task.FromResult(new[] { 1f, 0f, 0f });
		}
	}
}
