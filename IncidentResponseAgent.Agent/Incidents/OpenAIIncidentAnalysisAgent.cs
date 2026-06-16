using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using IncidentResponseAgent.Application.Incidents;
using IncidentResponseAgent.Application.Runbooks;
using IncidentResponseAgent.Application.Tools;
using IncidentResponseAgent.Domain.Incidents;
using IncidentResponseAgent.Domain.Runbooks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace IncidentResponseAgent.Agent.Incidents;

public sealed class OpenAIIncidentAnalysisAgent : IIncidentAnalysisAgent
{
	private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
	{
		PropertyNameCaseInsensitive = true
	};
	private static readonly object AnalysisResponseSchema = new
	{
		type = "object",
		properties = new
		{
			summary = new { type = "string" },
			evidence = new
			{
				type = "array",
				items = new
				{
					type = "object",
					properties = new
					{
						summary = new { type = "string" },
						source = new { type = "string" },
						details = new { type = "string" }
					},
					required = new[] { "summary", "source", "details" },
					additionalProperties = false
				}
			},
			hypotheses = new
			{
				type = "array",
				items = new
				{
					type = "object",
					properties = new
					{
						description = new { type = "string" },
						inferenceStrength = new { type = "string", @enum = new[] { "Strong", "Medium", "Weak" } },
						confidence = new { type = "string", @enum = new[] { "High", "Medium", "Low" } },
						supportingEvidence = new { type = "array", items = new { type = "string" } },
						evidenceReferences = new { type = "array", items = new { type = "string" } }
					},
					required = new[] { "description", "inferenceStrength", "confidence", "supportingEvidence", "evidenceReferences" },
					additionalProperties = false
				}
			},
			recommendedActions = new
			{
				type = "array",
				items = new
				{
					type = "object",
					properties = new
					{
						description = new { type = "string" },
						priority = new { type = "string", @enum = new[] { "Critical", "High", "Medium", "Low" } },
						rationale = new { type = "string" },
						supportingSignals = new { type = "array", items = new { type = "string" } }
					},
					required = new[] { "description", "priority", "rationale", "supportingSignals" },
					additionalProperties = false
				}
			},
			confidence = new { type = "string", @enum = new[] { "High", "Medium", "Low" } },
			notes = new { type = "string" }
		},
		required = new[] { "summary", "evidence", "hypotheses", "recommendedActions", "confidence", "notes" },
		additionalProperties = false
	};

	private readonly IncidentAnalysisAgentOptions _options;
	private readonly ILogger<OpenAIIncidentAnalysisAgent> _logger;
	private readonly ILogSearchProvider _logSearchProvider;
	private readonly IMetricsProvider _metricsProvider;
	private readonly IRunbookRetrievalService _runbookRetrievalService;
	private readonly HttpClient _httpClient = new();

	public OpenAIIncidentAnalysisAgent(
		IOptions<IncidentAnalysisAgentOptions> options,
		ILogSearchProvider logSearchProvider,
		IMetricsProvider metricsProvider,
		IRunbookRetrievalService runbookRetrievalService,
		ILogger<OpenAIIncidentAnalysisAgent> logger)
	{
		_options = options.Value ?? new IncidentAnalysisAgentOptions();
		_logSearchProvider = logSearchProvider;
		_metricsProvider = metricsProvider;
		_runbookRetrievalService = runbookRetrievalService;
		_logger = logger;
	}

