using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace IncidentResponseAgent.Infrastructure.Runbooks;

internal sealed class QdrantRunbookVectorStore
{
	private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
	{
		PropertyNameCaseInsensitive = true
	};

	private readonly HttpClient _httpClient;
	private readonly ILogger<QdrantRunbookVectorStore> _logger;
	private readonly RunbookRetrievalOptions _options;
	private bool _available = true;

	public QdrantRunbookVectorStore(
		RunbookRetrievalOptions options,
		IHttpClientFactory httpClientFactory,
		ILogger<QdrantRunbookVectorStore> logger)
	{
		_options = options;
		_logger = logger;
		_httpClient = httpClientFactory.CreateClient();
		_httpClient.BaseAddress = new Uri(NormalizeEndpoint(options.QdrantEndpoint));
		_httpClient.Timeout = TimeSpan.FromSeconds(Math.Clamp(options.QdrantTimeoutSeconds, 1, 30));

		var apiKey = ResolveApiKey(options);
		if (!string.IsNullOrWhiteSpace(apiKey))
		{
			_httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
		}
	}

	public string ProviderName => _available ? "qdrant" : "qdrant-unavailable/sqlite-fallback";

	public string Endpoint => _httpClient.BaseAddress?.ToString().TrimEnd('/') ?? _options.QdrantEndpoint;

	public string CollectionName => _options.QdrantCollectionName;

	public async Task<bool> EnsureCollectionAsync(int vectorSize, CancellationToken cancellationToken)
	{
		if (!_available || vectorSize <= 0)
		{
			return false;
		}

		try
		{
			var body = new
			{
				vectors = new
				{
					size = vectorSize,
					distance = "Cosine"
				}
			};
			using var response = await _httpClient.PutAsJsonAsync(
				$"/collections/{Uri.EscapeDataString(CollectionName)}",
				body,
				SerializerOptions,
				cancellationToken).ConfigureAwait(false);

			if (!response.IsSuccessStatusCode)
			{
				_available = false;
				_logger.LogWarning("Qdrant collection setup failed with status {StatusCode}. Falling back to SQLite vector search.", response.StatusCode);
				return false;
			}

			return true;
		}
		catch (OperationCanceledException)
		{
			throw;
		}
		catch (Exception exception)
		{
			_available = false;
			_logger.LogWarning(exception, "Qdrant collection setup failed. Falling back to SQLite vector search.");
			return false;
		}
	}

	public async Task<bool> UpsertAsync(IReadOnlyCollection<RunbookVectorPoint> points, CancellationToken cancellationToken)
	{
		if (!_available || points.Count == 0)
		{
			return false;
		}

		try
		{
			var body = new
			{
				points = points.Select(point => new
				{
					id = CreatePointId(point.RunbookId, point.Ordinal),
					vector = point.Embedding,
					payload = new
					{
						runbookId = point.RunbookId,
						documentTitle = point.DocumentTitle,
						documentSummary = point.DocumentSummary,
						tags = point.Tags,
						sourcePath = point.SourcePath,
						ordinal = point.Ordinal,
						sectionPath = point.SectionPath,
						text = point.Text,
						searchText = point.SearchText
					}
				}).ToArray()
			};

			using var response = await _httpClient.PutAsJsonAsync(
				$"/collections/{Uri.EscapeDataString(CollectionName)}/points?wait=true",
				body,
				SerializerOptions,
				cancellationToken).ConfigureAwait(false);

			if (!response.IsSuccessStatusCode)
			{
				_available = false;
				_logger.LogWarning("Qdrant upsert failed with status {StatusCode}. SQLite vector search remains available.", response.StatusCode);
				return false;
			}

			_logger.LogInformation("Upserted {Count} runbook chunks into Qdrant collection {CollectionName}.", points.Count, CollectionName);
			return true;
		}
		catch (OperationCanceledException)
		{
			throw;
		}
		catch (Exception exception)
		{
			_available = false;
			_logger.LogWarning(exception, "Qdrant upsert failed. SQLite vector search remains available.");
			return false;
		}
	}

