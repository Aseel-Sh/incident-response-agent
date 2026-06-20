using IncidentResponseAgent.Application.Incidents;
using IncidentResponseAgent.Domain.Incidents;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Diagnostics;

namespace IncidentResponseAgent.Agent.Incidents;

public sealed class ResilientIncidentAnalysisAgent : IIncidentAnalysisAgent
{
	private readonly IModelIncidentAnalysisAgent _openAiAgent;
	private readonly ILocalFallbackIncidentAnalysisAgent _fallbackAgent;
	private readonly IncidentAnalysisAgentOptions _options;
	private readonly ILogger<ResilientIncidentAnalysisAgent> _logger;

	public ResilientIncidentAnalysisAgent(
		IModelIncidentAnalysisAgent openAiAgent,
		ILocalFallbackIncidentAnalysisAgent fallbackAgent,
		IOptions<IncidentAnalysisAgentOptions> options,
		ILogger<ResilientIncidentAnalysisAgent> logger)
	{
		_openAiAgent = openAiAgent;
		_fallbackAgent = fallbackAgent;
		_options = options.Value ?? new IncidentAnalysisAgentOptions();
		_logger = logger;
	}

	public async Task<IncidentAgentExecutionResult> AnalyzeAsync(
		Incident incident,
		IncidentAnalysisSessionContext? sessionContext = null,
		IncidentAnalysisAgentContext? agentContext = null,
		CancellationToken cancellationToken = default)
	{
		_logger.LogInformation(
			"Model provider selected. IncidentId={IncidentId} Provider={Provider} Model={Model} EndpointConfigured={EndpointConfigured} ApiKeyConfigured={ApiKeyConfigured}.",
			incident.Id, _options.Provider, _options.Model, !string.IsNullOrWhiteSpace(_options.Endpoint), HasConfiguredApiKey());
		if (!HasConfiguredApiKey())
		{
			_logger.LogWarning("Fallback triggered. IncidentId={IncidentId} Reason={Reason}.", incident.Id, "Agent API key is not configured.");
			var fallbackResult = await _fallbackAgent.AnalyzeAsync(incident, sessionContext, agentContext, cancellationToken).ConfigureAwait(false);
			return WithAttempt(fallbackResult, "Agent API key is not configured.", 0, "before_model_execution");
		}

		var timeout = TimeSpan.FromSeconds(ResolveTimeoutSeconds());
		using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
		timeoutCts.CancelAfter(timeout);
		var modelStopwatch = Stopwatch.StartNew();

		try
		{
			var result = await _openAiAgent.AnalyzeAsync(incident, sessionContext, agentContext, timeoutCts.Token).ConfigureAwait(false);
			modelStopwatch.Stop();
			_logger.LogInformation("Model analysis completed. IncidentId={IncidentId} DurationMs={DurationMs} Provider={Provider} Model={Model}.", incident.Id, modelStopwatch.ElapsedMilliseconds, result.Provider, result.Model);
			return result with { ModelDurationMilliseconds = modelStopwatch.ElapsedMilliseconds };
		}
		catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
		{
			modelStopwatch.Stop();
			_logger.LogWarning("OpenRouter Microsoft Agent Framework analysis timed out after {TimeoutSeconds} seconds. Falling back to local analysis.", timeout.TotalSeconds);
			_logger.LogWarning("Fallback triggered after model execution started. IncidentId={IncidentId} DurationMs={DurationMs} TimeoutSource={TimeoutSource} Reason={Reason}.", incident.Id, modelStopwatch.ElapsedMilliseconds, "ResilientIncidentAnalysisAgent.CancelAfter", $"Model timed out after {timeout.TotalSeconds:0} seconds.");
			var fallbackResult = await _fallbackAgent.AnalyzeAsync(incident, sessionContext, agentContext, cancellationToken).ConfigureAwait(false);
			return WithAttempt(fallbackResult, $"OpenRouter model analysis timed out after {timeout.TotalSeconds:0} seconds.", modelStopwatch.ElapsedMilliseconds, "during_model_execution", $"ResilientIncidentAnalysisAgent.CancelAfter({timeout.TotalSeconds:0}s)");
		}
		catch (Exception exception) when (IsProviderFailure(exception))
		{
			modelStopwatch.Stop();
			_logger.LogWarning(exception, "OpenRouter Microsoft Agent Framework analysis failed. Falling back to local analysis.");
			var stage = exception is InvalidOperationException ? "after_model_response_validation" : "during_model_execution";
			_logger.LogWarning("Fallback triggered. IncidentId={IncidentId} DurationMs={DurationMs} Stage={Stage} Reason={Reason}.", incident.Id, modelStopwatch.ElapsedMilliseconds, stage, BuildFailureReason(exception));
			var fallbackResult = await _fallbackAgent.AnalyzeAsync(incident, sessionContext, agentContext, cancellationToken).ConfigureAwait(false);
			return WithAttempt(fallbackResult, $"OpenRouter model analysis failed: {BuildFailureReason(exception)}", modelStopwatch.ElapsedMilliseconds, stage);
		}
	}

