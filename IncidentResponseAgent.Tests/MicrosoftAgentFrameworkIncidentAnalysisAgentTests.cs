using System.Net;
using System.Text;
using System.Text.Json;
using IncidentResponseAgent.Agent.Incidents;
using IncidentResponseAgent.Application.Incidents;
using IncidentResponseAgent.Application.Runbooks;
using IncidentResponseAgent.Application.Tools;
using IncidentResponseAgent.Domain.Incidents;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace IncidentResponseAgent.Tests;

public sealed class MicrosoftAgentFrameworkIncidentAnalysisAgentTests
{
	[Fact]
	public async Task ProviderErrorInsideHttpSuccessIsNotReportedAsAnEmptyCompletion()
	{
		static HttpResponseMessage Error() => new(HttpStatusCode.OK) { Content = new StringContent("{\"error\":{\"code\":429,\"message\":\"private provider detail\"},\"choices\":[]}", Encoding.UTF8, "application/json") };
		var handler = new QueueHttpHandler(Error(), Error());
		var failure = await Assert.ThrowsAsync<ModelStructuredOutputException>(() => CreateAgent(handler).AnalyzeAsync(CreateIncident(), agentContext: CreateContext()));
		Assert.Contains("rate or quota limit", failure.Message);
		Assert.DoesNotContain("private provider detail", failure.Message);
		Assert.Equal(2, handler.Requests.Count);
	}
	[Fact]
	public async Task TruncatedCompletionRetriesWithMoreRoomAndTheSameStrictSchema()
	{
		var truncated = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"choices\":[{\"finish_reason\":\"length\",\"message\":{\"content\":\"{\"}}]}", Encoding.UTF8, "application/json") };
		var handler = new QueueHttpHandler(truncated, Response(ValidAnalysis("SEV-2")));
		var result = await CreateAgent(handler).AnalyzeAsync(CreateIncident(), agentContext: CreateContext());
		Assert.Contains("token limit", result.StructuredOutputRetryReason);
		Assert.Equal(2, handler.Requests.Count);
		using var first = JsonDocument.Parse(handler.Requests[0].Body);
		using var retry = JsonDocument.Parse(handler.Requests[1].Body);
		int Limit(JsonElement root) => root.TryGetProperty("max_completion_tokens", out var value) ? value.GetInt32() : root.GetProperty("max_tokens").GetInt32();
		Assert.Equal(Limit(first.RootElement) * 2, Limit(retry.RootElement));
		Assert.Equal("json_schema", retry.RootElement.GetProperty("response_format").GetProperty("type").GetString());
		Assert.True(retry.RootElement.GetProperty("response_format").GetProperty("json_schema").GetProperty("strict").GetBoolean());
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task EmptyCompletionChoicesAreRetriedOnceAndNeverLeakAdapterErrors(bool bothEmpty)
	{
		static HttpResponseMessage EmptyEnvelope() => new(HttpStatusCode.OK)
		{
			Content = new StringContent("{\"id\":\"empty\",\"object\":\"chat.completion\",\"model\":\"test/model\",\"choices\":[]}", Encoding.UTF8, "application/json")
		};
		var handler = new QueueHttpHandler(EmptyEnvelope(), bothEmpty ? EmptyEnvelope() : Response(ValidAnalysis("SEV-2")));
		if (bothEmpty)
		{
			var failure = await Assert.ThrowsAsync<ModelStructuredOutputException>(() => CreateAgent(handler).AnalyzeAsync(CreateIncident(), agentContext: CreateContext()));
			Assert.Contains("no completion choices", failure.Message);
			Assert.DoesNotContain("index", failure.Message);
		}
		else
		{
			var result = await CreateAgent(handler).AnalyzeAsync(CreateIncident(), agentContext: CreateContext());
			Assert.True(result.UsedStructuredOutputRetry);
			Assert.False(result.UsedFallback);
		}
		Assert.Equal(2, handler.Requests.Count);
		Assert.All(handler.Requests, request => Assert.Contains("response_format", request.Body));
	}

	[Fact]
	public async Task SuccessfulModelAnalysisUsesStrictChatCompletionsRequest()
	{
		var handler = new QueueHttpHandler(Response(ValidAnalysis("SEV-2")));
		var agent = CreateAgent(handler);

		var result = await agent.AnalyzeAsync(CreateIncident(), agentContext: CreateContext());

		Assert.False(result.UsedFallback);
		Assert.False(result.UsedStructuredOutputRetry);
		Assert.Equal("OpenRouter", result.Provider);
		var request = Assert.Single(handler.Requests);
		Assert.Equal("https://openrouter.ai/api/v1/chat/completions", request.Uri);
		using var json = JsonDocument.Parse(request.Body);
		Assert.Equal("test/model", json.RootElement.GetProperty("model").GetString());
		Assert.Equal("json_schema", json.RootElement.GetProperty("response_format").GetProperty("type").GetString());
		Assert.True(json.RootElement.GetProperty("response_format").GetProperty("json_schema").GetProperty("strict").GetBoolean());
		Assert.True(json.RootElement.GetProperty("provider").GetProperty("require_parameters").GetBoolean());
		Assert.True(json.RootElement.GetProperty("messages").GetArrayLength() >= 2);
		Assert.True(json.RootElement.TryGetProperty("tools", out var tools));
		Assert.Equal(7, tools.GetArrayLength());
	}

	[Fact]
	public async Task EmptyResponseRetriesWithPromptOnlyJson()
	{
		var handler = new QueueHttpHandler(Response(string.Empty), Response(ValidAnalysis("SEV-2")));

		var result = await CreateAgent(handler).AnalyzeAsync(CreateIncident(), agentContext: CreateContext());

		Assert.True(result.UsedStructuredOutputRetry);
		Assert.Contains("empty", result.StructuredOutputRetryReason, StringComparison.OrdinalIgnoreCase);
		Assert.Equal(2, handler.Requests.Count);
		Assert.Contains("response_format", handler.Requests[1].Body, StringComparison.Ordinal);
		using var retry = JsonDocument.Parse(handler.Requests[1].Body);
		var retryPrompt = retry.RootElement.GetProperty("messages").EnumerateArray().Last().GetProperty("content").GetString()!;
		Assert.Contains("previous response failed validation", retryPrompt);
		Assert.Contains("evidenceReferences", retryPrompt);
		Assert.Contains("details", retryPrompt);
	}

	[Fact]
	public async Task InvalidJsonResponseRetries()
	{
		var handler = new QueueHttpHandler(Response("not-json"), Response(ValidAnalysis("SEV-2")));

		var result = await CreateAgent(handler).AnalyzeAsync(CreateIncident(), agentContext: CreateContext());

		Assert.True(result.UsedStructuredOutputRetry);
		Assert.Contains("invalid JSON", result.StructuredOutputRetryReason, StringComparison.OrdinalIgnoreCase);
	}

	[Fact]
	public async Task SchemaInvalidResponseRetries()
	{
		var invalid = """{"summary":"missing severity","evidence":[],"hypotheses":[],"recommendedActions":[],"confidence":"Low","notes":"invalid"}""";
		var handler = new QueueHttpHandler(Response(invalid), Response(ValidAnalysis("SEV-2")));

		var result = await CreateAgent(handler).AnalyzeAsync(CreateIncident(), agentContext: CreateContext());

		Assert.True(result.UsedStructuredOutputRetry);
		Assert.Contains("severity", result.StructuredOutputRetryReason, StringComparison.OrdinalIgnoreCase);
	}

	[Fact]
	public async Task ValidButDifferentModelSeverityIsNormalizedWithoutFallback()
	{
		var handler = new QueueHttpHandler(Response(ValidAnalysis("SEV-2")));
		var incident = CreateIncident(IncidentSeverity.Sev3);

		var result = await CreateAgent(handler).AnalyzeAsync(incident, agentContext: CreateContext());

		Assert.False(result.UsedFallback);
		Assert.False(result.UsedStructuredOutputRetry);
		Assert.Contains("Model suggested SEV-2", result.ModelResponseWarning);
		Assert.Single(handler.Requests);
		using var json = JsonDocument.Parse(result.AnalysisText);
		Assert.Equal("SEV-3", json.RootElement.GetProperty("severity").GetString());
		Assert.Contains("severity changes require responder confirmation", json.RootElement.GetProperty("notes").GetString(), StringComparison.OrdinalIgnoreCase);
	}

	[Fact]
	public async Task StrictSchemaHttpRejectionRetries()
	{
		var handler = new QueueHttpHandler(
			new HttpResponseMessage(HttpStatusCode.BadRequest) { Content = new StringContent("unsupported response_format") },
			Response(ValidAnalysis("SEV-2")));

		var result = await CreateAgent(handler).AnalyzeAsync(CreateIncident(), agentContext: CreateContext());

		Assert.True(result.UsedStructuredOutputRetry);
		Assert.Contains("HTTP 400", result.StructuredOutputRetryReason);
	}

	[Theory]
	[InlineData("SEV-0")]
	[InlineData("Critical")]
	[InlineData("sev2")]
	public void StructuredValidationRejectsInvalidSeverity(string severity)
	{
		Assert.False(MicrosoftAgentFrameworkIncidentAnalysisAgent.ValidateStructuredResponse(ValidAnalysis(severity), out var reason));
		Assert.Contains("Severity", reason, StringComparison.OrdinalIgnoreCase);
	}

	[Fact]
	public void LocalAndModelSeverityFormattingUsesNumericSevLabels()
	{
		Assert.Equal("SEV-1", MicrosoftAgentFrameworkIncidentAnalysisAgent.FormatSeverity(IncidentSeverity.Sev1));
		Assert.Equal("SEV-5", MicrosoftAgentFrameworkIncidentAnalysisAgent.FormatSeverity(IncidentSeverity.Sev5));
	}

    [Theory]
    [InlineData("{\"summary\":\"observed\",\"source\":\"tool.logs\"}")]
    [InlineData("{\"summary\":\"observed\",\"source\":\"tool.logs\",\"details\":[]} ")]
    [InlineData("\"not an object\"")]
    public void RejectsMalformedEvidenceWithItsLocation(string item)
    {
        var response = ValidAnalysis("SEV-2").Replace("\"evidence\":[]", "\"evidence\":[" + item + "]");
        Assert.False(MicrosoftAgentFrameworkIncidentAnalysisAgent.ValidateStructuredResponse(response, out var reason));
        Assert.Contains("evidence[0]", reason);
    }

    [Fact]
    public async Task MalformedJsonFailsAfterExactlyTwoAttemptsWithoutLeakingParserDetails()
    {
        var handler = new QueueHttpHandler(Response("{\n{broken}"), Response("{\n{broken}"));
        var error = await Assert.ThrowsAsync<ModelStructuredOutputException>(() => CreateAgent(handler).AnalyzeAsync(CreateIncident(), agentContext: CreateContext()));
        Assert.Equal(2, handler.Requests.Count);
        Assert.All(handler.Requests, request => Assert.Contains("response_format", request.Body));
        Assert.Contains("invalid JSON", error.Message);
        Assert.DoesNotContain("BytePositionInLine", error.Message);
        Assert.DoesNotContain("JsonException", error.Message);
    }

	private static MicrosoftAgentFrameworkIncidentAnalysisAgent CreateAgent(QueueHttpHandler handler) => new(
		Options.Create(new IncidentAnalysisAgentOptions
		{
			Provider = "OpenRouter", Model = "test/model", Endpoint = "https://openrouter.ai/api/v1", ApiKey = "test-key", MaxOutputTokens = 512
		}),
		new ThrowingLogProvider(),
		new ThrowingMetricsProvider(),
		new ThrowingRunbookProvider(),
		NullLogger<MicrosoftAgentFrameworkIncidentAnalysisAgent>.Instance,
		new HttpClient(handler));

	private static Incident CreateIncident(IncidentSeverity severity = IncidentSeverity.Sev2) => new(Guid.NewGuid(), "Checkout errors", "Checkout requests are failing.", severity, "checkout-api", "test");

	private static IncidentAnalysisAgentContext CreateContext() => new()
	{
		Runbooks = new RunbookRetrievalResult(),
		Logs = new LogSearchResult(),
		Metrics = new MetricsQueryResult { MetricName = "request_error_rate" }
	};

	private static string ValidAnalysis(string severity) => $$"""
{"summary":"Checkout errors","severity":"{{severity}}","evidence":[],"hypotheses":[],"recommendedActions":[],"confidence":"Low","notes":"No unsupported claims."}
""";

	private static HttpResponseMessage Response(string content)
	{
		var envelope = JsonSerializer.Serialize(new { model = "test/model", choices = new[] { new { message = new { content } } } });
		return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(envelope, Encoding.UTF8, "application/json") };
	}

	private sealed class QueueHttpHandler(params HttpResponseMessage[] responses) : HttpMessageHandler
	{
		private readonly Queue<HttpResponseMessage> _responses = new(responses);
		public List<CapturedRequest> Requests { get; } = [];

		protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
		{
			Requests.Add(new CapturedRequest(request.RequestUri!.ToString(), await request.Content!.ReadAsStringAsync(cancellationToken)));
			return _responses.Dequeue();
		}
	}

	private sealed record CapturedRequest(string Uri, string Body);
	private sealed class ThrowingLogProvider : ILogSearchProvider { public Task<LogSearchResult> SearchAsync(LogSearchRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException(); }
	private sealed class ThrowingMetricsProvider : IMetricsProvider { public Task<MetricsQueryResult> QueryAsync(MetricsQueryRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException(); }
	private sealed class ThrowingRunbookProvider : IRunbookRetrievalService { public Task<RunbookRetrievalResult> RetrieveAsync(RunbookRetrievalRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException(); }
}
