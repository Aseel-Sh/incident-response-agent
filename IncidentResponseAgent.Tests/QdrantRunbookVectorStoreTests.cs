using System.Net;
using System.Text;
using IncidentResponseAgent.Infrastructure.Runbooks;
using Microsoft.Extensions.Logging.Abstractions;

namespace IncidentResponseAgent.Tests;

public sealed class QdrantRunbookVectorStoreTests
{
	[Fact]
	public async Task EnsureCollectionAsyncUsesExistingCollectionAndApiKeyHeader()
	{
		var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
		{
			Content = new StringContent(
				"""
				{
				  "result": {
				    "config": {
				      "params": {
				        "vectors": {
				          "vector_size": 384,
				          "distance": "Cosine"
				        }
				      }
				    }
				  }
				}
				""",
				Encoding.UTF8,
				"application/json")
		});

		var store = new QdrantRunbookVectorStore(
			new RunbookRetrievalOptions
			{
				QdrantEndpoint = "https://qdrant.example.test",
				QdrantCollectionName = "incident_runbook_chunks",
				QdrantApiKey = "secret"
			},
			new StaticHttpClientFactory(handler),
			NullLogger<QdrantRunbookVectorStore>.Instance);

		var ready = await store.EnsureCollectionAsync(384, CancellationToken.None);

		Assert.True(ready);
		Assert.Single(handler.Requests);
		Assert.Equal(HttpMethod.Get, handler.Requests[0].Method);
		Assert.Equal("secret", handler.Requests[0].Headers.GetValues("api-key").Single());
	}

	private sealed class StaticHttpClientFactory : IHttpClientFactory
	{
		private readonly HttpMessageHandler _handler;

		public StaticHttpClientFactory(HttpMessageHandler handler)
		{
			_handler = handler;
		}

		public HttpClient CreateClient(string name)
		{
			return new HttpClient(_handler);
		}
	}

	private sealed class RecordingHandler : HttpMessageHandler
	{
		private readonly Func<HttpRequestMessage, HttpResponseMessage> _responseFactory;

		public RecordingHandler(Func<HttpRequestMessage, HttpResponseMessage> responseFactory)
		{
			_responseFactory = responseFactory;
		}

		public List<HttpRequestMessage> Requests { get; } = [];

		protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
		{
			Requests.Add(request);
			return Task.FromResult(_responseFactory(request));
		}
	}
}
