using Microsoft.Extensions.DependencyInjection;

namespace IncidentResponseAgent.Application;

public static class DependencyInjection
{
	public static IServiceCollection AddApplication(this IServiceCollection services)
	{
		services.AddTransient<Incidents.IAnalyzeIncidentUseCase, Incidents.AnalyzeIncidentUseCase>();
		services.AddTransient<Incidents.IGetRecentIncidentAnalysesUseCase, Incidents.GetRecentIncidentAnalysesUseCase>();
		return services;
	}
}
