using System.ClientModel;
using IncidentResponseAgent.Application.Incidents;
using IncidentResponseAgent.Application.Runbooks;
using IncidentResponseAgent.Domain.Incidents;
using IncidentResponseAgent.Domain.Runbooks;
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
	private readonly IRunbookRetrievalService _runbookRetrievalService;
	private readonly object _agentLock = new();
	private readonly object _sessionLock = new();
	private readonly Dictionary<string, AgentSession> _sessions = new(StringComparer.Ordinal);
	private AIAgent? _agent;

	public OpenAIIncidentAnalysisAgent(
		IOptions<IncidentAnalysisAgentOptions> options,
		IncidentAnalysisAgentTools tools,
		IRunbookRetrievalService runbookRetrievalService)
	{
		_options = options.Value ?? new IncidentAnalysisAgentOptions();
		_tools = tools;
		_runbookRetrievalService = runbookRetrievalService;
	}

	public async Task<string> AnalyzeAsync(Incident incident, IncidentAnalysisSessionContext? sessionContext = null, CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(incident);

		var agent = GetOrCreateAgent();
		var runbookResult = await _runbookRetrievalService.RetrieveAsync(
			new RunbookRetrievalRequest
			{
				Query = BuildRunbookQuery(incident),
				ServiceName = incident.ServiceName,
				Environment = incident.Environment,
				MaxResults = 3
			},
			cancellationToken);

		var prompt = _instructions.BuildPrompt(
			incident,
			BuildProfile(),
			sessionContext,
			runbookResult.Runbooks,
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
Return valid JSON only. Do not wrap the JSON in Markdown.
Use this exact shape:
{
  "summary": "short incident summary",
  "evidence": [
    {
      "summary": "what the evidence says",
      "source": "incident.description | rag.runbook.<id> | tool.logs | tool.metrics",
      "details": "specific supporting detail"
    }
  ],
  "hypotheses": [
    {
      "description": "likely root cause hypothesis",
      "inferenceStrength": "Strong | Medium | Weak",
      "confidence": "High | Medium | Low",
      "supportingEvidence": ["short evidence note"],
      "evidenceReferences": ["source reference"]
    }
  ],
  "recommendedActions": [
    {
      "description": "specific next action",
      "priority": "Critical | High | Medium | Low",
      "rationale": "why this action matters",
      "supportingSignals": ["source reference"]
    }
  ],
  "confidence": "High | Medium | Low",
  "notes": "short caveats"
}

Do not invent facts. Prefer tool output and incident details.
""";
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
