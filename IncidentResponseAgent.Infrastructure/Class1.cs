using Microsoft.Extensions.DependencyInjection;
using IncidentResponseAgent.Application.Tools;
using IncidentResponseAgent.Infrastructure.Tools;

namespace IncidentResponseAgent.Infrastructure;

public static class DependencyInjection
{
	public static IServiceCollection AddInfrastructure(this IServiceCollection services)
	{
		services.AddSingleton<ILogSearchProvider, FakeLogSearchProvider>();
		services.AddSingleton<IMetricsProvider, FakeMetricsProvider>();
		return services;
	}
}
