using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace IncidentResponseAgent.Infrastructure.Runbooks;

internal sealed class HuggingFaceRunbookEmbeddingProvider : IRunbookEmbeddingProvider
{
	private readonly IHttpClientFactory _httpClientFactory;
	private readonly ILogger<HuggingFaceRunbookEmbeddingProvider> _logger;
	private readonly string _apiKey;
	private readonly string _endpoint;

	public HuggingFaceRunbookEmbeddingProvider(
		RunbookRetrievalOptions options,
		IHttpClientFactory httpClientFactory,
		ILogger<HuggingFaceRunbookEmbeddingProvider> logger)
	{
		_httpClientFactory = httpClientFactory;
		_logger = logger;
		_apiKey = ResolveApiKey(options);
		ModelName = ResolveModel(options);
		_endpoint = ResolveEndpoint(options);
	}

	public string ProviderName => "huggingface";

	public string ModelName { get; }

	public int Dimensions => 0;

	public static bool IsConfigured(RunbookRetrievalOptions options)
	{
		return !string.IsNullOrWhiteSpace(ResolveApiKey(options));
	}

	public async Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default)
	{
		if (string.IsNullOrWhiteSpace(text))
		{
			return Array.Empty<float>();
		}

		var httpClient = _httpClientFactory.CreateClient();
		using var request = new HttpRequestMessage(HttpMethod.Post, BuildEmbeddingUri());
		request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
		request.Content = JsonContent.Create(new
		{
			inputs = text,
			normalize = true,
			truncate = true
		});

		_logger.LogDebug("Generating Hugging Face embedding with model {ModelName}.", ModelName);
		using var response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
		response.EnsureSuccessStatusCode();

		await using var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
		using var jsonDocument = await JsonDocument.ParseAsync(responseStream, cancellationToken: cancellationToken).ConfigureAwait(false);
		return ParseEmbedding(jsonDocument.RootElement);
	}

	private Uri BuildEmbeddingUri()
	{
		var baseEndpoint = _endpoint.EndsWith("/", StringComparison.Ordinal) ? _endpoint : _endpoint + "/";
		return new Uri(baseEndpoint + Uri.EscapeDataString(ModelName), UriKind.Absolute);
	}

	private static float[] ParseEmbedding(JsonElement element)
	{
		if (element.ValueKind != JsonValueKind.Array || element.GetArrayLength() == 0)
		{
			return Array.Empty<float>();
		}

		var first = element[0];
		if (first.ValueKind != JsonValueKind.Array)
		{
			return element.EnumerateArray().Select(value => (float)value.GetDouble()).ToArray();
		}

		var nested = element.EnumerateArray()
			.Where(item => item.ValueKind == JsonValueKind.Array)
			.Select(item => item.EnumerateArray().Select(value => (float)value.GetDouble()).ToArray())
			.Where(vector => vector.Length > 0)
			.ToArray();

		if (nested.Length == 0)
		{
			return Array.Empty<float>();
		}

		if (nested.Length == 1)
		{
			return nested[0];
		}

		var dimensions = nested.Min(vector => vector.Length);
		var pooled = new float[dimensions];
		foreach (var vector in nested)
		{
			for (var index = 0; index < dimensions; index++)
			{
				pooled[index] += vector[index];
			}
		}

		for (var index = 0; index < dimensions; index++)
		{
			pooled[index] /= nested.Length;
		}

		return pooled;
	}

	private static string ResolveApiKey(RunbookRetrievalOptions options)
	{
		var apiKey = options.ApiKey;
		if (string.IsNullOrWhiteSpace(apiKey))
		{
			apiKey = Environment.GetEnvironmentVariable("HF_TOKEN");
		}

		return string.IsNullOrWhiteSpace(apiKey) ? string.Empty : apiKey.Trim();
	}

	private static string ResolveModel(RunbookRetrievalOptions options)
	{
		var model = options.Model;
		if (string.IsNullOrWhiteSpace(model))
		{
			model = Environment.GetEnvironmentVariable("HF_EMBEDDING_MODEL");
		}

		return string.IsNullOrWhiteSpace(model) ? "thenlper/gte-large" : model.Trim();
	}

	private static string ResolveEndpoint(RunbookRetrievalOptions options)
	{
		return string.IsNullOrWhiteSpace(options.Endpoint)
			? "https://api-inference.huggingface.co/pipeline/feature-extraction/"
			: options.Endpoint.Trim();
	}
}
