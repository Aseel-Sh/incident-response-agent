using IncidentResponseAgent.Application.Incidents;
using IncidentResponseAgent.Domain.Incidents;

namespace IncidentResponseAgent.Agent.Incidents;

public sealed class PromptBasedIncidentAnalysisAgent : IIncidentAnalysisAgent
{
	private readonly IIncidentAnalysisAgentFactory _agentFactory;
	private readonly IncidentAnalysisAgentInstructions _instructions;

	public PromptBasedIncidentAnalysisAgent(IIncidentAnalysisAgentFactory agentFactory)
	{
		_agentFactory = agentFactory;
		_instructions = new IncidentAnalysisAgentInstructions();
	}

	public Task<string> AnalyzeAsync(Incident incident, CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(incident);

		var profile = _agentFactory.Create();
		var prompt = _instructions.BuildPrompt(incident, profile);
		var response = BuildAnalysisText(incident, profile, prompt);

		return Task.FromResult(response);
	}

	private static string BuildAnalysisText(Incident incident, IncidentAnalysisAgentProfile profile, string prompt)
	{
		var serviceName = string.IsNullOrWhiteSpace(incident.ServiceName) ? "the impacted service" : incident.ServiceName;
		var promptLength = prompt.Length;

		return $"[{profile.Name}] Prompt-based analysis for {serviceName}: start with log review, confirm recent changes, and validate the incident scope. Prompt size: {promptLength} characters.";
	}
}