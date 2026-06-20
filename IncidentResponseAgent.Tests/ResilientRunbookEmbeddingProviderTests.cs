using IncidentResponseAgent.Infrastructure.Runbooks;
using Microsoft.Extensions.Logging.Abstractions;

namespace IncidentResponseAgent.Tests;

public sealed class ResilientRunbookEmbeddingProviderTests
{
	[Fact]
	public async Task HealthyPrimaryIsReportedWithoutFallbackLabel()
	{
		var primary = new NamedEmbeddingProvider("huggingface", "BAAI/bge-small-en-v1.5");
		var provider = new ResilientRunbookEmbeddingProvider(primary, new TestEmbeddingProvider(), NullLogger<ResilientRunbookEmbeddingProvider>.Instance);

		var vector = await provider.GenerateEmbeddingAsync("checkout failure");

		Assert.Equal([1f, 0f, 0f], vector);
		Assert.Equal("huggingface", provider.ProviderName);
		Assert.Equal("BAAI/bge-small-en-v1.5", provider.ModelName);
		Assert.False(provider.IsDegraded);
		Assert.Null(provider.DegradedReason);
	}

	[Fact]
	public async Task GenerateEmbeddingAsyncFallsBackWhenPrimaryFails()
	{
		var primary = new ThrowingEmbeddingProvider();
		var fallback = new TestEmbeddingProvider();
		var provider = new ResilientRunbookEmbeddingProvider(primary, fallback, NullLogger<ResilientRunbookEmbeddingProvider>.Instance);

		var vector = await provider.GenerateEmbeddingAsync("checkout failure");

		Assert.Equal([1f, 0f, 0f], vector);
		Assert.Contains("failed", provider.ProviderName);
		Assert.True(provider.IsDegraded);
		Assert.Contains("HttpRequestException", provider.DegradedReason);
		Assert.Contains("provider unavailable", provider.DegradedReason);
	}

	[Fact]
	public async Task EmptyPrimaryVectorIsReportedAsDegradedFallback()
	{
		var provider = new ResilientRunbookEmbeddingProvider(new EmptyEmbeddingProvider(), new TestEmbeddingProvider(), NullLogger<ResilientRunbookEmbeddingProvider>.Instance);

		var vector = await provider.GenerateEmbeddingAsync("checkout failure");

		Assert.Equal([1f, 0f, 0f], vector);
		Assert.True(provider.IsDegraded);
		Assert.Contains("empty embedding vector", provider.DegradedReason);
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

	private sealed class EmptyEmbeddingProvider : IRunbookEmbeddingProvider
	{
		public string ProviderName => "empty";
		public string ModelName => "empty-model";
		public int Dimensions => 0;
		public Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default) => Task.FromResult(Array.Empty<float>());
	}

	private sealed class NamedEmbeddingProvider(string providerName, string modelName) : IRunbookEmbeddingProvider
	{
		public string ProviderName => providerName;
		public string ModelName => modelName;
		public int Dimensions => 3;
		public Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default) => Task.FromResult(new[] { 1f, 0f, 0f });
	}
}
