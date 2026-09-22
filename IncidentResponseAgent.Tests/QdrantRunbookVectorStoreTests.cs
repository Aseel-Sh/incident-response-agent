using System.Net;
using System.Text;
using System.Text.Json;
using IncidentResponseAgent.Infrastructure.Runbooks;
using Microsoft.Extensions.Logging.Abstractions;

namespace IncidentResponseAgent.Tests;

public sealed class QdrantRunbookVectorStoreTests
{
	[Fact]
	public async Task CollectionProbeRecoversAfterAnOutageWithoutClaimingFallback()
	{
		var available = false;
		var handler = new RecordingHandler(_ => available
			? new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"result\":{\"config\":{\"params\":{\"vectors\":{\"size\":384}}}}}", Encoding.UTF8, "application/json") }
			: throw new HttpRequestException("Qdrant is offline"));
		var store = new QdrantRunbookVectorStore(new RunbookRetrievalOptions { QdrantEndpoint = "https://qdrant.example.test" },
			new StaticHttpClientFactory(handler), NullLogger<QdrantRunbookVectorStore>.Instance);
		Assert.False(await store.EnsureCollectionAsync(384, CancellationToken.None));
		Assert.Equal("qdrant-unavailable", store.ProviderName);
		available = true;
		Assert.True(await store.EnsureCollectionAsync(384, CancellationToken.None));
		Assert.Equal("qdrant", store.ProviderName);
	}

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

	[Fact]
	public async Task SearchAsyncUsesQueryPointsApiAndMapsPayload()
	{
		var handler = new RecordingHandler(request =>
		{
			Assert.Equal(HttpMethod.Post, request.Method);
			Assert.Equal("/collections/incident_runbook_chunks/points/query", request.RequestUri?.AbsolutePath);
			var requestJson = request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
			using var document = JsonDocument.Parse(requestJson);
			Assert.Equal(0.25, document.RootElement.GetProperty("query")[0].GetDouble());
			Assert.Equal(2, document.RootElement.GetProperty("limit").GetInt32());
			Assert.True(document.RootElement.GetProperty("with_payload").GetBoolean());

			return new HttpResponseMessage(HttpStatusCode.OK)
			{
				Content = new StringContent(
					"""
					{
					  "result": {
					    "points": [{
					      "id": "79c6b59f-3d31-46c3-a7d9-4544784d5c13",
					      "score": 0.93,
					      "payload": {
					        "runbookId": "database-latency",
					        "documentTitle": "Database latency response",
					        "documentSummary": "Recover saturated database connections.",
					        "tags": ["database", "latency"],
					        "sourcePath": "database-latency.md",
					        "ordinal": 1,
					        "sectionPath": "Mitigation",
					        "text": "Inspect connection pool utilization.",
					        "searchText": "database latency connection pool"
					      }
					    }]
					  }
					}
					""",
					Encoding.UTF8,
					"application/json")
			};
		});

		var store = new QdrantRunbookVectorStore(
			new RunbookRetrievalOptions
			{
				QdrantEndpoint = "https://qdrant.example.test",
				QdrantCollectionName = "incident_runbook_chunks"
			},
			new StaticHttpClientFactory(handler),
			NullLogger<QdrantRunbookVectorStore>.Instance);

		var matches = await store.SearchAsync([0.25f, 0.75f], 2, CancellationToken.None);

		var match = Assert.Single(matches);
		Assert.Equal("database-latency", match.RunbookId);
		Assert.Equal("Database latency response", match.DocumentTitle);
		Assert.Equal(0.93, match.Score, 2);
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