	public async Task<IncidentAgentExecutionResult> AnalyzeAsync(
		Incident incident,
		IncidentAnalysisSessionContext? sessionContext = null,
		IncidentAnalysisAgentContext? agentContext = null,
		CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(incident);

		var endpoint = ResolveOption(_options.Endpoint, "IRA_AGENT_ENDPOINT");
		var model = ResolveOption(_options.Model, "IRA_AGENT_MODEL");
		var apiKey = ResolveOption(_options.ApiKey, "IRA_AGENT_API_KEY", "OPENROUTER_API_KEY");

		if (string.IsNullOrWhiteSpace(endpoint))
		{
			throw new InvalidOperationException("Agent:IncidentAnalysis:Endpoint is not configured.");
		}

		if (string.IsNullOrWhiteSpace(model))
		{
			throw new InvalidOperationException("Agent:IncidentAnalysis:Model is not configured.");
		}

		if (string.IsNullOrWhiteSpace(apiKey))
		{
			throw new InvalidOperationException("Agent:IncidentAnalysis:ApiKey is not configured.");
		}

		var context = agentContext ?? await BuildAgentContextAsync(incident, cancellationToken).ConfigureAwait(false);
		var request = BuildChatCompletionRequest(incident, sessionContext, context, model);
		var requestJson = JsonSerializer.Serialize(request, SerializerOptions);
		using var httpRequest = new HttpRequestMessage(HttpMethod.Post, BuildChatCompletionsUri(endpoint))
		{
			Content = new StringContent(requestJson, Encoding.UTF8, "application/json")
		};

		httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
		httpRequest.Headers.TryAddWithoutValidation("HTTP-Referer", "http://localhost:5155");
		httpRequest.Headers.TryAddWithoutValidation("X-OpenRouter-Title", "Incident Response Agent Local");

		_logger.LogInformation(
			"Running direct OpenAI-compatible incident analysis for IncidentId={IncidentId} Model={Model} Runbooks={RunbookCount} Logs={LogCount} MetricSamples={MetricSampleCount}.",
			incident.Id,
			model,
			context.Runbooks.Runbooks.Count,
			context.Logs.Entries.Count,
			context.Metrics.Samples.Count);

		using var response = await _httpClient.SendAsync(httpRequest, cancellationToken).ConfigureAwait(false);
		var responseText = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
		if (!response.IsSuccessStatusCode)
		{
			throw new HttpRequestException(
				$"OpenAI-compatible provider returned {(int)response.StatusCode} {response.ReasonPhrase}: {responseText}",
				null,
				response.StatusCode);
		}

		var completion = JsonSerializer.Deserialize<ChatCompletionResponse>(responseText, SerializerOptions);
		var content = completion?.Choices.FirstOrDefault()?.Message.Content;
		if (string.IsNullOrWhiteSpace(content))
		{
			throw new InvalidOperationException("OpenAI-compatible provider returned an empty analysis response.");
		}

		_logger.LogInformation(
			"Direct OpenAI-compatible incident analysis completed for IncidentId={IncidentId}. RoutedModel={RoutedModel} ResponseLength={ResponseLength}.",
			incident.Id,
			completion?.Model ?? model,
			content.Length);
		return new IncidentAgentExecutionResult
		{
			AnalysisText = content,
			Provider = "openai-compatible",
			Model = completion?.Model ?? model,
			UsedFallback = false
		};
	}

	private async Task<IncidentAnalysisAgentContext> BuildAgentContextAsync(Incident incident, CancellationToken cancellationToken)
	{
		var runbooks = await _runbookRetrievalService.RetrieveAsync(new RunbookRetrievalRequest
		{
			Query = BuildRunbookQuery(incident),
			ServiceName = incident.ServiceName,
			Environment = incident.Environment,
			MaxResults = 3
		}, cancellationToken).ConfigureAwait(false);

		var logs = await _logSearchProvider.SearchAsync(new LogSearchRequest
		{
			Query = incident.Title,
			ServiceName = incident.ServiceName,
			Environment = incident.Environment,
			StartTime = incident.Timestamp?.AddHours(-1),
			EndTime = incident.Timestamp,
			MaxResults = 3
		}, cancellationToken).ConfigureAwait(false);

		var metrics = await _metricsProvider.QueryAsync(new MetricsQueryRequest
		{
			MetricName = "request_error_rate",
			ServiceName = incident.ServiceName,
			Environment = incident.Environment,
			StartTime = incident.Timestamp?.AddHours(-1),
			EndTime = incident.Timestamp
		}, cancellationToken).ConfigureAwait(false);

		return new IncidentAnalysisAgentContext
		{
			Runbooks = runbooks,
			Logs = logs,
			Metrics = metrics
		};
	}

