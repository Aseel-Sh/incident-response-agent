using System.Net;
using System.Text;
using System.Text.Json;
using IncidentResponseAgent.Infrastructure.Runbooks;

namespace IncidentResponseAgent.Tests;

public sealed class OpenRouterRunbookEmbeddingProviderTests
{
    [Fact]
    public async Task ExternalEmbeddingUsesConfiguredModelAndFloatEncoding()
    {
        var handler = new Handler("""{"data":[{"index":0,"embedding":[0.2,0.4,0.6]}]}""");
        var provider = Create(handler);
        var vector = await provider.GenerateEmbeddingAsync("runbook");
        Assert.Equal(new[] {0.2f, 0.4f, 0.6f}, vector);
        Assert.Equal("https://example.test/v1/embeddings", handler.Url);
        using var json = JsonDocument.Parse(handler.Body!);
        Assert.Equal("embedding/test", json.RootElement.GetProperty("model").GetString());
        Assert.Equal("float", json.RootElement.GetProperty("encoding_format").GetString());
        Assert.Equal("runbook", json.RootElement.GetProperty("input")[0].GetString());
    }

    [Theory]
    [InlineData("{\"data\":[]}")]
    [InlineData("{\"data\":[{\"index\":0,\"embedding\":[]}]}")]
    public async Task RejectsMissingVectors(string response) =>
        await Assert.ThrowsAsync<InvalidOperationException>(() => Create(new Handler(response)).GenerateEmbeddingAsync("runbook"));

    [Fact]
    public async Task HttpFailureDoesNotExposeProviderBody()
    {
        var error = await Assert.ThrowsAsync<HttpRequestException>(() => Create(new Handler("private provider content", HttpStatusCode.Forbidden)).GenerateEmbeddingAsync("runbook"));
        Assert.Equal(HttpStatusCode.Forbidden, error.StatusCode);
        Assert.DoesNotContain("private provider content", error.Message);
    }

    [Fact]
    public async Task BatchResultsAreMappedByIndexRatherThanResponseOrder()
    {
        var provider = Create(new Handler("""{"data":[{"index":1,"embedding":[0,1]},{"index":0,"embedding":[1,0]}]}"""));
        var vectors = await provider.GenerateEmbeddingsAsync(["first", "second"]);
        Assert.Equal(new[] {1f,0f}, vectors[0]);
        Assert.Equal(new[] {0f,1f}, vectors[1]);
    }

    private static OpenRouterRunbookEmbeddingProvider Create(Handler handler) => new(new RunbookRetrievalOptions { ApiKey = "test", Endpoint = "https://example.test/v1", Model = "embedding/test" }, new Factory(handler));
    private sealed class Factory(Handler handler) : IHttpClientFactory { public HttpClient CreateClient(string name) => new(handler, false); }
    private sealed class Handler(string body, HttpStatusCode status = HttpStatusCode.OK) : HttpMessageHandler
    {
        public string? Body; public string? Url;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        { Url = request.RequestUri!.ToString(); Body = await request.Content!.ReadAsStringAsync(cancellationToken); return new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") }; }
    }
}