	public async Task<IReadOnlyList<QdrantRunbookMatch>> SearchAsync(float[] queryVector, int limit, CancellationToken cancellationToken)
	{
		if (!_available || queryVector.Length == 0)
		{
			return Array.Empty<QdrantRunbookMatch>();
		}

		try
		{
			var body = new
			{
				vector = queryVector,
				limit,
				with_payload = true
			};

			using var response = await _httpClient.PostAsJsonAsync(
				$"/collections/{Uri.EscapeDataString(CollectionName)}/points/search",
				body,
				SerializerOptions,
				cancellationToken).ConfigureAwait(false);

			if (!response.IsSuccessStatusCode)
			{
				_available = false;
				_logger.LogWarning("Qdrant search failed with status {StatusCode}. Falling back to SQLite vector search.", response.StatusCode);
				return Array.Empty<QdrantRunbookMatch>();
			}

			await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
			var envelope = await JsonSerializer.DeserializeAsync<QdrantSearchEnvelope>(stream, SerializerOptions, cancellationToken).ConfigureAwait(false);
			return envelope?.Result?.Select(ToMatch).Where(match => match is not null).Cast<QdrantRunbookMatch>().ToArray()
				?? Array.Empty<QdrantRunbookMatch>();
		}
		catch (OperationCanceledException)
		{
			throw;
		}
		catch (Exception exception)
		{
			_available = false;
			_logger.LogWarning(exception, "Qdrant search failed. Falling back to SQLite vector search.");
			return Array.Empty<QdrantRunbookMatch>();
		}
	}

	private static QdrantRunbookMatch? ToMatch(QdrantScoredPoint point)
	{
		if (point.Payload is null)
		{
			return null;
		}

		var payload = point.Payload;
		return new QdrantRunbookMatch(
			RunbookId: payload.RunbookId ?? string.Empty,
			DocumentTitle: payload.DocumentTitle ?? string.Empty,
			DocumentSummary: payload.DocumentSummary ?? string.Empty,
			Tags: payload.Tags ?? Array.Empty<string>(),
			SourcePath: payload.SourcePath ?? string.Empty,
			Ordinal: payload.Ordinal,
			SectionPath: payload.SectionPath ?? string.Empty,
			Text: payload.Text ?? string.Empty,
			SearchText: payload.SearchText ?? string.Empty,
			Score: point.Score);
	}

	private static string NormalizeEndpoint(string endpoint)
	{
		return string.IsNullOrWhiteSpace(endpoint) ? "http://localhost:6333" : endpoint.Trim().TrimEnd('/');
	}

	private static string ResolveApiKey(RunbookRetrievalOptions options)
	{
		if (!string.IsNullOrWhiteSpace(options.QdrantApiKey))
		{
			return options.QdrantApiKey.Trim();
		}

		var value = Environment.GetEnvironmentVariable("QDRANT_API_KEY");
		return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
	}

	private static string CreatePointId(string runbookId, int ordinal)
	{
		var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"{runbookId}:{ordinal}"));
		bytes[6] = (byte)((bytes[6] & 0x0F) | 0x40);
		bytes[8] = (byte)((bytes[8] & 0x3F) | 0x80);
		return new Guid(bytes[..16]).ToString();
	}

	private sealed record QdrantSearchEnvelope(IReadOnlyList<QdrantScoredPoint>? Result);

	private sealed record QdrantScoredPoint(double Score, QdrantPayload? Payload);

	private sealed record QdrantPayload
	{
		public string? RunbookId { get; init; }

		public string? DocumentTitle { get; init; }

		public string? DocumentSummary { get; init; }

		public IReadOnlyCollection<string>? Tags { get; init; }

		public string? SourcePath { get; init; }

		public int Ordinal { get; init; }

		public string? SectionPath { get; init; }

		public string? Text { get; init; }

		public string? SearchText { get; init; }
	}
}

internal sealed record RunbookVectorPoint(
	string RunbookId,
	string DocumentTitle,
	string DocumentSummary,
	IReadOnlyCollection<string> Tags,
	string SourcePath,
	int Ordinal,
	string SectionPath,
	string Text,
	string SearchText,
	float[] Embedding);

internal sealed record QdrantRunbookMatch(
	string RunbookId,
	string DocumentTitle,
	string DocumentSummary,
	IReadOnlyCollection<string> Tags,
	string SourcePath,
	int Ordinal,
	string SectionPath,
	string Text,
	string SearchText,
	double Score);
