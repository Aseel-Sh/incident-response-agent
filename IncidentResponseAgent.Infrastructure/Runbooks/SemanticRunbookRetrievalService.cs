using IncidentResponseAgent.Application.Runbooks;
using IncidentResponseAgent.Domain.Runbooks;
using Microsoft.Extensions.Options;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace IncidentResponseAgent.Infrastructure.Runbooks;

public sealed class SemanticRunbookRetrievalService : IRunbookRetrievalService
{
	private readonly RunbookRetrievalOptions _options;
	private readonly IHttpClientFactory _httpClientFactory;
	private readonly HuggingFaceEmbeddingSettings? _embeddingSettings;
	private readonly object _indexLock = new();
	private Task<RunbookSemanticIndex>? _indexTask;

	public SemanticRunbookRetrievalService(IOptions<RunbookRetrievalOptions> options, IHttpClientFactory httpClientFactory)
	{
		_options = options.Value ?? new RunbookRetrievalOptions();
		_httpClientFactory = httpClientFactory;
		_embeddingSettings = CreateEmbeddingSettings(_options);
	}

	public async Task<RunbookRetrievalResult> RetrieveAsync(RunbookRetrievalRequest request, CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(request);
		cancellationToken.ThrowIfCancellationRequested();

		if (string.IsNullOrWhiteSpace(request.Query))
		{
			throw new ArgumentException("Runbook query cannot be empty.", nameof(request));
		}

		var index = await GetIndexAsync().ConfigureAwait(false);
		var limit = Math.Clamp(request.MaxResults <= 0 ? _options.MaxResults : request.MaxResults, 1, 5);
		var queryTokens = Tokenize(request.Query);
		var serviceTokens = Tokenize(request.ServiceName);
		var environmentTokens = Tokenize(request.Environment);

		IReadOnlyList<ScoredRunbookChunk> matches = await SearchSemanticallyAsync(index, request.Query, limit, cancellationToken).ConfigureAwait(false);

		if (matches.Count == 0)
		{
			matches = SearchLexically(index, queryTokens, serviceTokens, environmentTokens, limit);
		}

		return new RunbookRetrievalResult
		{
			Runbooks = matches.Select(match => match.ToDocument()).ToArray()
		};
	}

	private Task<RunbookSemanticIndex> GetIndexAsync()
	{
		lock (_indexLock)
		{
			_indexTask ??= BuildIndexAsync(_options);
			return _indexTask;
		}
	}

	private async Task<IReadOnlyList<ScoredRunbookChunk>> SearchSemanticallyAsync(
		RunbookSemanticIndex index,
		string query,
		int limit,
		CancellationToken cancellationToken)
	{
		if (_embeddingSettings is null)
		{
			return Array.Empty<ScoredRunbookChunk>();
		}

		float[]? queryVector;
		try
		{
			queryVector = await GenerateEmbeddingAsync(_embeddingSettings, query, cancellationToken).ConfigureAwait(false);
		}
		catch
		{
			return Array.Empty<ScoredRunbookChunk>();
		}

		return index.Entries
			.Select(entry => new ScoredRunbookChunk(entry, CosineSimilarity(queryVector, entry.Embedding)))
			.Where(match => match.Score >= index.Options.MinimumRelevanceScore)
			.OrderByDescending(match => match.Score)
			.ThenBy(match => match.Entry.Document.Title, StringComparer.OrdinalIgnoreCase)
			.Take(limit)
			.ToArray();
	}

	private static IReadOnlyList<ScoredRunbookChunk> SearchLexically(
		RunbookSemanticIndex index,
		HashSet<string> queryTokens,
		HashSet<string> serviceTokens,
		HashSet<string> environmentTokens,
		int limit)
	{
		return index.Entries
			.Select(entry => new ScoredRunbookChunk(entry, Score(entry, queryTokens, serviceTokens, environmentTokens)))
			.Where(match => match.Score > 0)
			.OrderByDescending(match => match.Score)
			.ThenBy(match => match.Entry.Document.Title, StringComparer.OrdinalIgnoreCase)
			.Take(limit)
			.ToArray();
	}

