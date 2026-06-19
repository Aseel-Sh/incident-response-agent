using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using IncidentResponseAgent.Application.Runbooks;
using IncidentResponseAgent.Domain.Runbooks;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace IncidentResponseAgent.Infrastructure.Runbooks;

public sealed class SemanticRunbookRetrievalService : IRunbookRetrievalService, IRunbookRetrievalDiagnosticsService
{
	private const string IndexVersion = "rag-index-v2";
	private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
	private readonly IRunbookEmbeddingProvider _embeddingProvider;
	private readonly ILogger<SemanticRunbookRetrievalService> _logger;
	private readonly RunbookRetrievalOptions _options;
	private readonly QdrantRunbookVectorStore? _qdrantVectorStore;
	private readonly SemaphoreSlim _indexLock = new(1, 1);
	private bool _isInitialized;
	private string? _sourceFingerprint;

	public SemanticRunbookRetrievalService(
		IOptions<RunbookRetrievalOptions> options,
		IHttpClientFactory httpClientFactory,
		ILoggerFactory loggerFactory,
		ILogger<SemanticRunbookRetrievalService> logger)
	{
		_options = options.Value ?? new RunbookRetrievalOptions();
		_logger = logger;
		var fallback = new LocalHashingRunbookEmbeddingProvider(_options);
		var primary = HuggingFaceRunbookEmbeddingProvider.IsConfigured(_options)
			? new HuggingFaceRunbookEmbeddingProvider(
				_options,
				httpClientFactory,
				loggerFactory.CreateLogger<HuggingFaceRunbookEmbeddingProvider>())
			: null;
		_embeddingProvider = new ResilientRunbookEmbeddingProvider(
			primary,
			fallback,
			loggerFactory.CreateLogger<ResilientRunbookEmbeddingProvider>());
		_qdrantVectorStore = IsQdrantEnabled(_options)
			? new QdrantRunbookVectorStore(
				_options,
				httpClientFactory,
				loggerFactory.CreateLogger<QdrantRunbookVectorStore>())
			: null;
	}

	public async Task<RunbookRetrievalResult> RetrieveAsync(RunbookRetrievalRequest request, CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(request);
		cancellationToken.ThrowIfCancellationRequested();

		if (string.IsNullOrWhiteSpace(request.Query))
		{
			throw new ArgumentException("Runbook query cannot be empty.", nameof(request));
		}

		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		var queryVector = await _embeddingProvider.GenerateEmbeddingAsync(BuildQueryText(request.Query, request.ServiceName, request.Environment), cancellationToken).ConfigureAwait(false);
		var limit = Math.Clamp(request.MaxResults <= 0 ? _options.MaxResults : request.MaxResults, 1, 8);
		var retrieval = await GetScoredMatchesAsync(
			request.Query,
			request.ServiceName,
			request.Environment,
			queryVector,
			limit,
			cancellationToken).ConfigureAwait(false);

		return new RunbookRetrievalResult
		{
			Runbooks = retrieval.Matches.Select(match => match.ToDocument()).ToArray(),
			EmbeddingProvider = _embeddingProvider.ProviderName,
			VectorStoreProvider = retrieval.VectorStoreProvider,
			RagStatus = retrieval.Matches.Count > 0 ? "available" : "no matches",
			IsDegraded = _embeddingProvider.IsDegraded || retrieval.IsDegraded,
			DegradedReason = _embeddingProvider.DegradedReason ?? retrieval.DegradedReason
		};
	}

