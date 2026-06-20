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
	private readonly TimeSpan _timeout;

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
		_timeout = TimeSpan.FromSeconds(Math.Clamp(options.EmbeddingTimeoutSeconds, 3, 60));
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
		using var response = await SendAsync(httpClient, request, cancellationToken).ConfigureAwait(false);
		if (!response.IsSuccessStatusCode)
		{
			var errorBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
			throw new HttpRequestException(
				$"Hugging Face embedding request failed with HTTP {(int)response.StatusCode} {response.ReasonPhrase}: {Sanitize(errorBody)}",
				null,
				response.StatusCode);
		}

		await using var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
		using var jsonDocument = await JsonDocument.ParseAsync(responseStream, cancellationToken: cancellationToken).ConfigureAwait(false);
		return ParseEmbedding(jsonDocument.RootElement);
	}

	private async Task<HttpResponseMessage> SendAsync(HttpClient httpClient, HttpRequestMessage request, CancellationToken cancellationToken)
	{
		using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
		timeoutCts.CancelAfter(_timeout);
		try
		{
			return await httpClient.SendAsync(request, timeoutCts.Token).ConfigureAwait(false);
		}
		catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
		{
			throw new TimeoutException($"Hugging Face embedding request timed out after {_timeout.TotalSeconds:0} seconds at {new Uri(_endpoint).Host}.");
		}
	}

	private Uri BuildEmbeddingUri()
	{
		var baseEndpoint = _endpoint.EndsWith("/", StringComparison.Ordinal) ? _endpoint : _endpoint + "/";
		var modelPath = string.Join('/', ModelName.Split('/').Select(Uri.EscapeDataString));
		return new Uri(baseEndpoint + modelPath, UriKind.Absolute);
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
		if (string.IsNullOrWhiteSpace(apiKey)) apiKey = Environment.GetEnvironmentVariable("HF_API_TOKEN");

		return string.IsNullOrWhiteSpace(apiKey) ? string.Empty : apiKey.Trim();
	}

	private static string ResolveModel(RunbookRetrievalOptions options)
	{
		var model = options.Model;
		if (string.IsNullOrWhiteSpace(model))
		{
			model = Environment.GetEnvironmentVariable("HF_EMBEDDING_MODEL");
		}

		return string.IsNullOrWhiteSpace(model) ? "BAAI/bge-small-en-v1.5" : model.Trim();
	}

	private static string ResolveEndpoint(RunbookRetrievalOptions options)
	{
		return string.IsNullOrWhiteSpace(options.Endpoint)
			? "https://router.huggingface.co/hf-inference/models/"
			: options.Endpoint.Trim();
	}

	private static string Sanitize(string value)
	{
		var text = string.IsNullOrWhiteSpace(value) ? "no response body" : value.ReplaceLineEndings(" ").Trim();
		return text.Length <= 300 ? text : string.Concat(text.AsSpan(0, 297), "...");
	}
}
