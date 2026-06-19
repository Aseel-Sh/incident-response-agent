using IncidentResponseAgent.Agent;
using IncidentResponseAgent.Agent.Incidents;
using IncidentResponseAgent.Application.Incidents;
using IncidentResponseAgent.Application.Runbooks;
using IncidentResponseAgent.Application.Tools;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;

namespace IncidentResponseAgent.Tests;

[Collection("Environment variable tests")]
public sealed class MicrosoftAgentFrameworkIntegrationTests
{
	[Fact]
	public void AgentProjectLoadsMicrosoftAgentFrameworkAndRegistersFrameworkImplementation()
	{
		Assert.StartsWith("Microsoft.Agents.AI", typeof(AIAgent).Assembly.GetName().Name, StringComparison.Ordinal);
		Assert.Equal("Microsoft.Agents.AI", typeof(ChatClientAgent).Assembly.GetName().Name);
		var services = new ServiceCollection().AddAgent();
		var registration = Assert.Single(services, item => item.ServiceType == typeof(IModelIncidentAnalysisAgent));
		Assert.Equal(typeof(MicrosoftAgentFrameworkIncidentAnalysisAgent), registration.ImplementationType);
	}

	[Fact]
	public async Task IncidentToolsAreRealFrameworkFunctionsAndDraftToolIsCallable()
	{
		var tools = new IncidentAnalysisAgentTools(new EmptyLogs(), new EmptyMetrics(), new EmptyRunbooks()).CreateFrameworkTools();

		Assert.Equal(7, tools.Count);
		Assert.All(tools, tool => Assert.IsAssignableFrom<AIFunction>(tool));
		Assert.Contains(tools.Cast<AIFunction>(), tool => tool.Name == "SearchLogs");
		Assert.Contains(tools.Cast<AIFunction>(), tool => tool.Name == "DraftProposedKnowledgeUpdate");

		var draftTool = tools.Cast<AIFunction>().Single(tool => tool.Name == "DraftProposedKnowledgeUpdate");
		var draft = await draftTool.InvokeAsync(new AIFunctionArguments(new Dictionary<string, object?>
		{
			["title"] = "Database latency", ["severity"] = "SEV-2", ["serviceName"] = "orders-api", ["environment"] = "production",
			["evidence"] = new[] { "metric:p95" }, ["actionOutcomes"] = new[] { "worked: rollback" }, ["futureSteps"] = new[] { "verify pool saturation" }
		}));
		var draftText = Assert.IsType<JsonElement>(draft).GetString();
		Assert.NotNull(draftText);
		Assert.Contains("SEV-2", draftText);
		Assert.Contains("metric:p95", draftText);
	}

	[Fact]
	public void FactoryReadsOpenRouterEnvironmentConfigurationWithoutExposingSecrets()
	{
		var oldKey = Environment.GetEnvironmentVariable("OPENROUTER_API_KEY");
		var oldModel = Environment.GetEnvironmentVariable("OPENROUTER_MODEL");
		var oldUrl = Environment.GetEnvironmentVariable("OPENROUTER_BASE_URL");
		try
		{
			Environment.SetEnvironmentVariable("OPENROUTER_API_KEY", "test-secret-never-display");
			Environment.SetEnvironmentVariable("OPENROUTER_MODEL", "openai/gpt-4.1-mini");
			Environment.SetEnvironmentVariable("OPENROUTER_BASE_URL", "https://openrouter.ai/api/v1");
			var profile = new IncidentAnalysisAgentFactory().Create();
			Assert.Equal("OpenRouter", profile.Provider);
			Assert.Equal("openai/gpt-4.1-mini", profile.Model);
			Assert.Equal("https://openrouter.ai/api/v1", profile.Endpoint);
			Assert.Equal("test-secret-never-display", profile.ApiKey);
		}
		finally
		{
			Environment.SetEnvironmentVariable("OPENROUTER_API_KEY", oldKey);
			Environment.SetEnvironmentVariable("OPENROUTER_MODEL", oldModel);
			Environment.SetEnvironmentVariable("OPENROUTER_BASE_URL", oldUrl);
		}
	}

	private sealed class EmptyLogs : ILogSearchProvider
	{
		public Task<LogSearchResult> SearchAsync(LogSearchRequest request, CancellationToken cancellationToken = default) => Task.FromResult(new LogSearchResult());
	}
	private sealed class EmptyMetrics : IMetricsProvider
	{
		public Task<MetricsQueryResult> QueryAsync(MetricsQueryRequest request, CancellationToken cancellationToken = default) => Task.FromResult(new MetricsQueryResult { MetricName = request.MetricName });
	}
	private sealed class EmptyRunbooks : IRunbookRetrievalService
	{
		public Task<RunbookRetrievalResult> RetrieveAsync(RunbookRetrievalRequest request, CancellationToken cancellationToken = default) => Task.FromResult(new RunbookRetrievalResult());
	}
}

[CollectionDefinition("Environment variable tests", DisableParallelization = true)]
public sealed class EnvironmentVariableTestCollection;