	private static double Score(RunbookChunkEntry entry, HashSet<string> queryTokens, HashSet<string> serviceTokens, HashSet<string> environmentTokens)
	{
		var haystack = Tokenize(entry.Chunk.SearchText);
		var score = haystack.Overlaps(queryTokens) ? 2 : 0;

		foreach (var token in queryTokens)
		{
			if (haystack.Contains(token))
			{
				score += 2;
			}
		}

		foreach (var token in serviceTokens)
		{
			if (haystack.Contains(token))
			{
				score += 3;
			}
		}

		foreach (var token in environmentTokens)
		{
			if (haystack.Contains(token))
			{
				score += 1;
			}
		}

		if (entry.Document.Tags.Any(tag => serviceTokens.Contains(tag)))
		{
			score += 4;
		}

		if (entry.Document.Tags.Any(tag => queryTokens.Contains(tag)))
		{
			score += 3;
		}

		return score;
	}

	private static HashSet<string> Tokenize(string? value)
	{
		if (string.IsNullOrWhiteSpace(value))
		{
			return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		}

		return System.Text.RegularExpressions.Regex.Matches(value.ToLowerInvariant(), "[a-z0-9]+")
			.Select(match => match.Value)
			.ToHashSet(StringComparer.OrdinalIgnoreCase);
	}

	private static double CosineSimilarity(float[] left, float[] right)
	{
		var length = Math.Min(left.Length, right.Length);
		if (length == 0)
		{
			return 0;
		}

		double dot = 0;
		double leftMagnitude = 0;
		double rightMagnitude = 0;

		for (var index = 0; index < length; index++)
		{
			var leftValue = left[index];
			var rightValue = right[index];
			dot += leftValue * rightValue;
			leftMagnitude += leftValue * leftValue;
			rightMagnitude += rightValue * rightValue;
		}

		if (leftMagnitude <= 0 || rightMagnitude <= 0)
		{
			return 0;
		}

		return dot / (Math.Sqrt(leftMagnitude) * Math.Sqrt(rightMagnitude));
	}

	private async Task<RunbookSemanticIndex> BuildIndexAsync(RunbookRetrievalOptions options)
	{
		var documents = LoadRunbooks();
		var entries = new List<RunbookChunkEntry>();
		var embeddingSettings = CreateEmbeddingSettings(options);
		if (embeddingSettings is null)
		{
			foreach (var document in documents)
			{
				foreach (var chunk in MarkdownRunbookChunker.Chunk(document))
				{
					entries.Add(new RunbookChunkEntry(document, chunk, Array.Empty<float>()));
				}
			}

			return await Task.FromResult(new RunbookSemanticIndex(entries, options, UseEmbeddings: false)).ConfigureAwait(false);
		}

		foreach (var document in documents)
		{
			foreach (var chunk in MarkdownRunbookChunker.Chunk(document))
			{
				entries.Add(new RunbookChunkEntry(document, chunk, Array.Empty<float>()));
			}
		}

		try
		{
			for (var index = 0; index < entries.Count; index++)
			{
				entries[index] = entries[index] with { Embedding = await GenerateEmbeddingAsync(embeddingSettings, entries[index].Chunk.SearchText, CancellationToken.None).ConfigureAwait(false) };
			}

			return await Task.FromResult(new RunbookSemanticIndex(entries, options, UseEmbeddings: true)).ConfigureAwait(false);
		}
		catch
		{
			return await Task.FromResult(new RunbookSemanticIndex(entries, options, UseEmbeddings: false)).ConfigureAwait(false);
		}
	}

