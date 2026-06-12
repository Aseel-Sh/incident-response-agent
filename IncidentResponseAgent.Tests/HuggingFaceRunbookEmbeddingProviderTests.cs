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
		var provider = new HuggingFaceRunbookEmbeddingProvider(
			new RunbookRetrievalOptions
			{
				ApiKey = "test-token",
				Endpoint = "https://example.test/feature-extraction/",
				Model = "thenlper/gte-large"
			},
			new StaticHttpClientFactory("[[1, 3, 5], [3, 5, 7]]"),
			NullLogger<HuggingFaceRunbookEmbeddingProvider>.Instance);

		var vector = await provider.GenerateEmbeddingAsync("checkout failure");

		Assert.Equal([2f, 4f, 6f], vector);
	}

	private sealed class StaticHttpClientFactory : IHttpClientFactory
	{
		private readonly string _json;

		public StaticHttpClientFactory(string json)
		{
			_json = json;
		}

		public HttpClient CreateClient(string name)
		{
			return new HttpClient(new StaticJsonHandler(_json));
		}
	}

	private sealed class StaticJsonHandler : HttpMessageHandler
	{
		private readonly string _json;

		public StaticJsonHandler(string json)
		{
			_json = json;
		}

		protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
		{
			var response = new HttpResponseMessage(HttpStatusCode.OK)
			{
				Content = new StringContent(_json, Encoding.UTF8, "application/json")
			};
			return Task.FromResult(response);
		}
	}
}
