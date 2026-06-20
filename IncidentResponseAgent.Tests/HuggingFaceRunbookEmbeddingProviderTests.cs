using System.Net;
using System.Text;
using IncidentResponseAgent.Infrastructure.Runbooks;
using Microsoft.Extensions.Logging.Abstractions;

namespace IncidentResponseAgent.Tests;

public sealed class HuggingFaceRunbookEmbeddingProviderTests
{
	[Fact]
	public async Task GenerateEmbeddingAsyncPoolsNestedTokenEmbeddings()
	{
		var factory = new StaticHttpClientFactory("[[1, 3, 5], [3, 5, 7]]");
		var provider = new HuggingFaceRunbookEmbeddingProvider(
			new RunbookRetrievalOptions
			{
				ApiKey = "test-token",
				Endpoint = "https://example.test/feature-extraction/",
				Model = "BAAI/bge-small-en-v1.5"
			},
			factory,
			NullLogger<HuggingFaceRunbookEmbeddingProvider>.Instance);

		var vector = await provider.GenerateEmbeddingAsync("checkout failure");

		Assert.Equal([2f, 4f, 6f], vector);
		Assert.Equal("https://example.test/feature-extraction/BAAI/bge-small-en-v1.5", factory.LastRequestUri?.ToString());
	}

	[Fact]
	public async Task GenerateEmbeddingAsyncPreservesProviderHttpFailureReason()
	{
		var provider = new HuggingFaceRunbookEmbeddingProvider(
			new RunbookRetrievalOptions { ApiKey = "test-token", Endpoint = "https://router.huggingface.co/hf-inference/models/", Model = "BAAI/bge-small-en-v1.5" },
			new StaticHttpClientFactory("{\"error\":\"model unavailable\"}", HttpStatusCode.ServiceUnavailable),
			NullLogger<HuggingFaceRunbookEmbeddingProvider>.Instance);

		var exception = await Assert.ThrowsAsync<HttpRequestException>(() => provider.GenerateEmbeddingAsync("checkout failure"));

		Assert.Equal(HttpStatusCode.ServiceUnavailable, exception.StatusCode);
		Assert.Contains("model unavailable", exception.Message);
	}

	private sealed class StaticHttpClientFactory : IHttpClientFactory
	{
		private readonly string _json;
		private readonly HttpStatusCode _statusCode;
		public Uri? LastRequestUri { get; private set; }

		public StaticHttpClientFactory(string json, HttpStatusCode statusCode = HttpStatusCode.OK)
		{
			_json = json;
			_statusCode = statusCode;
		}

		public HttpClient CreateClient(string name)
		{
			return new HttpClient(new StaticJsonHandler(_json, _statusCode, uri => LastRequestUri = uri));
		}
	}

	private sealed class StaticJsonHandler : HttpMessageHandler
	{
		private readonly string _json;
		private readonly HttpStatusCode _statusCode;
		private readonly Action<Uri?> _captureUri;

		public StaticJsonHandler(string json, HttpStatusCode statusCode, Action<Uri?> captureUri)
		{
			_json = json;
			_statusCode = statusCode;
			_captureUri = captureUri;
		}

		protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
		{
			_captureUri(request.RequestUri);
			var response = new HttpResponseMessage(_statusCode)
			{
				Content = new StringContent(_json, Encoding.UTF8, "application/json")
			};
			return Task.FromResult(response);
		}
	}
}
