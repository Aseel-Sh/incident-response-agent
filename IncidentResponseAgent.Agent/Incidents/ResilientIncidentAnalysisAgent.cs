using IncidentResponseAgent.Application.Incidents;
using IncidentResponseAgent.Domain.Incidents;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

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
			return fallbackResult with { FallbackReason = "Agent API key is not configured." };
		}

		var timeout = TimeSpan.FromSeconds(Math.Clamp(_options.AnalysisTimeoutSeconds, 5, 120));
		using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
		timeoutCts.CancelAfter(timeout);

		try
		{
			return await _openAiAgent.AnalyzeAsync(incident, sessionContext, agentContext, timeoutCts.Token).ConfigureAwait(false);
		}
		catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
		{
			_logger.LogWarning("OpenRouter Microsoft Agent Framework analysis timed out after {TimeoutSeconds} seconds. Falling back to local analysis.", timeout.TotalSeconds);
			_logger.LogWarning("Fallback triggered. IncidentId={IncidentId} Reason={Reason}.", incident.Id, $"Model timed out after {timeout.TotalSeconds:0} seconds.");
			var fallbackResult = await _fallbackAgent.AnalyzeAsync(incident, sessionContext, agentContext, cancellationToken).ConfigureAwait(false);
			return fallbackResult with { FallbackReason = $"OpenRouter model analysis timed out after {timeout.TotalSeconds:0} seconds." };
		}
		catch (Exception exception) when (IsProviderFailure(exception))
		{
			_logger.LogWarning(exception, "OpenRouter Microsoft Agent Framework analysis failed. Falling back to local analysis.");
			_logger.LogWarning("Fallback triggered. IncidentId={IncidentId} Reason={Reason}.", incident.Id, BuildFailureReason(exception));
			var fallbackResult = await _fallbackAgent.AnalyzeAsync(incident, sessionContext, agentContext, cancellationToken).ConfigureAwait(false);
			return fallbackResult with { FallbackReason = $"OpenRouter model analysis failed: {BuildFailureReason(exception)}" };
		}
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
