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

	public void Dispose()
	{
		if (Directory.Exists(_rootPath))
		{
			Directory.Delete(_rootPath, recursive: true);
		}
	}

	private sealed class EmptyHttpClientFactory : IHttpClientFactory
	{
		public HttpClient CreateClient(string name)
		{
			return new HttpClient();
		}
	}
}
