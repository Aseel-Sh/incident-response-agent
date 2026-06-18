using System.Net.Http.Headers;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using IncidentResponseAgent.Application.Incidents;
using IncidentResponseAgent.Application.Runbooks;
using IncidentResponseAgent.Application.Tools;
using IncidentResponseAgent.Domain.Incidents;
using IncidentResponseAgent.Domain.Runbooks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace IncidentResponseAgent.Agent.Incidents;

public sealed class OpenAIIncidentAnalysisAgent : IModelIncidentAnalysisAgent
{
	private static readonly HttpClient SharedHttpClient = new();
	private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
	{
		PropertyNameCaseInsensitive = true,
		DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
	};
	private static readonly object AnalysisResponseSchema = new
	{
		type = "object",
		properties = new
		{
			summary = new { type = "string" },
			severity = new { type = "string", @enum = new[] { "SEV-1", "SEV-2", "SEV-3", "SEV-4", "SEV-5" } },
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
		required = new[] { "summary", "severity", "evidence", "hypotheses", "recommendedActions", "confidence", "notes" },
		additionalProperties = false
	};

	private readonly IncidentAnalysisAgentOptions _options;
	private readonly ILogger<OpenAIIncidentAnalysisAgent> _logger;
	private readonly ILogSearchProvider _logSearchProvider;
	private readonly IMetricsProvider _metricsProvider;
	private readonly IRunbookRetrievalService _runbookRetrievalService;
	private readonly HttpClient _httpClient;

	public OpenAIIncidentAnalysisAgent(
		IOptions<IncidentAnalysisAgentOptions> options,
		ILogSearchProvider logSearchProvider,
		IMetricsProvider metricsProvider,
		IRunbookRetrievalService runbookRetrievalService,
		ILogger<OpenAIIncidentAnalysisAgent> logger,
		HttpClient? httpClient = null)
	{
		_options = options.Value ?? new IncidentAnalysisAgentOptions();
		_httpClient = httpClient ?? SharedHttpClient;
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
		_logger.LogInformation(
			"Model provider selected. IncidentId={IncidentId} Provider={Provider} Model={Model} BaseUrl={BaseUrl} ApiFormat={ApiFormat}.",
			incident.Id,
			"openai-compatible",
			model,
			endpoint,
			"chat/completions");
		_logger.LogInformation(
			"Analysis request started. IncidentId={IncidentId} Runbooks={RunbookCount} Logs={LogCount} MetricSamples={MetricSampleCount}.",
			incident.Id,
			context.Runbooks.Runbooks.Count,
			context.Logs.Entries.Count,
			context.Metrics.Samples.Count);

		string? content = null;
		string? routedModel = null;
		string retryReason;
		try
		{
			(content, routedModel) = await SendCompletionAsync(
				endpoint,
				apiKey,
				BuildChatCompletionRequest(incident, sessionContext, context, model, useStrictSchema: true),
				"strict-json-schema",
				cancellationToken).ConfigureAwait(false);
			retryReason = ValidateStructuredResponse(content, out var validationFailure, FormatSeverity(incident.Severity)) ? string.Empty : validationFailure;
		}
		catch (HttpRequestException exception) when (exception.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.UnprocessableEntity)
		{
			retryReason = $"Strict JSON schema request was rejected with HTTP {(int)exception.StatusCode.Value}.";
		}

		if (!string.IsNullOrWhiteSpace(retryReason))
		{
			_logger.LogWarning("Model response validation failed. IncidentId={IncidentId} Attempt={Attempt} Reason={Reason}.", incident.Id, "strict-json-schema", retryReason);
			_logger.LogInformation("Structured output retry started. IncidentId={IncidentId} Mode={Mode} Reason={Reason}.", incident.Id, "prompt-only-json", retryReason);
			(content, routedModel) = await SendCompletionAsync(
				endpoint,
				apiKey,
				BuildChatCompletionRequest(incident, sessionContext, context, model, useStrictSchema: false),
				"prompt-only-json",
				cancellationToken).ConfigureAwait(false);

			if (!ValidateStructuredResponse(content, out var retryFailure, FormatSeverity(incident.Severity)))
			{
				_logger.LogWarning("Structured output retry failed. IncidentId={IncidentId} Reason={Reason}.", incident.Id, retryFailure);
				throw new InvalidOperationException($"Model structured response validation failed after retry: {retryFailure}");
			}

			_logger.LogInformation("Structured output retry passed. IncidentId={IncidentId}.", incident.Id);
		}
		else
		{
			_logger.LogInformation("Model response validation passed. IncidentId={IncidentId} Attempt={Attempt}.", incident.Id, "strict-json-schema");
		}

		_logger.LogInformation(
			"Direct OpenAI-compatible incident analysis completed for IncidentId={IncidentId}. RoutedModel={RoutedModel} ResponseLength={ResponseLength}.",
			incident.Id,
			routedModel ?? model,
			content!.Length);
		return new IncidentAgentExecutionResult
		{
			AnalysisText = content,
			Provider = "openai-compatible",
			Model = routedModel ?? model,
			UsedFallback = false,
			UsedStructuredOutputRetry = !string.IsNullOrWhiteSpace(retryReason),
			StructuredOutputRetryReason = string.IsNullOrWhiteSpace(retryReason) ? null : retryReason
		};
	}

	private async Task<(string? Content, string? RoutedModel)> SendCompletionAsync(
		string endpoint,
		string apiKey,
		object request,
		string attempt,
		CancellationToken cancellationToken)
	{
		var requestJson = JsonSerializer.Serialize(request, SerializerOptions);
		using var httpRequest = new HttpRequestMessage(HttpMethod.Post, BuildChatCompletionsUri(endpoint))
		{
			Content = new StringContent(requestJson, Encoding.UTF8, "application/json")
		};

		httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
		httpRequest.Headers.TryAddWithoutValidation("HTTP-Referer", "http://localhost:5155");
		httpRequest.Headers.TryAddWithoutValidation("X-OpenRouter-Title", "Incident Response Agent Local");
		_logger.LogInformation(
			"Model request sent. Provider={Provider} Uri={Uri} Attempt={Attempt} RequestBytes={RequestBytes}.",
			"openai-compatible", httpRequest.RequestUri, attempt, Encoding.UTF8.GetByteCount(requestJson));

		using var response = await _httpClient.SendAsync(httpRequest, cancellationToken).ConfigureAwait(false);
		var responseText = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
		_logger.LogInformation(
			"Model response received. Provider={Provider} Attempt={Attempt} StatusCode={StatusCode} ResponseBytes={ResponseBytes}.",
			"openai-compatible", attempt, (int)response.StatusCode, Encoding.UTF8.GetByteCount(responseText));
		if (!response.IsSuccessStatusCode)
		{
			var safeBody = responseText.Length <= 800 ? responseText : responseText[..800] + "...";
			throw new HttpRequestException(
				$"OpenAI-compatible provider returned {(int)response.StatusCode} {response.ReasonPhrase}: {safeBody}",
				null,
				response.StatusCode);
		}

		var completion = JsonSerializer.Deserialize<ChatCompletionResponse>(responseText, SerializerOptions);
		var content = completion?.Choices
			.Select(choice => choice.Message.Content)
			.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
		return (content, completion?.Model);
	}

	public static bool ValidateStructuredResponse(string? content, out string failureReason, string? expectedSeverity = null)
	{
		if (string.IsNullOrWhiteSpace(content))
		{
			failureReason = "Model returned empty content.";
			return false;
		}

		try
		{
			using var document = JsonDocument.Parse(content);
			var root = document.RootElement;
			if (root.ValueKind != JsonValueKind.Object) return Fail("Response root is not a JSON object.", out failureReason);
			var rootProperties = new[] { "summary", "severity", "evidence", "hypotheses", "recommendedActions", "confidence", "notes" };
			foreach (var property in rootProperties)
			{
				if (!root.TryGetProperty(property, out _)) return Fail($"Required property '{property}' is missing.", out failureReason);
			}
			if (!HasOnlyProperties(root, rootProperties)) return Fail("Response contains unsupported top-level properties.", out failureReason);

			var severity = root.GetProperty("severity").GetString();
			if (severity is not ("SEV-1" or "SEV-2" or "SEV-3" or "SEV-4" or "SEV-5")) return Fail($"Severity '{severity}' is invalid.", out failureReason);
			if (expectedSeverity is not null && !string.Equals(severity, expectedSeverity, StringComparison.Ordinal)) return Fail($"Model severity '{severity}' does not match incident severity '{expectedSeverity}'.", out failureReason);
			var confidence = root.GetProperty("confidence").GetString();
			if (confidence is not ("High" or "Medium" or "Low")) return Fail($"Confidence '{confidence}' is invalid.", out failureReason);
			if (root.GetProperty("summary").ValueKind != JsonValueKind.String || root.GetProperty("notes").ValueKind != JsonValueKind.String) return Fail("Summary and notes must be strings.", out failureReason);
			if (root.GetProperty("evidence").ValueKind != JsonValueKind.Array || root.GetProperty("hypotheses").ValueKind != JsonValueKind.Array || root.GetProperty("recommendedActions").ValueKind != JsonValueKind.Array) return Fail("Evidence, hypotheses, and recommendedActions must be arrays.", out failureReason);
			foreach (var item in root.GetProperty("evidence").EnumerateArray())
			{
				if (!HasExactProperties(item, "summary", "source", "details")) return Fail("An evidence item is schema-invalid.", out failureReason);
			}
			foreach (var item in root.GetProperty("hypotheses").EnumerateArray())
			{
				if (!HasExactProperties(item, "description", "inferenceStrength", "confidence", "supportingEvidence", "evidenceReferences")) return Fail("A hypothesis item is schema-invalid.", out failureReason);
				if (item.GetProperty("inferenceStrength").GetString() is not ("Strong" or "Medium" or "Weak")) return Fail("A hypothesis has invalid inferenceStrength.", out failureReason);
				if (item.GetProperty("confidence").GetString() is not ("High" or "Medium" or "Low")) return Fail("A hypothesis has invalid confidence.", out failureReason);
			}
			foreach (var item in root.GetProperty("recommendedActions").EnumerateArray())
			{
				if (!HasExactProperties(item, "description", "priority", "rationale", "supportingSignals")) return Fail("A recommended action is schema-invalid.", out failureReason);
				if (item.GetProperty("priority").GetString() is not ("Critical" or "High" or "Medium" or "Low")) return Fail("A recommended action has invalid priority.", out failureReason);
			}
			failureReason = string.Empty;
			return true;
		}
		catch (JsonException exception)
		{
			failureReason = $"Response is invalid JSON: {exception.Message}";
			return false;
		}
		catch (InvalidOperationException exception)
		{
			failureReason = $"Response has an invalid value type: {exception.Message}";
			return false;
		}
	}

	private static bool Fail(string reason, out string failureReason)
	{
		failureReason = reason;
		return false;
	}

	private static bool HasExactProperties(JsonElement item, params string[] properties) =>
		item.ValueKind == JsonValueKind.Object && properties.All(property => item.TryGetProperty(property, out _)) && HasOnlyProperties(item, properties);

	private static bool HasOnlyProperties(JsonElement item, IReadOnlyCollection<string> properties) =>
		item.EnumerateObject().All(property => properties.Contains(property.Name, StringComparer.Ordinal));

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
		string model,
		bool useStrictSchema)
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
  "severity": "SEV-1 | SEV-2 | SEV-3 | SEV-4 | SEV-5",
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
							Severity = FormatSeverity(incident.Severity),
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
			response_format = useStrictSchema
				? new
				{
					type = "json_schema",
					json_schema = new
					{
						name = "incident_analysis",
						strict = true,
						schema = AnalysisResponseSchema
					}
				}
				: null,
			provider = useStrictSchema ? new { require_parameters = true } : null,
			max_tokens = Math.Clamp(useStrictSchema ? _options.MaxOutputTokens : _options.MaxOutputTokens * 2, 256, 4096),
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

	public static string FormatSeverity(IncidentSeverity severity) => severity switch
	{
		IncidentSeverity.Sev1 => "SEV-1",
		IncidentSeverity.Sev2 => "SEV-2",
		IncidentSeverity.Sev3 => "SEV-3",
		IncidentSeverity.Sev4 => "SEV-4",
		IncidentSeverity.Sev5 => "SEV-5",
		_ => throw new ArgumentOutOfRangeException(nameof(severity), severity, "Unsupported incident severity.")
	};

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
