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

	public async Task<string> AnalyzeAsync(
		Incident incident,
		IncidentAnalysisSessionContext? sessionContext = null,
		IncidentAnalysisAgentContext? agentContext = null,
		CancellationToken cancellationToken = default)
	{
		if (!HasConfiguredApiKey())
		{
			return await _fallbackAgent.AnalyzeAsync(incident, sessionContext, agentContext, cancellationToken).ConfigureAwait(false);
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
			return await _fallbackAgent.AnalyzeAsync(incident, sessionContext, agentContext, cancellationToken).ConfigureAwait(false);
		}
		catch (Exception exception) when (IsProviderFailure(exception))
		{
			_logger.LogWarning(exception, "OpenAI-compatible incident analysis failed. Falling back to local analysis.");
			return await _fallbackAgent.AnalyzeAsync(incident, sessionContext, agentContext, cancellationToken).ConfigureAwait(false);
		}
	}

	private bool HasConfiguredApiKey()
	{
		if (!string.IsNullOrWhiteSpace(_options.ApiKey))
		{
			return true;
		}

		return !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("IRA_AGENT_API_KEY"));
	}

	private static bool IsProviderFailure(Exception exception)
	{
		return exception is HttpRequestException or TimeoutException or InvalidOperationException
			|| exception.GetType().Name.Contains("ClientResultException", StringComparison.OrdinalIgnoreCase);
	}
}