	private IncidentAgentExecutionResult WithAttempt(IncidentAgentExecutionResult result, string reason, long durationMilliseconds, string stage, string? timeoutSource = null) => result with
	{
		FallbackReason = reason,
		ModelDurationMilliseconds = durationMilliseconds,
		FallbackStage = stage,
		TimeoutSource = timeoutSource,
		AttemptedProvider = _options.Provider,
		AttemptedModel = FirstConfigured(
			Environment.GetEnvironmentVariable("OPENROUTER_MODEL"),
			Environment.GetEnvironmentVariable("IRA_AGENT_MODEL"),
			_options.Model)
	};

	private static string? FirstConfigured(params string?[] values) => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));

	private int ResolveTimeoutSeconds()
	{
		var configured = FirstConfigured(
			Environment.GetEnvironmentVariable("OPENROUTER_TIMEOUT_SECONDS"),
			Environment.GetEnvironmentVariable("IRA_AGENT_ANALYSIS_TIMEOUT_SECONDS"));
		return int.TryParse(configured, out var seconds)
			? Math.Clamp(seconds, 5, 180)
			: Math.Clamp(_options.AnalysisTimeoutSeconds, 5, 180);
	}

	private bool HasConfiguredApiKey()
	{
		if (!string.IsNullOrWhiteSpace(_options.ApiKey))
		{
			return true;
		}

		return !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("IRA_AGENT_API_KEY"))
			|| !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("OPENROUTER_API_KEY"));
	}

	private static bool IsProviderFailure(Exception exception)
	{
		return exception is HttpRequestException or TimeoutException or InvalidOperationException
			|| exception.GetType().Name.Contains("ClientResultException", StringComparison.OrdinalIgnoreCase);
	}

	private static string BuildFailureReason(Exception exception)
	{
		var message = string.IsNullOrWhiteSpace(exception.Message)
			? exception.GetType().Name
			: exception.Message.Trim();

		if (message.Contains("empty message", StringComparison.OrdinalIgnoreCase)
		    || message.Contains("empty analysis response", StringComparison.OrdinalIgnoreCase))
		{
			return "Model returned empty output; local analysis used the gathered runbooks, logs, metrics, and incident history.";
		}

		if (message.Contains("429", StringComparison.OrdinalIgnoreCase)
		    || message.Contains("Too Many Requests", StringComparison.OrdinalIgnoreCase))
		{
			return "Model provider is rate-limited; local analysis used the gathered runbooks, logs, metrics, and incident history.";
		}

		if (message.Length > 220)
		{
			message = string.Concat(message.AsSpan(0, 217), "...");
		}

		return $"{exception.GetType().Name} - {message}.";
	}
}