	public async Task<RunbookRetrievalDiagnosticsResult> SearchAsync(
		RunbookRetrievalDiagnosticsRequest request,
		CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(request);
		cancellationToken.ThrowIfCancellationRequested();

		if (string.IsNullOrWhiteSpace(request.Query))
		{
			throw new ArgumentException("Runbook query cannot be empty.", nameof(request));
		}

		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		var queryVector = await _embeddingProvider.GenerateEmbeddingAsync(
			BuildQueryText(request.Query, request.ServiceName, request.Environment),
			cancellationToken).ConfigureAwait(false);
		var limit = Math.Clamp(request.MaxResults <= 0 ? _options.MaxResults : request.MaxResults, 1, 20);
		var retrieval = await GetScoredMatchesAsync(
			request.Query,
			request.ServiceName,
			request.Environment,
			queryVector,
			limit,
			cancellationToken).ConfigureAwait(false);

		return new RunbookRetrievalDiagnosticsResult
		{
			EmbeddingProvider = _embeddingProvider.ProviderName,
			EmbeddingModel = _embeddingProvider.ModelName,
			VectorStoreProvider = retrieval.VectorStoreProvider,
			VectorStoreEndpoint = _qdrantVectorStore?.Endpoint,
			VectorStoreCollection = _qdrantVectorStore?.CollectionName,
			DatabasePath = ResolveDatabasePath(),
			KnowledgeBasePath = ResolveKnowledgeBasePath(),
			RagStatus = retrieval.Matches.Count > 0 ? "available" : "no matches",
			IsDegraded = _embeddingProvider.IsDegraded || retrieval.IsDegraded,
			DegradedReason = _embeddingProvider.DegradedReason ?? retrieval.DegradedReason,
			Matches = retrieval.Matches.Select(match => match.ToDiagnosticMatch()).ToArray()
		};
	}

	private async Task<ScoredRetrieval> GetScoredMatchesAsync(
		string query,
		string? serviceName,
		string? environment,
		float[] queryVector,
		int limit,
		CancellationToken cancellationToken)
	{
		var queryTokens = RunbookTextAnalysis.Tokenize(query);
		var serviceTokens = RunbookTextAnalysis.Tokenize(serviceName);
		var environmentTokens = RunbookTextAnalysis.Tokenize(environment);
		var qdrantMatches = _qdrantVectorStore is null
			? Array.Empty<QdrantRunbookMatch>()
			: await _qdrantVectorStore.SearchAsync(queryVector, limit, cancellationToken).ConfigureAwait(false);

		if (qdrantMatches.Count > 0)
		{
			var matches = qdrantMatches
				.Select(match => ToScoredRunbookChunk(match, queryTokens, serviceTokens, environmentTokens))
				.Where(match => match.Score >= _options.MinimumRelevanceScore || HasLexicalOverlap(match.Chunk, queryTokens, serviceTokens, environmentTokens))
				.OrderByDescending(match => match.Score)
				.ThenBy(match => match.Chunk.DocumentTitle, StringComparer.OrdinalIgnoreCase)
				.ThenBy(match => match.Chunk.Ordinal)
				.ToArray();

			return new ScoredRetrieval(DiversifyBySection(matches).Take(limit).ToArray(), "qdrant", false, null);
		}

		var chunks = await LoadChunksAsync(cancellationToken).ConfigureAwait(false);

		var scored = chunks
			.Select(chunk => new ScoredRunbookChunk(chunk, Score(chunk, queryVector, queryTokens, serviceTokens, environmentTokens)))
			.Where(match => match.Score >= _options.MinimumRelevanceScore || HasLexicalOverlap(match.Chunk, queryTokens, serviceTokens, environmentTokens))
			.OrderByDescending(match => match.Score)
			.ThenBy(match => match.Chunk.DocumentTitle, StringComparer.OrdinalIgnoreCase)
			.ThenBy(match => match.Chunk.Ordinal)
			.ToArray();

		var qdrantDegraded = _qdrantVectorStore is not null && _qdrantVectorStore.ProviderName.Contains("unavailable", StringComparison.OrdinalIgnoreCase);
		return new ScoredRetrieval(
			DiversifyBySection(scored).Take(limit).ToArray(),
			qdrantDegraded ? "qdrant-unavailable/sqlite" : "sqlite",
			qdrantDegraded,
			qdrantDegraded ? "Qdrant is unavailable; SQLite vector search served this query." : null);
	}