	private object BuildChatCompletionRequest(
		Incident incident,
		IncidentAnalysisSessionContext? sessionContext,
		IncidentAnalysisAgentContext context,
		string model)
	{
		return new
		{
			model,
			messages = new object[]
			{
				new
				{
					role = "system",
					content = """
You are IncidentAnalysisAgent, an SRE incident response agent.
You receive incident details plus already-gathered runbook, log, and metric evidence.
Do not claim evidence is missing when the evidence JSON contains logs, metrics, or runbooks.
Return valid compact JSON only. Do not wrap JSON in Markdown.
Use exactly this schema:
{
  "summary": "short incident summary",
  "evidence": [{"summary":"what the evidence says","source":"incident.description | rag.runbook.<id> | tool.logs | tool.metrics","details":"specific supporting detail"}],
  "hypotheses": [{"description":"likely root cause hypothesis","inferenceStrength":"Strong | Medium | Weak","confidence":"High | Medium | Low","supportingEvidence":["short evidence note"],"evidenceReferences":["source reference"]}],
  "recommendedActions": [{"description":"specific next action","priority":"Critical | High | Medium | Low","rationale":"why this action matters","supportingSignals":["source reference"]}],
  "confidence": "High | Medium | Low",
  "notes": "short caveats"
}
"""
				},
				new
				{
					role = "user",
					content = JsonSerializer.Serialize(new
					{
						incident = new
						{
							incident.Id,
							incident.Title,
							incident.Description,
							Severity = incident.Severity.ToString(),
							incident.ServiceName,
							incident.Environment,
							incident.Timestamp,
							incident.Tags
						},
						session = sessionContext is null
							? null
							: new
							{
								sessionContext.SessionId,
								sessionContext.TurnNumber,
								sessionContext.LastIncidentSummary,
								sessionContext.LastAnalysisSummary
							},
						evidence = new
						{
							runbooks = context.Runbooks.Runbooks.Select(ToRunbookEvidence).ToArray(),
							logs = context.Logs.Entries.Select(entry => new
							{
								entry.Timestamp,
								entry.Source,
								entry.Level,
								entry.Message,
								entry.CorrelationId
							}).ToArray(),
							metrics = new
							{
								context.Metrics.MetricName,
								samples = context.Metrics.Samples.Select(sample => new
								{
									sample.Timestamp,
									sample.Value
								}).ToArray()
							},
							similarIncidents = context.SimilarIncidents.Select(incident => new
							{
								incident.IncidentId,
								incident.IncidentSummary,
								incident.ServiceName,
								incident.Environment,
								incident.ResolutionSummary,
								incident.Score,
								incident.SharedSignals,
								incident.CreatedAtUtc
							}).ToArray()
						}
					}, SerializerOptions)
				}
			},
			response_format = new
			{
				type = "json_schema",
				json_schema = new
				{
					name = "incident_analysis",
					strict = true,
					schema = AnalysisResponseSchema
				}
			},
			provider = new { require_parameters = true },
			max_tokens = Math.Clamp(_options.MaxOutputTokens, 256, 4096),
			temperature = _options.Temperature
		};
	}

	private static object ToRunbookEvidence(RunbookDocument runbook)
	{
		return new
		{
			runbook.Id,
			runbook.Title,
			runbook.Summary,
			runbook.Tags
		};
	}

	private static Uri BuildChatCompletionsUri(string endpoint)
	{
		var trimmed = endpoint.TrimEnd('/');
		return trimmed.EndsWith("/chat/completions", StringComparison.OrdinalIgnoreCase)
			? new Uri(trimmed)
			: new Uri($"{trimmed}/chat/completions");
	}

	private static string? ResolveOption(string? configuredValue, params string[] environmentVariableNames)
	{
		if (!string.IsNullOrWhiteSpace(configuredValue))
		{
			return configuredValue.Trim();
		}

		foreach (var environmentVariableName in environmentVariableNames)
		{
			var environmentValue = Environment.GetEnvironmentVariable(environmentVariableName);
			if (!string.IsNullOrWhiteSpace(environmentValue))
			{
				return environmentValue.Trim();
			}
		}

		return null;
	}

	private static string BuildRunbookQuery(Incident incident)
	{
		var parts = new List<string> { incident.Title, incident.Description };
		if (!string.IsNullOrWhiteSpace(incident.ServiceName))
		{
			parts.Add(incident.ServiceName);
		}

		if (!string.IsNullOrWhiteSpace(incident.Environment))
		{
			parts.Add(incident.Environment);
		}

		parts.AddRange(incident.Tags);
		return string.Join(' ', parts.Where(part => !string.IsNullOrWhiteSpace(part)));
	}

	private sealed record ChatCompletionResponse
	{
		public string? Model { get; init; }

		public IReadOnlyList<ChatCompletionChoice> Choices { get; init; } = Array.Empty<ChatCompletionChoice>();
	}

	private sealed record ChatCompletionChoice
	{
		public required ChatCompletionMessage Message { get; init; }
	}

	private sealed record ChatCompletionMessage
	{
		public string? Content { get; init; }
	}
}
