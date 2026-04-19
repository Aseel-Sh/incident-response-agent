using System.ClientModel;
using IncidentResponseAgent.Application.Incidents;
using IncidentResponseAgent.Domain.Incidents;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using OpenAI;
using OpenAI.Chat;

namespace IncidentResponseAgent.Agent.Incidents;

public sealed class OpenAIIncidentAnalysisAgent : IIncidentAnalysisAgent
{
	private const string DefaultAgentName = "IncidentAnalysisAgent";
	private readonly IncidentAnalysisAgentOptions _options;
	private readonly IncidentAnalysisAgentInstructions _instructions = new();
	private readonly IncidentAnalysisAgentTools _tools;
	private readonly object _agentLock = new();
	private readonly object _sessionLock = new();
	private readonly Dictionary<string, AgentSession> _sessions = new(StringComparer.Ordinal);
	private AIAgent? _agent;

	public OpenAIIncidentAnalysisAgent(IOptions<IncidentAnalysisAgentOptions> options, IncidentAnalysisAgentTools tools)
	{
		_options = options.Value ?? new IncidentAnalysisAgentOptions();
		_tools = tools;
	}

	public async Task<string> AnalyzeAsync(Incident incident, IncidentAnalysisSessionContext? sessionContext = null, CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(incident);

		var agent = GetOrCreateAgent();
		var prompt = _instructions.BuildPrompt(
			incident,
			BuildProfile(),
			sessionContext,
			Array.Empty<Domain.Runbooks.RunbookDocument>(),
			Array.Empty<string>(),
			Array.Empty<string>());

		AgentSession? session = null;
		if (sessionContext is not null)
		{
			session = await GetOrCreateSessionAsync(agent, sessionContext.SessionId, cancellationToken);
		}

		AgentResponse response = await agent.RunAsync(prompt, session, cancellationToken: cancellationToken);
		return response.Text;
	}

	private static IncidentAnalysisAgentProfile BuildProfile()
	{
		return new IncidentAnalysisAgentProfile
		{
			Name = DefaultAgentName,
			Provider = "OpenAI-compatible provider",
			Model = "configured at runtime"
		};
	}

	private AIAgent GetOrCreateAgent()
	{
		if (_agent is not null)
		{
			return _agent;
		}

		lock (_agentLock)
		{
			if (_agent is not null)
			{
				return _agent;
			}

			if (string.IsNullOrWhiteSpace(_options.Endpoint))
			{
				throw new InvalidOperationException("Agent:IncidentAnalysis:Endpoint is not configured.");
			}

			if (string.IsNullOrWhiteSpace(_options.Model))
			{
				throw new InvalidOperationException("Agent:IncidentAnalysis:Model is not configured.");
			}

			if (string.IsNullOrWhiteSpace(_options.ApiKey))
			{
				throw new InvalidOperationException("Agent:IncidentAnalysis:ApiKey is not configured.");
			}

			var client = new OpenAIClient(
				new ApiKeyCredential(_options.ApiKey),
				new OpenAIClientOptions
				{
					Endpoint = new Uri(_options.Endpoint)
				});
			var toolSet = new List<AITool>
			{
				AIFunctionFactory.Create(_tools.SearchLogsAsync),
				AIFunctionFactory.Create(_tools.QueryMetricsAsync),
				AIFunctionFactory.Create(_tools.RetrieveRunbooksAsync)
			};

			ChatClient chatClient = client.GetChatClient(_options.Model);

			_agent = chatClient.AsAIAgent(
				instructions: BuildInstructions(),
				name: _options.Name,
				tools: toolSet);

			return _agent;
		}
	}

	private static string BuildInstructions()
	{
		return """
You are an incident response assistant.
Use the available tools to gather evidence before answering.
Return a concise operational analysis with this structure only:
Summary
Evidence
Hypotheses
Recommended actions
Confidence
Notes

Do not invent facts. Prefer tool output and incident details.
""";
	}

	private async Task<AgentSession> GetOrCreateSessionAsync(AIAgent agent, string sessionId, CancellationToken cancellationToken)
	{
		lock (_sessionLock)
		{
			if (_sessions.TryGetValue(sessionId, out var existingSession))
			{
				return existingSession;
			}
		}

		var createdSession = await agent.CreateSessionAsync(cancellationToken);

		lock (_sessionLock)
		{
			_sessions[sessionId] = createdSession;
		}

		return createdSession;
	}
}