	private async Task EnsureInitializedAsync(CancellationToken cancellationToken)
	{
		var sourceFingerprint = ComputeSourceFingerprint();
		if (_isInitialized && string.Equals(_sourceFingerprint, sourceFingerprint, StringComparison.Ordinal))
		{
			return;
		}

		await _indexLock.WaitAsync(cancellationToken).ConfigureAwait(false);
		try
		{
			sourceFingerprint = ComputeSourceFingerprint();
			if (_isInitialized && string.Equals(_sourceFingerprint, sourceFingerprint, StringComparison.Ordinal))
			{
				return;
			}

			await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
			await EnsureSchemaAsync(connection, cancellationToken).ConfigureAwait(false);
			await IndexMarkdownRunbooksAsync(connection, cancellationToken).ConfigureAwait(false);
			await UpsertSqliteIndexToQdrantAsync(connection, cancellationToken).ConfigureAwait(false);
			_logger.LogInformation(
				"Runbook RAG index ready. Provider={EmbeddingProvider} Model={EmbeddingModel} DatabasePath={DatabasePath} KnowledgeBasePath={KnowledgeBasePath}",
				_embeddingProvider.ProviderName,
				_embeddingProvider.ModelName,
				ResolveDatabasePath(),
				ResolveKnowledgeBasePath());
			_isInitialized = true;
			_sourceFingerprint = sourceFingerprint;
		}
		finally
		{
			_indexLock.Release();
		}
	}

	private async Task IndexMarkdownRunbooksAsync(SqliteConnection connection, CancellationToken cancellationToken)
	{
		var sources = LoadRunbookSources();
		var currentDocumentIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

		foreach (var source in sources)
		{
			cancellationToken.ThrowIfCancellationRequested();

			var document = ParseDocument(source.Path, source.Content);
			currentDocumentIds.Add(document.Id);
			var contentHash = ComputeHash($"{IndexVersion}\n{source.Content}");
			var existingState = await GetExistingDocumentIndexStateAsync(connection, document.Id, cancellationToken).ConfigureAwait(false);
			if (existingState is not null &&
			    string.Equals(existingState.ContentHash, contentHash, StringComparison.Ordinal) &&
			    string.Equals(existingState.EmbeddingProvider, _embeddingProvider.ProviderName, StringComparison.Ordinal) &&
			    string.Equals(existingState.EmbeddingModel, _embeddingProvider.ModelName, StringComparison.Ordinal))
			{
				continue;
			}

			using var transaction = connection.BeginTransaction();
			await UpsertDocumentAsync(connection, transaction, document, source.Path, contentHash, cancellationToken).ConfigureAwait(false);
			await DeleteChunksAsync(connection, transaction, document.Id, cancellationToken).ConfigureAwait(false);
			var vectorPoints = new List<RunbookVectorPoint>();

			foreach (var chunk in MarkdownRunbookChunker.Chunk(document))
			{
				var embedding = await _embeddingProvider.GenerateEmbeddingAsync(chunk.SearchText, cancellationToken).ConfigureAwait(false);
				await InsertChunkAsync(connection, transaction, document, chunk, embedding, cancellationToken).ConfigureAwait(false);
				vectorPoints.Add(new RunbookVectorPoint(
					document.Id,
					document.Title,
					document.Summary,
					document.Tags,
					source.Path,
					chunk.Ordinal,
					chunk.SectionPath,
					chunk.Text,
					chunk.SearchText,
					embedding));
			}

			await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
			if (_qdrantVectorStore is not null && vectorPoints.Count > 0)
			{
				var collectionReady = await _qdrantVectorStore.EnsureCollectionAsync(vectorPoints[0].Embedding.Length, cancellationToken).ConfigureAwait(false);
				if (collectionReady)
				{
					await _qdrantVectorStore.UpsertAsync(vectorPoints, cancellationToken).ConfigureAwait(false);
				}
			}
			_logger.LogInformation(
				"Indexed runbook {RunbookId} with embedding provider {EmbeddingProvider} and model {EmbeddingModel}.",
				document.Id,
				_embeddingProvider.ProviderName,
				_embeddingProvider.ModelName);
		}

		var removedDocumentIds = await DeleteMissingDocumentsAsync(connection, currentDocumentIds, cancellationToken).ConfigureAwait(false);
		if (_qdrantVectorStore is not null)
		{
			foreach (var removedDocumentId in removedDocumentIds)
			{
				await _qdrantVectorStore.DeleteRunbookAsync(removedDocumentId, cancellationToken).ConfigureAwait(false);
			}
		}
	}