	private static IReadOnlyList<RunbookDocument> LoadRunbooks()
	{
		var directory = Path.Combine(AppContext.BaseDirectory, "KnowledgeBase", "Runbooks");
		if (!Directory.Exists(directory))
		{
			return Array.Empty<RunbookDocument>();
		}

		var files = Directory.EnumerateFiles(directory, "*.md", SearchOption.TopDirectoryOnly)
			.OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
			.ToArray();

		var runbooks = new List<RunbookDocument>(files.Length);
		foreach (var filePath in files)
		{
			var content = File.ReadAllText(filePath);
			runbooks.Add(ParseDocument(filePath, content));
		}

		return runbooks;
	}

	private static RunbookDocument ParseDocument(string filePath, string content)
	{
		var fileName = Path.GetFileNameWithoutExtension(filePath);
		var lines = content.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
		var title = lines.FirstOrDefault(line => line.StartsWith("# ", StringComparison.Ordinal))?.TrimStart('#', ' ').Trim() ?? fileName;
		var summary = ExtractPurpose(lines) ?? ExtractSummary(lines) ?? title;
		var tags = ExtractTags(lines);

		return new RunbookDocument(fileName, title, summary, content, tags);
	}

	private static string? ExtractPurpose(string[] lines)
	{
		for (var index = 0; index < lines.Length; index++)
		{
			if (!string.Equals(lines[index].Trim(), "## Purpose", StringComparison.OrdinalIgnoreCase))
			{
				continue;
			}

			var fragments = new List<string>();
			for (var cursor = index + 1; cursor < lines.Length; cursor++)
			{
				var line = lines[cursor].Trim();
				if (string.IsNullOrWhiteSpace(line))
				{
					if (fragments.Count > 0)
					{
						break;
					}

					continue;
				}

				if (line.StartsWith("---", StringComparison.Ordinal) || line.StartsWith("## ", StringComparison.Ordinal) || line.StartsWith("- ", StringComparison.Ordinal) || line.StartsWith("1.", StringComparison.Ordinal))
				{
					break;
				}

				fragments.Add(line);
			}

			if (fragments.Count > 0)
			{
				return string.Join(' ', fragments);
			}
		}

		return null;
	}

	private static string? ExtractSummary(string[] lines)
	{
		foreach (var rawLine in lines)
		{
			var line = rawLine.Trim();
			if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#", StringComparison.Ordinal) || line.StartsWith("-", StringComparison.Ordinal))
			{
				continue;
			}

			if (line.Length >= 40)
			{
				return line;
			}
		}

		return null;
	}

