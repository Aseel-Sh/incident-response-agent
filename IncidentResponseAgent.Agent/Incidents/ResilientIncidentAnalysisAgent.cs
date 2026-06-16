using IncidentResponseAgent.Application.Incidents;
using IncidentResponseAgent.Domain.Incidents;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace IncidentResponseAgent.Agent.Incidents;

public sealed class ResilientIncidentAnalysisAgent : IIncidentAnalysisAgent
{
	private readonly OpenAIIncidentAnalysisAgent _openAiAgent;
	private readonly PromptBasedIncidentAnalysisAgent _fallbackAgent;
	private readonly IncidentAnalysisAgentOptions _options;
	private readonly ILogger<ResilientIncidentAnalysisAgent> _logger;

	public ResilientIncidentAnalysisAgent(
		OpenAIIncidentAnalysisAgent openAiAgent,
		PromptBasedIncidentAnalysisAgent fallbackAgent,
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
		if (!HasConfiguredApiKey())
		{
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
			_logger.LogWarning("OpenAI-compatible incident analysis timed out after {TimeoutSeconds} seconds. Falling back to local analysis.", timeout.TotalSeconds);
			var fallbackResult = await _fallbackAgent.AnalyzeAsync(incident, sessionContext, agentContext, cancellationToken).ConfigureAwait(false);
			return fallbackResult with { FallbackReason = $"OpenAI-compatible analysis timed out after {timeout.TotalSeconds:0} seconds." };
		}
		catch (Exception exception) when (IsProviderFailure(exception))
		{
			_logger.LogWarning(exception, "OpenAI-compatible incident analysis failed. Falling back to local analysis.");
			var fallbackResult = await _fallbackAgent.AnalyzeAsync(incident, sessionContext, agentContext, cancellationToken).ConfigureAwait(false);
			return fallbackResult with { FallbackReason = $"OpenAI-compatible analysis failed: {BuildFailureReason(exception)}" };
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

		if (message.Length > 220)
		{
			message = string.Concat(message.AsSpan(0, 217), "...");
		}

		return $"{exception.GetType().Name} - {message}.";
	}
}