	private async Task UpsertSqliteIndexToQdrantAsync(SqliteConnection connection, CancellationToken cancellationToken)
	{
		if (_qdrantVectorStore is null)
		{
			return;
		}

		var chunks = await LoadChunksAsync(connection, cancellationToken).ConfigureAwait(false);
		if (chunks.Count == 0)
		{
			return;
		}

		var collectionReady = await _qdrantVectorStore.EnsureCollectionAsync(chunks[0].Embedding.Length, cancellationToken).ConfigureAwait(false);
		if (!collectionReady)
		{
			return;
		}

		await _qdrantVectorStore.UpsertAsync(
			chunks.Select(ToVectorPoint).ToArray(),
			cancellationToken).ConfigureAwait(false);
	}

	private async Task<RunbookDocumentIndexState?> GetExistingDocumentIndexStateAsync(SqliteConnection connection, string documentId, CancellationToken cancellationToken)
	{
		await using var command = connection.CreateCommand();
		command.CommandText = """
select d.content_hash, c.embedding_provider, c.embedding_model
from runbook_documents d
left join runbook_chunks c on c.document_id = d.id
where d.id = $id
order by c.ordinal
limit 1;
""";
		command.Parameters.AddWithValue("$id", documentId);
		await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
		if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
		{
			return null;
		}

		return new RunbookDocumentIndexState(
			reader.GetString(0),
			reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
			reader.IsDBNull(2) ? string.Empty : reader.GetString(2));
	}

	private async Task UpsertDocumentAsync(
		SqliteConnection connection,
		SqliteTransaction transaction,
		RunbookDocument document,
		string sourcePath,
		string contentHash,
		CancellationToken cancellationToken)
	{
		await using var command = connection.CreateCommand();
		command.Transaction = transaction;
		command.CommandText = """
insert into runbook_documents (id, title, summary, content, tags_json, source_path, content_hash, indexed_at_utc)
values ($id, $title, $summary, $content, $tagsJson, $sourcePath, $contentHash, $indexedAtUtc)
on conflict(id) do update set
	title = excluded.title,
	summary = excluded.summary,
	content = excluded.content,
	tags_json = excluded.tags_json,
	source_path = excluded.source_path,
	content_hash = excluded.content_hash,
	indexed_at_utc = excluded.indexed_at_utc;
""";
		command.Parameters.AddWithValue("$id", document.Id);
		command.Parameters.AddWithValue("$title", document.Title);
		command.Parameters.AddWithValue("$summary", document.Summary);
		command.Parameters.AddWithValue("$content", document.Content);
		command.Parameters.AddWithValue("$tagsJson", JsonSerializer.Serialize(document.Tags, SerializerOptions));
		command.Parameters.AddWithValue("$sourcePath", sourcePath);
		command.Parameters.AddWithValue("$contentHash", contentHash);
		command.Parameters.AddWithValue("$indexedAtUtc", DateTimeOffset.UtcNow.ToString("O"));
		await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
	}

	private static async Task DeleteChunksAsync(
		SqliteConnection connection,
		SqliteTransaction transaction,
		string documentId,
		CancellationToken cancellationToken)
	{
		await using var command = connection.CreateCommand();
		command.Transaction = transaction;
		command.CommandText = "delete from runbook_chunks where document_id = $documentId;";
		command.Parameters.AddWithValue("$documentId", documentId);
		await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
	}

	private async Task<IReadOnlyList<string>> DeleteMissingDocumentsAsync(
		SqliteConnection connection,
		HashSet<string> currentDocumentIds,
		CancellationToken cancellationToken)
	{
		var existingIds = await LoadDocumentIdsAsync(connection, cancellationToken).ConfigureAwait(false);
		var missingIds = existingIds
			.Where(id => !currentDocumentIds.Contains(id))
			.ToArray();

		if (missingIds.Length == 0)
		{
			return Array.Empty<string>();
		}

		using var transaction = connection.BeginTransaction();
		foreach (var missingId in missingIds)
		{
			await DeleteChunksAsync(connection, transaction, missingId, cancellationToken).ConfigureAwait(false);
			await DeleteDocumentAsync(connection, transaction, missingId, cancellationToken).ConfigureAwait(false);
		}

		await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
		_logger.LogInformation("Pruned {Count} removed runbooks from the SQLite RAG index.", missingIds.Length);
		return missingIds;
	}

