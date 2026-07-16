using IncidentResponseAgent.Application.Services;
using Microsoft.Extensions.Options;

namespace IncidentResponseAgent.Infrastructure.Services;

public sealed record ServiceCatalogOptions
{
	public IReadOnlyList<ServiceCatalogEntry> Services { get; init; } = Array.Empty<ServiceCatalogEntry>();
}

public sealed class ConfiguredServiceCatalog(IOptions<ServiceCatalogOptions> options) : IServiceCatalog
{
	private readonly IReadOnlyList<ServiceCatalogEntry> _services = options.Value.Services;
	public IReadOnlyList<ServiceCatalogEntry> GetServices() => _services;
	public ServiceCatalogEntry? Find(string? serviceName) => string.IsNullOrWhiteSpace(serviceName) ? null : _services.FirstOrDefault(item => item.ServiceName.Equals(serviceName, StringComparison.OrdinalIgnoreCase));
}
