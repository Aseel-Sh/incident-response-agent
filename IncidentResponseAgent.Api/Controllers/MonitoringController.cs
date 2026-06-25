using IncidentResponseAgent.Application.Incidents;
using Microsoft.AspNetCore.Mvc;

namespace IncidentResponseAgent.Api.Controllers;

[ApiController]
[Route("api/monitoring")]
public sealed class MonitoringController(IIncidentMonitoringCoordinator coordinator) : ControllerBase
{
	[HttpGet("state")]
	public async Task<ActionResult<IncidentMonitoringState>> GetStateAsync(CancellationToken cancellationToken) => Ok(await coordinator.GetStateAsync(cancellationToken));

	[HttpPost("pause")]
	public async Task<ActionResult<IncidentMonitoringState>> PauseAsync(CancellationToken cancellationToken) => Ok(await coordinator.PauseAsync(cancellationToken));

	[HttpPost("resume")]
	public async Task<ActionResult<IncidentMonitoringState>> ResumeAsync(CancellationToken cancellationToken) => Ok(await coordinator.ResumeAsync(cancellationToken));

	[HttpPut("interval")]
	public async Task<ActionResult<IncidentMonitoringState>> SetIntervalAsync([FromBody] MonitoringIntervalRequest request, CancellationToken cancellationToken) => Ok(await coordinator.SetPollingIntervalAsync(request.Seconds, cancellationToken));

	[HttpPost("scan")]
	public async Task<ActionResult<IncidentMonitoringState>> ScanAsync(CancellationToken cancellationToken) => Ok(await coordinator.ScanNowAsync(cancellationToken));
}

public sealed record MonitoringIntervalRequest(int Seconds);