	private static IReadOnlyCollection<string> ExtractTags(string[] lines)
	{
		var tags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

		foreach (var line in lines)
		{
			var trimmed = line.Trim();
			if (trimmed.StartsWith("tags:", StringComparison.OrdinalIgnoreCase))
			{
				var value = trimmed[5..].Trim();
				foreach (var token in value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
				{
					if (!string.IsNullOrWhiteSpace(token))
					{
						tags.Add(token.Trim());
					}
				}
			}
		}

		var metadataKeys = new[] { "Service/System", "Incident Type", "Severity Range", "Owner Team", "Primary On-Call Role" };
		foreach (var key in metadataKeys)
		{
			var value = ExtractMetadataValue(lines, key);
			if (string.IsNullOrWhiteSpace(value))
			{
				continue;
			}

			foreach (var token in Tokenize(value))
			{
				tags.Add(token);
			}
		}

		return tags.ToArray();
	}

	private static string? ExtractMetadataValue(string[] lines, string metadataKey)
	{
		var prefix = $"- **{metadataKey}:**";
		foreach (var rawLine in lines)
		{
			var trimmed = rawLine.Trim();
			if (trimmed.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
			{
				return trimmed[prefix.Length..].Trim();
			}
		}

		return null;
	}

	private async Task<float[]> GenerateEmbeddingAsync(HuggingFaceEmbeddingSettings settings, string text, CancellationToken cancellationToken)
	{
		if (string.IsNullOrWhiteSpace(text))
		{
			return Array.Empty<float>();
		}

		var httpClient = _httpClientFactory.CreateClient();
		using var request = new HttpRequestMessage(HttpMethod.Post, BuildEmbeddingUri(settings));
		request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", settings.ApiKey);
		request.Content = JsonContent.Create(new
		{
			inputs = text,
			normalize = true,
			truncate = true
		});

		using var response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
		response.EnsureSuccessStatusCode();

		await using var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
		using var jsonDocument = await JsonDocument.ParseAsync(responseStream, cancellationToken: cancellationToken).ConfigureAwait(false);
		return ParseEmbedding(jsonDocument.RootElement);
	}

	private static Uri BuildEmbeddingUri(HuggingFaceEmbeddingSettings settings)
	{
		var baseEndpoint = settings.Endpoint.EndsWith("/", StringComparison.Ordinal) ? settings.Endpoint : settings.Endpoint + "/";
		return new Uri(baseEndpoint + Uri.EscapeDataString(settings.Model), UriKind.Absolute);
	}

	private static float[] ParseEmbedding(JsonElement element)
	{
		if (element.ValueKind != JsonValueKind.Array)
		{
			return Array.Empty<float>();
		}

		if (element.GetArrayLength() == 0)
		{
			return Array.Empty<float>();
		}

		var first = element[0];
		if (first.ValueKind == JsonValueKind.Array)
		{
			return first.EnumerateArray().Select(value => (float)value.GetDouble()).ToArray();
		}

		return element.EnumerateArray().Select(value => (float)value.GetDouble()).ToArray();
	}

	private static HuggingFaceEmbeddingSettings? CreateEmbeddingSettings(RunbookRetrievalOptions options)
	{
		var apiKey = options.ApiKey;
		if (string.IsNullOrWhiteSpace(apiKey))
		{
			apiKey = Environment.GetEnvironmentVariable("HF_TOKEN");
		}

		if (string.IsNullOrWhiteSpace(apiKey))
		{
			return null;
		}

		var model = options.Model;
		if (string.IsNullOrWhiteSpace(model))
		{
			model = Environment.GetEnvironmentVariable("HF_EMBEDDING_MODEL");
		}

		if (string.IsNullOrWhiteSpace(model))
		{
			model = "thenlper/gte-large";
		}

		var endpoint = options.Endpoint;
		if (string.IsNullOrWhiteSpace(endpoint))
		{
			endpoint = "https://api-inference.huggingface.co/pipeline/feature-extraction/";
		}

		return new HuggingFaceEmbeddingSettings(endpoint.Trim(), model.Trim(), apiKey.Trim());
	}

	private sealed record RunbookSemanticIndex(
		IReadOnlyList<RunbookChunkEntry> Entries,
		RunbookRetrievalOptions Options,
		bool UseEmbeddings);

	private sealed record HuggingFaceEmbeddingSettings(string Endpoint, string Model, string ApiKey);

	private sealed record RunbookChunkEntry(RunbookDocument Document, RunbookChunk Chunk, float[] Embedding);

	private sealed record ScoredRunbookChunk(RunbookChunkEntry Entry, double Score)
	{
		public RunbookDocument ToDocument()
		{
			var title = string.IsNullOrWhiteSpace(Entry.Chunk.SectionPath)
				? Entry.Document.Title
				: $"{Entry.Document.Title} - {Entry.Chunk.SectionPath}";

			return new RunbookDocument(
				$"{Entry.Document.Id}-{Entry.Chunk.Ordinal}",
				title,
				BuildSummary(Entry.Chunk.Text),
				Entry.Chunk.SearchText,
				Entry.Document.Tags);
		}

		private static string BuildSummary(string text)
		{
			var normalized = text.Replace("\r", " ").Replace("\n", " ");
			return normalized.Length <= 240 ? normalized : normalized[..240].TrimEnd() + "...";
		}
	}
}
