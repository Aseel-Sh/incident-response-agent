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

	[Fact]
	public async Task SameServiceReindexesApprovedKnowledgeAddedAndRemovedAfterStartup()
	{
		var knowledgeBasePath = Path.Combine(_rootPath, "dynamic-knowledge-base");
		Directory.CreateDirectory(knowledgeBasePath);
		var options = Options.Create(new RunbookRetrievalOptions
		{
			DatabasePath = Path.Combine(_rootPath, "dynamic-rag.sqlite"),
			KnowledgeBasePath = knowledgeBasePath,
			MinimumRelevanceScore = 0.05
		});
		var service = new SemanticRunbookRetrievalService(options, new EmptyHttpClientFactory(), NullLoggerFactory.Instance, NullLogger<SemanticRunbookRetrievalService>.Instance);
		var publisher = new MarkdownApprovedKnowledgePublisher(options);
		var proposalId = Guid.NewGuid();
		var incidentId = Guid.NewGuid();

		var empty = await service.RetrieveAsync(new RunbookRetrievalRequest { Query = "catalog redis cache stampede", ServiceName = "catalog-api" });
		Assert.Empty(empty.Runbooks);

		await publisher.PublishAsync(proposalId, incidentId, "Catalog cache recovery", "## Evidence\n\nCatalog Redis cache stampede saturated catalog-api.\n\n## Mitigation\n\nThrottle cache refresh and warm keys gradually.");
		var publishedPath = Path.Combine(knowledgeBasePath, $"approved-{proposalId:N}.md");
		var published = await File.ReadAllTextAsync(publishedPath);
		Assert.Contains($"incident-id: {incidentId}", published);
		Assert.Contains("approved-by: human-review", published);
		await publisher.PublishAsync(proposalId, incidentId, "Catalog cache recovery", "## Evidence\n\nValidated catalog cache evidence.\n\n## Mitigation\n\nThrottle cache refresh and warm keys gradually.");
		Assert.Single(Directory.EnumerateFiles(Path.Combine(knowledgeBasePath, ".history", $"approved-{proposalId:N}"), "*.md"));
		var indexed = await service.RetrieveAsync(new RunbookRetrievalRequest { Query = "catalog redis cache stampede", ServiceName = "catalog-api" });
		Assert.Contains(indexed.Runbooks, runbook => runbook.Title.Contains("Catalog cache recovery", StringComparison.OrdinalIgnoreCase));

		await publisher.RemoveAsync(proposalId);
		var removed = await service.RetrieveAsync(new RunbookRetrievalRequest { Query = "catalog redis cache stampede", ServiceName = "catalog-api" });
		Assert.DoesNotContain(removed.Runbooks, runbook => runbook.Title.Contains("Catalog cache recovery", StringComparison.OrdinalIgnoreCase));
	}

	[Fact]
	public async Task ConnectedDirectoryCanBeSynchronizedDisabledAndRemoved()
	{
		var primary = Path.Combine(_rootPath, "source-primary");
		var external = Path.Combine(_rootPath, "source-external");
		Directory.CreateDirectory(primary);
		Directory.CreateDirectory(external);
		await File.WriteAllTextAsync(Path.Combine(external, "payments.md"), "# Payment dependency timeout\n\n## Mitigation\n\nFail over the payment dependency and verify timeout metrics.");
		var service = new SemanticRunbookRetrievalService(
			Options.Create(new RunbookRetrievalOptions
			{
				DatabasePath = Path.Combine(_rootPath, "source-rag.sqlite"),
				KnowledgeBasePath = primary,
				SourceRegistryPath = Path.Combine(_rootPath, "runbook-sources.json"),
				MinimumRelevanceScore = 0.05
			}),
			new EmptyHttpClientFactory(), NullLoggerFactory.Instance, NullLogger<SemanticRunbookRetrievalService>.Instance);

		var connected = await service.AddSourceAsync(new RunbookSourceInput { Name = "Payments operations", Type = "directory", Path = external });
		var synchronized = await service.SynchronizeAsync(connected.Id);
		var result = await service.RetrieveAsync(new RunbookRetrievalRequest { Query = "payment dependency timeout fail over", ServiceName = "payments-api" });

		Assert.True(synchronized.Reachable);
		Assert.Equal(1, synchronized.DocumentCount);
		Assert.True(synchronized.SectionCount > 0);
		Assert.NotNull(synchronized.LastSynchronizedAtUtc);
		Assert.Contains(result.Runbooks, item => item.Title.Contains("Payment dependency", StringComparison.OrdinalIgnoreCase));

		await service.SetEnabledAsync(connected.Id, false);
		await service.SynchronizeAsync("configured-primary");
		var disabled = await service.RetrieveAsync(new RunbookRetrievalRequest { Query = "payment dependency timeout fail over", ServiceName = "payments-api" });
		Assert.DoesNotContain(disabled.Runbooks, item => item.Title.Contains("Payment dependency", StringComparison.OrdinalIgnoreCase));

		Assert.True(await service.RemoveSourceAsync(connected.Id));
		Assert.DoesNotContain(await service.GetSourcesAsync(), item => item.Id == connected.Id);
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
