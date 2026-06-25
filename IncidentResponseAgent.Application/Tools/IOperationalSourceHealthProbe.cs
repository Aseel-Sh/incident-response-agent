namespace IncidentResponseAgent.Application.Tools;

public interface IOperationalSourceHealthProbe
{
	Task<OperationalSourceHealth> CheckAsync(CancellationToken cancellationToken = default);
}

public sealed record OperationalSourceHealth(bool Connected, string? Error = null);
