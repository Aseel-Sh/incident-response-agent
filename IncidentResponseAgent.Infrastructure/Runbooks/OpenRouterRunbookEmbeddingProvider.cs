using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace IncidentResponseAgent.Infrastructure.Runbooks;

internal sealed class OpenRouterRunbookEmbeddingProvider(RunbookRetrievalOptions options, IHttpClientFactory clients) : IRunbookEmbeddingProvider
{
    public string ProviderName => "openrouter";
    public string ModelName => options.Model ?? "openai/text-embedding-3-small";
    public int Dimensions => 0;
    public static bool IsConfigured(RunbookRetrievalOptions options) => !string.IsNullOrWhiteSpace(Key(options));
    private static string? Key(RunbookRetrievalOptions options) => string.IsNullOrWhiteSpace(options.ApiKey) ? Environment.GetEnvironmentVariable("OPENROUTER_API_KEY") : options.ApiKey;

    public async Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text)) return [];
        return (await GenerateEmbeddingsAsync([text], cancellationToken))[0];
    }

    public async Task<IReadOnlyList<float[]>> GenerateEmbeddingsAsync(IReadOnlyList<string> texts, CancellationToken cancellationToken = default)
    {
        if (texts.Count == 0) return [];
        var endpoint = options.Endpoint ?? "https://openrouter.ai/api/v1";
        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint.TrimEnd('/') + "/embeddings");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Key(options));
        request.Content = JsonContent.Create(new { model = ModelName, input = texts, encoding_format = "float" });
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(Math.Clamp(options.EmbeddingTimeoutSeconds, 3, 60)));
        try
        {
            using var response = await clients.CreateClient().SendAsync(request, timeout.Token);
            if (!response.IsSuccessStatusCode)
            {
                var remedy = response.StatusCode switch
                {
                    System.Net.HttpStatusCode.TooManyRequests => "Provider rate limit reached; retry after the provider quota resets.",
                    System.Net.HttpStatusCode.Forbidden => "Check the API key spending limit and model access.",
                    System.Net.HttpStatusCode.PaymentRequired => "Check the provider account credit balance.",
                    System.Net.HttpStatusCode.Unauthorized => "Check the configured API key.",
                    _ => "Check provider availability and the configured embedding model."
                };
                throw new HttpRequestException($"OpenRouter embeddings returned HTTP {(int)response.StatusCode}. {remedy}", null, response.StatusCode);
            }
            using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(timeout.Token));
            var data = body.RootElement.GetProperty("data");
            if (data.GetArrayLength() != texts.Count) throw new InvalidOperationException("OpenRouter embedding count does not match input count.");
            var vectors = new float[texts.Count][];
            foreach (var item in data.EnumerateArray())
            {
                var index = item.GetProperty("index").GetInt32();
                if (index < 0 || index >= texts.Count || vectors[index] is not null) throw new InvalidOperationException("OpenRouter returned an invalid or duplicate embedding index.");
                var vector = item.GetProperty("embedding").EnumerateArray().Select(value => value.GetSingle()).ToArray();
                if (vector.Length == 0 || vector.Any(value => !float.IsFinite(value))) throw new InvalidOperationException("OpenRouter returned an empty or non-finite embedding.");
                vectors[index] = vector;
            }
            if (vectors.Any(vector => vector.Length != vectors[0].Length)) throw new InvalidOperationException("OpenRouter returned inconsistent embedding dimensions.");
            return vectors;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        { throw new TimeoutException("OpenRouter embedding request timed out. Retry retrieval when the provider is available."); }
    }
}
