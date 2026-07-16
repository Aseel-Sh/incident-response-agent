using Microsoft.Extensions.DependencyInjection;
using IncidentResponseAgent.Application.Incidents;
using IncidentResponseAgent.Application.Tools;
using IncidentResponseAgent.Application.Runbooks;
using IncidentResponseAgent.Infrastructure.Tools;
using IncidentResponseAgent.Infrastructure.Runbooks;
using IncidentResponseAgent.Infrastructure.Incidents;
using IncidentResponseAgent.Application.Services;
using IncidentResponseAgent.Infrastructure.Services;

namespace IncidentResponseAgent.Infrastructure;

public static class DependencyInjection
{
	public static IServiceCollection AddInfrastructure(this IServiceCollection services)
	{
		services.AddSingleton<ILogSearchProvider, LocalJsonLogSearchProvider>();
		services.AddSingleton<LocalJsonMetricsProvider>();
		services.AddSingleton<IOperationalProjectRegistry, OperationalProjectRegistry>();
		services.AddSingleton<IMetricsProvider>(provider => provider.GetRequiredService<LocalJsonMetricsProvider>());
		services.AddSingleton<IMetricSeriesCatalog>(provider => provider.GetRequiredService<LocalJsonMetricsProvider>());
		services.AddSingleton<IOperationalSourceHealthProbe, HttpOperationalSourceHealthProbe>();
		services.AddSingleton<SemanticRunbookRetrievalService>();
		services.AddSingleton<IRunbookRetrievalService>(provider => provider.GetRequiredService<SemanticRunbookRetrievalService>());
		services.AddSingleton<IRunbookSourceManagementService>(provider => provider.GetRequiredService<SemanticRunbookRetrievalService>());
		services.AddSingleton<IApprovedKnowledgePublisher, MarkdownApprovedKnowledgePublisher>();
		services.AddSingleton<IRunbookRetrievalDiagnosticsService>(provider => provider.GetRequiredService<SemanticRunbookRetrievalService>());
		services.AddSingleton<IIncidentSignalMonitor, LocalOperationalSignalMonitor>();
		services.AddSingleton<IIncidentAnalysisSessionStore, SqliteIncidentAnalysisSessionStore>();
		services.AddSingleton<IIncidentRecordStore, FileIncidentRecordStore>();
		services.AddSingleton<IServiceCatalog, ConfiguredServiceCatalog>();
		return services;
	}
}
