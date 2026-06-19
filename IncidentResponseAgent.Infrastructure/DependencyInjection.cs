using Microsoft.Extensions.DependencyInjection;
using IncidentResponseAgent.Application.Incidents;
using IncidentResponseAgent.Application.Tools;
using IncidentResponseAgent.Application.Runbooks;
using IncidentResponseAgent.Infrastructure.Tools;
using IncidentResponseAgent.Infrastructure.Runbooks;
using IncidentResponseAgent.Infrastructure.Incidents;

namespace IncidentResponseAgent.Infrastructure;

public static class DependencyInjection
{
	public static IServiceCollection AddInfrastructure(this IServiceCollection services)
	{
		services.AddSingleton<ILogSearchProvider, LocalJsonLogSearchProvider>();
		services.AddSingleton<LocalJsonMetricsProvider>();
		services.AddSingleton<IMetricsProvider>(provider => provider.GetRequiredService<LocalJsonMetricsProvider>());
		services.AddSingleton<IMetricSeriesCatalog>(provider => provider.GetRequiredService<LocalJsonMetricsProvider>());
		services.AddSingleton<IRunbookRetrievalService, SemanticRunbookRetrievalService>();
		services.AddSingleton<IApprovedKnowledgePublisher, MarkdownApprovedKnowledgePublisher>();
		services.AddSingleton<IRunbookRetrievalDiagnosticsService>(serviceProvider =>
			(IRunbookRetrievalDiagnosticsService)serviceProvider.GetRequiredService<IRunbookRetrievalService>());
		services.AddSingleton<IIncidentSignalMonitor, LocalOperationalSignalMonitor>();
		services.AddSingleton<IIncidentAnalysisSessionStore, SqliteIncidentAnalysisSessionStore>();
		services.AddSingleton<IIncidentRecordStore, FileIncidentRecordStore>();
		return services;
	}
}
