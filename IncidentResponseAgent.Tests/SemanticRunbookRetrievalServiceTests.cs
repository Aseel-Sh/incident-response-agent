using IncidentResponseAgent.Application.Runbooks;
using IncidentResponseAgent.Infrastructure.Runbooks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace IncidentResponseAgent.Tests;

public sealed class SemanticRunbookRetrievalServiceTests : IDisposable
{
	private readonly string _rootPath = Path.Combine(Path.GetTempPath(), "ira-rag-tests", Guid.NewGuid().ToString("N"));

	[Fact]
	public async Task RetrieveAsyncIndexesMarkdownIntoSqliteAndReturnsRelevantChunks()
	{
		var knowledgeBasePath = Path.Combine(_rootPath, "knowledge-base");
		Directory.CreateDirectory(knowledgeBasePath);
		await File.WriteAllTextAsync(
			Path.Combine(knowledgeBasePath, "checkout-5xx.md"),
			"""
# Checkout 5xx Triage

- **Service/System:** checkout-api
- **Incident Type:** HTTP 5xx
- **Severity Range:** High

## Purpose

Use this runbook when checkout starts returning HTTP 500 errors.

## Mitigation

Check recent deployments, review downstream payment dependencies, and confirm blast radius.
""");

		var databasePath = Path.Combine(_rootPath, "rag.sqlite");
		var service = new SemanticRunbookRetrievalService(
			Options.Create(new RunbookRetrievalOptions
			{
				DatabasePath = databasePath,
				KnowledgeBasePath = knowledgeBasePath,
				MinimumRelevanceScore = 0.05
			}),
			new EmptyHttpClientFactory(),
			NullLoggerFactory.Instance,
			NullLogger<SemanticRunbookRetrievalService>.Instance);

		var result = await service.RetrieveAsync(new RunbookRetrievalRequest
		{
			Query = "checkout http 500 errors",
			ServiceName = "checkout-api",
			Environment = "production",
			MaxResults = 3
		});

		Assert.NotEmpty(result.Runbooks);
		Assert.Contains(result.Runbooks, runbook => runbook.Title.Contains("Checkout 5xx", StringComparison.OrdinalIgnoreCase));
		Assert.True(File.Exists(databasePath));

		var diagnostics = await service.SearchAsync(new RunbookRetrievalDiagnosticsRequest
		{
			Query = "checkout http 500 errors",
			ServiceName = "checkout-api",
			Environment = "production",
			MaxResults = 3
		});

		Assert.Equal("local", diagnostics.EmbeddingProvider);
		Assert.Equal(databasePath, diagnostics.DatabasePath);
		Assert.NotEmpty(diagnostics.Matches);
		Assert.All(diagnostics.Matches, match => Assert.True(match.Score > 0));
	}

	[Fact]
	public void MarkdownRunbookChunkerDoesNotDuplicateDocumentTitleInSectionPath()
	{
		var document = new IncidentResponseAgent.Domain.Runbooks.RunbookDocument(
			"checkout",
			"Checkout Triage",
			"Checkout failures",
			"""
# Checkout Triage

## Diagnosis Steps

1. Check service health.
""",
			["checkout"]);

		var chunks = MarkdownRunbookChunker.Chunk(document);

		Assert.Contains(chunks, chunk => chunk.SectionPath == "Diagnosis Steps");
		Assert.DoesNotContain(chunks, chunk => chunk.SectionPath.Contains("Checkout Triage > Diagnosis Steps", StringComparison.OrdinalIgnoreCase));
	}

	[Fact]
	public async Task RetrieveAsyncPrunesRemovedMarkdownRunbooks()
	{
		var knowledgeBasePath = Path.Combine(_rootPath, "prune-knowledge-base");
		Directory.CreateDirectory(knowledgeBasePath);
		var checkoutPath = Path.Combine(knowledgeBasePath, "checkout-5xx.md");
		await File.WriteAllTextAsync(
			checkoutPath,
			"""
# Checkout 5xx Triage

- **Service/System:** checkout-api

## Purpose

Use this runbook when checkout starts returning HTTP 500 errors.
""");

		var databasePath = Path.Combine(_rootPath, "prune-rag.sqlite");
		var firstService = CreateService(databasePath, knowledgeBasePath);
		var firstResult = await firstService.RetrieveAsync(new RunbookRetrievalRequest
		{
			Query = "checkout http 500 errors",
			ServiceName = "checkout-api",
			Environment = "production",
			MaxResults = 3
		});
		Assert.NotEmpty(firstResult.Runbooks);

		File.Delete(checkoutPath);
		await File.WriteAllTextAsync(
			Path.Combine(knowledgeBasePath, "queue-backlog.md"),
			"""
# Queue Backlog Growth

- **Service/System:** orders-worker

## Purpose

Use this runbook when order processing queues grow faster than consumers can drain them.
""");

		var secondService = CreateService(databasePath, knowledgeBasePath);
		var secondResult = await secondService.RetrieveAsync(new RunbookRetrievalRequest
		{
			Query = "checkout http 500 errors",
			ServiceName = "checkout-api",
			Environment = "production",
			MaxResults = 3
		});

		Assert.DoesNotContain(secondResult.Runbooks, runbook => runbook.Title.Contains("Checkout 5xx", StringComparison.OrdinalIgnoreCase));
	}

	public void Dispose()
	{
		if (Directory.Exists(_rootPath))
		{
			Directory.Delete(_rootPath, recursive: true);
		}
	}

	private static SemanticRunbookRetrievalService CreateService(string databasePath, string knowledgeBasePath)
	{
		return new SemanticRunbookRetrievalService(
			Options.Create(new RunbookRetrievalOptions
			{
				DatabasePath = databasePath,
				KnowledgeBasePath = knowledgeBasePath,
				MinimumRelevanceScore = 0.05
			}),
			new EmptyHttpClientFactory(),
			NullLoggerFactory.Instance,
			NullLogger<SemanticRunbookRetrievalService>.Instance);
	}

	private sealed class EmptyHttpClientFactory : IHttpClientFactory
	{
		public HttpClient CreateClient(string name)
		{
			return new HttpClient();
		}
	}
}
