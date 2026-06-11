using IncidentResponseAgent.Agent.Incidents;
using IncidentResponseAgent.Application.Incidents;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace IncidentResponseAgent.Agent;

public static class DependencyInjection
{
	public static IServiceCollection AddAgent(this IServiceCollection services)
	{
		services.AddTransient<IncidentAnalysisAgentTools>();
		services.AddTransient<IIncidentAnalysisAgentFactory, IncidentAnalysisAgentFactory>();
		services.AddTransient<OpenAIIncidentAnalysisAgent>();
		services.AddTransient<PromptBasedIncidentAnalysisAgent>();
		services.AddTransient<IIncidentAnalysisAgent, ResilientIncidentAnalysisAgent>();

		return services;
	}
}
