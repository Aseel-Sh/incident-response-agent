namespace IncidentResponseAgent.Application.Services;

public sealed record ServiceCatalogEntry
{
	public required string ServiceName { get; init; }
	public required string OwningTeam { get; init; }
	public string? OnCallTarget { get; init; }
	public string? EscalationPolicy { get; init; }
	public string? RunbookUrl { get; init; }
	public IReadOnlyList<string> Dependencies { get; init; } = Array.Empty<string>();
}

public interface IServiceCatalog
{
	IReadOnlyList<ServiceCatalogEntry> GetServices();
	ServiceCatalogEntry? Find(string? serviceName);
}
