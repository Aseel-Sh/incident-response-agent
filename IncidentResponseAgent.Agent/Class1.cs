using Microsoft.Extensions.DependencyInjection;
using IncidentResponseAgent.Agent.Incidents;
using IncidentResponseAgent.Application.Incidents;

namespace IncidentResponseAgent.Agent;

public static class DependencyInjection
{
	public static IServiceCollection AddAgent(this IServiceCollection services)
	{
		services.AddSingleton<IIncidentAnalysisAgentFactory, IncidentAnalysisAgentFactory>();
		services.AddTransient<IIncidentAnalysisAgent, PromptBasedIncidentAnalysisAgent>();
		return services;
	}
}
