using Microsoft.Extensions.DependencyInjection;
using IncidentResponseAgent.Application.Tools;
using IncidentResponseAgent.Application.Runbooks;
using IncidentResponseAgent.Infrastructure.Tools;
using IncidentResponseAgent.Infrastructure.Runbooks;

namespace IncidentResponseAgent.Infrastructure;

public static class DependencyInjection
{
	public static IServiceCollection AddInfrastructure(this IServiceCollection services)
	{
		services.AddSingleton<ILogSearchProvider, FakeLogSearchProvider>();
		services.AddSingleton<IMetricsProvider, FakeMetricsProvider>();
		services.AddSingleton<IRunbookRetrievalService, InMemoryRunbookRetrievalService>();
		return services;
	}
}