	private static async Task<IReadOnlyList<string>> LoadDocumentIdsAsync(SqliteConnection connection, CancellationToken cancellationToken)
	{
		await using var command = connection.CreateCommand();
		command.CommandText = "select id from runbook_documents;";
		var ids = new List<string>();
		await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
		while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
		{
			ids.Add(reader.GetString(0));
		}

		return ids;
	}

	private static async Task DeleteDocumentAsync(
		SqliteConnection connection,
		SqliteTransaction transaction,
		string documentId,
		CancellationToken cancellationToken)
	{
		await using var command = connection.CreateCommand();
		command.Transaction = transaction;
		command.CommandText = "delete from runbook_documents where id = $documentId;";
		command.Parameters.AddWithValue("$documentId", documentId);
		await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
	}

	private async Task InsertChunkAsync(
		SqliteConnection connection,
		SqliteTransaction transaction,
		RunbookDocument document,
		RunbookChunk chunk,
		float[] embedding,
		CancellationToken cancellationToken)
	{
		await using var command = connection.CreateCommand();
		command.Transaction = transaction;
		command.CommandText = """
insert into runbook_chunks (
	document_id,
	ordinal,
	section_path,
	text,
	search_text,
	embedding,
	embedding_dimensions,
	embedding_provider,
	embedding_model,
	content_hash)
values (
	$documentId,
	$ordinal,
	$sectionPath,
	$text,
	$searchText,
	$embedding,
	$embeddingDimensions,
	$embeddingProvider,
	$embeddingModel,
	$contentHash);
""";
		command.Parameters.AddWithValue("$documentId", document.Id);
		command.Parameters.AddWithValue("$ordinal", chunk.Ordinal);
		command.Parameters.AddWithValue("$sectionPath", chunk.SectionPath);
		command.Parameters.AddWithValue("$text", chunk.Text);
		command.Parameters.AddWithValue("$searchText", chunk.SearchText);
		command.Parameters.Add("$embedding", SqliteType.Blob).Value = SerializeVector(embedding);
		command.Parameters.AddWithValue("$embeddingDimensions", embedding.Length);
		command.Parameters.AddWithValue("$embeddingProvider", _embeddingProvider.ProviderName);
		command.Parameters.AddWithValue("$embeddingModel", _embeddingProvider.ModelName);
		command.Parameters.AddWithValue("$contentHash", ComputeHash(chunk.SearchText));
		await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
	}

	private async Task<IReadOnlyList<RunbookChunkRecord>> LoadChunksAsync(CancellationToken cancellationToken)
	{
		await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
		return await LoadChunksAsync(connection, cancellationToken).ConfigureAwait(false);
	}

	private static async Task<IReadOnlyList<RunbookChunkRecord>> LoadChunksAsync(SqliteConnection connection, CancellationToken cancellationToken)
	{
		await using var command = connection.CreateCommand();
		command.CommandText = """
select
	c.id,
	c.document_id,
	d.title,
	d.summary,
	d.tags_json,
	d.source_path,
	c.ordinal,
	c.section_path,
	c.text,
	c.search_text,
	c.embedding
from runbook_chunks c
join runbook_documents d on d.id = c.document_id
order by d.title, c.ordinal;
""";

		var chunks = new List<RunbookChunkRecord>();
		await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
		while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
		{
			chunks.Add(new RunbookChunkRecord(
				ChunkId: reader.GetInt64(0),
				DocumentId: reader.GetString(1),
				DocumentTitle: reader.GetString(2),
				DocumentSummary: reader.GetString(3),
				Tags: DeserializeTags(reader.GetString(4)),
				SourcePath: reader.GetString(5),
				Ordinal: reader.GetInt32(6),
				SectionPath: reader.GetString(7),
				Text: reader.GetString(8),
				SearchText: reader.GetString(9),
				Embedding: DeserializeVector((byte[])reader["embedding"])));
		}

		return chunks;
	}

	private async Task<SqliteConnection> OpenConnectionAsync(CancellationToken cancellationToken)
	{
		var databasePath = ResolveDatabasePath();
		Directory.CreateDirectory(Path.GetDirectoryName(databasePath)!);

		var connectionString = new SqliteConnectionStringBuilder
		{
			DataSource = databasePath,
			Pooling = false
		}.ToString();
		var connection = new SqliteConnection(connectionString);
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
		return connection;
	}

