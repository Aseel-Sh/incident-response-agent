using Microsoft.Extensions.DependencyInjection;
using IncidentResponseAgent.Agent.Incidents;

namespace IncidentResponseAgent.Agent;

public static class DependencyInjection
{
	public static IServiceCollection AddAgent(this IServiceCollection services)
	{
		services.AddSingleton<IIncidentAnalysisAgentFactory, IncidentAnalysisAgentFactory>();
		return services;
	}
}
