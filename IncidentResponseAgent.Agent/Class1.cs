using Microsoft.Extensions.DependencyInjection;

namespace IncidentResponseAgent.Agent;

public static class DependencyInjection
{
	public static IServiceCollection AddAgent(this IServiceCollection services)
	{
		return services;
	}
}