	private static async Task EnsureSchemaAsync(SqliteConnection connection, CancellationToken cancellationToken)
	{
		await using var command = connection.CreateCommand();
		command.CommandText = """
create table if not exists runbook_documents (
	id text primary key,
	title text not null,
	summary text not null,
	content text not null,
	tags_json text not null,
	source_path text not null,
	content_hash text not null,
	indexed_at_utc text not null
);

create table if not exists runbook_chunks (
	id integer primary key autoincrement,
	document_id text not null,
	ordinal integer not null,
	section_path text not null,
	text text not null,
	search_text text not null,
	embedding blob not null,
	embedding_dimensions integer not null,
	embedding_provider text not null,
	embedding_model text not null,
	content_hash text not null,
	foreign key(document_id) references runbook_documents(id) on delete cascade
);

create index if not exists ix_runbook_chunks_document_id on runbook_chunks(document_id);
create index if not exists ix_runbook_documents_content_hash on runbook_documents(content_hash);
""";
		await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
	}

	private string ResolveDatabasePath()
	{
		if (!string.IsNullOrWhiteSpace(_options.DatabasePath))
		{
			return Path.GetFullPath(Environment.ExpandEnvironmentVariables(_options.DatabasePath));
		}

		return Path.Combine(
			Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
			"IncidentResponseAgent",
			"runbook-rag.sqlite");
	}

	private IReadOnlyList<RunbookSource> LoadRunbookSources()
	{
		var directory = ResolveKnowledgeBasePath();
		if (!Directory.Exists(directory))
		{
			return Array.Empty<RunbookSource>();
		}

		return Directory.EnumerateFiles(directory, "*.md", SearchOption.TopDirectoryOnly)
			.OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
			.Select(path => new RunbookSource(path, File.ReadAllText(path)))
			.ToArray();
	}

	private string ResolveKnowledgeBasePath()
	{
		if (!string.IsNullOrWhiteSpace(_options.KnowledgeBasePath))
		{
			return Path.GetFullPath(Environment.ExpandEnvironmentVariables(_options.KnowledgeBasePath));
		}

		var candidates = new[]
		{
			Path.Combine(AppContext.BaseDirectory, "Runbooks", "KnowledgeBase"),
			Path.Combine(AppContext.BaseDirectory, "KnowledgeBase", "Runbooks"),
			Path.Combine(AppContext.BaseDirectory, "KnowledgeBase")
		};

		return candidates.FirstOrDefault(Directory.Exists) ?? candidates[0];
	}

