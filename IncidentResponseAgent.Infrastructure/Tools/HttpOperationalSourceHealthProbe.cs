using IncidentResponseAgent.Application.Tools;
using Microsoft.Extensions.Options;

namespace IncidentResponseAgent.Infrastructure.Tools;

public sealed class HttpOperationalSourceHealthProbe(IOptions<OperationalDataOptions> options, IHttpClientFactory httpClientFactory) : IOperationalSourceHealthProbe
{
	private readonly OperationalDataOptions _options = options.Value ?? new OperationalDataOptions();

	public async Task<OperationalSourceHealth> CheckAsync(CancellationToken cancellationToken = default)
	{
		if (string.IsNullOrWhiteSpace(_options.SourceHealthEndpoint)) return new OperationalSourceHealth(true);
		try
		{
			using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
			timeout.CancelAfter(TimeSpan.FromSeconds(Math.Clamp(_options.SourceHealthTimeoutSeconds, 1, 15)));
			using var response = await httpClientFactory.CreateClient().GetAsync(_options.SourceHealthEndpoint, timeout.Token);
			return response.IsSuccessStatusCode
				? new OperationalSourceHealth(true)
				: new OperationalSourceHealth(true, $"Telemetry producer is reachable but its health endpoint reports HTTP {(int)response.StatusCode} {response.ReasonPhrase}.");
		}
		catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
		{
			return new OperationalSourceHealth(false, $"Telemetry health check failed: {exception.GetType().Name}: {exception.Message}");
		}
	}
}