	private string ComputeSourceFingerprint()
	{
		var directory = ResolveKnowledgeBasePath();
		if (!Directory.Exists(directory)) return "missing";
		var state = Directory.EnumerateFiles(directory, "*.md", SearchOption.TopDirectoryOnly)
			.OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
			.Select(path =>
			{
				var info = new FileInfo(path);
				return $"{info.FullName}|{info.Length}|{info.LastWriteTimeUtc.Ticks}";
			});
		return ComputeHash(string.Join('\n', state));
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

				if (line.StartsWith("---", StringComparison.Ordinal) ||
				    line.StartsWith("## ", StringComparison.Ordinal) ||
				    line.StartsWith("- ", StringComparison.Ordinal) ||
				    line.StartsWith("1.", StringComparison.Ordinal))
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
					tags.Add(token.Trim());
				}
			}
		}

		var metadataKeys = new[] { "Service/System", "Incident Type", "Severity Range", "Owner Team", "Primary On-Call Role" };
		foreach (var key in metadataKeys)
		{
			var value = ExtractMetadataValue(lines, key);
			foreach (var token in RunbookTextAnalysis.Tokenize(value))
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

	private static string BuildQueryText(string query, string? serviceName, string? environment)
	{
		var parts = new[] { query, serviceName, environment };
		return string.Join(' ', parts.Where(part => !string.IsNullOrWhiteSpace(part)));
	}

	private static bool IsQdrantEnabled(RunbookRetrievalOptions options)
	{
		return string.Equals(options.VectorStoreProvider, "Qdrant", StringComparison.OrdinalIgnoreCase);
	}

	private ScoredRunbookChunk ToScoredRunbookChunk(
		QdrantRunbookMatch match,
		HashSet<string> queryTokens,
		HashSet<string> serviceTokens,
		HashSet<string> environmentTokens)
	{
		var chunk = new RunbookChunkRecord(
			ChunkId: 0,
			DocumentId: match.RunbookId,
			DocumentTitle: match.DocumentTitle,
			DocumentSummary: match.DocumentSummary,
			Tags: match.Tags,
			SourcePath: match.SourcePath,
			Ordinal: match.Ordinal,
			SectionPath: match.SectionPath,
			Text: match.Text,
			SearchText: match.SearchText,
			Embedding: Array.Empty<float>());
		var lexicalScore = LexicalScore(chunk, queryTokens, serviceTokens, environmentTokens);
		var semanticWeight = Math.Clamp(_options.SemanticWeight, 0, 1);
		var lexicalWeight = Math.Clamp(_options.LexicalWeight, 0, 1);
		var score = match.Score * semanticWeight + lexicalScore * lexicalWeight;
		return new ScoredRunbookChunk(chunk, score);
	}

	private double Score(
		RunbookChunkRecord chunk,
		float[] queryVector,
		HashSet<string> queryTokens,
		HashSet<string> serviceTokens,
		HashSet<string> environmentTokens)
	{
		var semanticScore = RunbookTextAnalysis.CosineSimilarity(queryVector, chunk.Embedding);
		var lexicalScore = LexicalScore(chunk, queryTokens, serviceTokens, environmentTokens);
		var semanticWeight = Math.Clamp(_options.SemanticWeight, 0, 1);
		var lexicalWeight = Math.Clamp(_options.LexicalWeight, 0, 1);

		if (semanticWeight + lexicalWeight <= 0)
		{
			semanticWeight = 0.75;
			lexicalWeight = 0.25;
		}

		return semanticScore * semanticWeight + lexicalScore * lexicalWeight;
	}

	private static double LexicalScore(
		RunbookChunkRecord chunk,
		HashSet<string> queryTokens,
		HashSet<string> serviceTokens,
		HashSet<string> environmentTokens)
	{
		var haystack = RunbookTextAnalysis.Tokenize(chunk.SearchText);
		var score = 0d;

		score += CountMatches(haystack, queryTokens) * 0.08;
		score += CountMatches(haystack, serviceTokens) * 0.12;
		score += CountMatches(haystack, environmentTokens) * 0.04;
		score += chunk.Tags.Any(tag => serviceTokens.Contains(tag)) ? 0.2 : 0;
		score += chunk.Tags.Any(tag => queryTokens.Contains(tag)) ? 0.15 : 0;
		score += SectionPriorityBoost(chunk.SectionPath);

		return Math.Min(score, 1);
	}

	private static double SectionPriorityBoost(string sectionPath)
	{
		if (string.IsNullOrWhiteSpace(sectionPath))
		{
			return 0;
		}

		if (ContainsAny(sectionPath, "Trigger", "Purpose", "Initial Verification", "Diagnosis", "Decision", "Mitigation"))
		{
			return 0.18;
		}

		if (ContainsAny(sectionPath, "Post-Incident", "Related Links", "Communication", "Resolution"))
		{
			return -0.3;
		}

		if (ContainsAny(sectionPath, "Metadata", "Required Access"))
		{
			return -0.05;
		}

		return 0;
	}

	private static bool ContainsAny(string value, params string[] terms)
	{
		return terms.Any(term => value.Contains(term, StringComparison.OrdinalIgnoreCase));
	}

	private static IReadOnlyList<ScoredRunbookChunk> DiversifyBySection(IReadOnlyList<ScoredRunbookChunk> matches)
	{
		return matches
			.GroupBy(match => $"{match.Chunk.DocumentId}:{match.Chunk.SectionPath}", StringComparer.OrdinalIgnoreCase)
			.Select(group => group
				.OrderByDescending(match => match.Score)
				.ThenBy(match => match.Chunk.Ordinal)
				.First())
			.OrderByDescending(match => match.Score)
			.ThenBy(match => match.Chunk.DocumentTitle, StringComparer.OrdinalIgnoreCase)
			.ThenBy(match => match.Chunk.Ordinal)
			.ToArray();
	}

	private static int CountMatches(HashSet<string> haystack, HashSet<string> needles)
	{
		var matches = 0;
		foreach (var needle in needles)
		{
			if (haystack.Contains(needle))
			{
				matches++;
			}
		}

		return matches;
	}

	private static bool HasLexicalOverlap(
		RunbookChunkRecord chunk,
		HashSet<string> queryTokens,
		HashSet<string> serviceTokens,
		HashSet<string> environmentTokens)
	{
		var haystack = RunbookTextAnalysis.Tokenize(chunk.SearchText);
		// A single common token (for example "production", "metrics", or "api") is not
		// enough to bypass the semantic relevance threshold. Require a meaningful lexical
		// intersection so unrelated runbooks are not presented as evidence.
		return CountMatches(haystack, queryTokens) >= 4 || CountMatches(haystack, serviceTokens) >= 3;
	}

	private static RunbookVectorPoint ToVectorPoint(RunbookChunkRecord chunk)
	{
		return new RunbookVectorPoint(
			chunk.DocumentId,
			chunk.DocumentTitle,
			chunk.DocumentSummary,
			chunk.Tags,
			chunk.SourcePath,
			chunk.Ordinal,
			chunk.SectionPath,
			chunk.Text,
			chunk.SearchText,
			chunk.Embedding);
	}

	private static byte[] SerializeVector(float[] vector)
	{
		var bytes = new byte[vector.Length * sizeof(float)];
		Buffer.BlockCopy(vector, 0, bytes, 0, bytes.Length);
		return bytes;
	}

	private static float[] DeserializeVector(byte[] bytes)
	{
		var vector = new float[bytes.Length / sizeof(float)];
		Buffer.BlockCopy(bytes, 0, vector, 0, bytes.Length);
		return vector;
	}

	private static IReadOnlyCollection<string> DeserializeTags(string tagsJson)
	{
		return JsonSerializer.Deserialize<string[]>(tagsJson, SerializerOptions) ?? Array.Empty<string>();
	}

	private static string ComputeHash(string value)
	{
		var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
		return Convert.ToHexString(bytes);
	}

	private sealed record RunbookSource(string Path, string Content);

	private sealed record RunbookDocumentIndexState(string ContentHash, string EmbeddingProvider, string EmbeddingModel);

	private sealed record RunbookChunkRecord(
		long ChunkId,
		string DocumentId,
		string DocumentTitle,
		string DocumentSummary,
		IReadOnlyCollection<string> Tags,
		string SourcePath,
		int Ordinal,
		string SectionPath,
		string Text,
		string SearchText,
		float[] Embedding);

	private sealed record ScoredRunbookChunk(RunbookChunkRecord Chunk, double Score)
	{
		public RunbookDocument ToDocument()
		{
			var title = string.IsNullOrWhiteSpace(Chunk.SectionPath)
				? Chunk.DocumentTitle
				: $"{Chunk.DocumentTitle} - {Chunk.SectionPath}";

			return new RunbookDocument(
				$"{Chunk.DocumentId}-{Chunk.Ordinal}",
				title,
				BuildSummary(Chunk.Text),
				Chunk.SearchText,
				Chunk.Tags);
		}

		public RunbookRetrievalMatch ToDiagnosticMatch()
		{
			return new RunbookRetrievalMatch
			{
				RunbookId = $"{Chunk.DocumentId}-{Chunk.Ordinal}",
				Title = string.IsNullOrWhiteSpace(Chunk.SectionPath)
					? Chunk.DocumentTitle
					: $"{Chunk.DocumentTitle} - {Chunk.SectionPath}",
				SectionPath = Chunk.SectionPath,
				Summary = BuildSummary(Chunk.Text),
				Source = Chunk.SourcePath,
				Score = Math.Round(Score, 4),
				Tags = Chunk.Tags
			};
		}

		private static string BuildSummary(string text)
		{
			var normalized = text.Replace("\r", " ").Replace("\n", " ");
			return normalized.Length <= 240 ? normalized : normalized[..240].TrimEnd() + "...";
		}
	}

	private sealed record ScoredRetrieval(IReadOnlyList<ScoredRunbookChunk> Matches, string VectorStoreProvider, bool IsDegraded, string? DegradedReason);
}
