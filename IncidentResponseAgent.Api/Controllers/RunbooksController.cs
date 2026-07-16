using IncidentResponseAgent.Application.Runbooks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;

namespace IncidentResponseAgent.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/runbooks")]
public sealed class RunbooksController : ControllerBase
{
	private readonly IRunbookRetrievalDiagnosticsService _diagnosticsService;
	private readonly IRunbookSourceManagementService _sourceManagementService;

	public RunbooksController(IRunbookRetrievalDiagnosticsService diagnosticsService, IRunbookSourceManagementService sourceManagementService)
	{
		_diagnosticsService = diagnosticsService;
		_sourceManagementService = sourceManagementService;
	}

	[HttpGet("sources")]
	public async Task<ActionResult<IReadOnlyList<RunbookSourceStatus>>> GetSourcesAsync(CancellationToken cancellationToken) =>
		Ok(await _sourceManagementService.GetSourcesAsync(cancellationToken));

	[HttpPost("sources")]
	[Authorize(Roles = "admin")]
	public async Task<ActionResult<RunbookSourceStatus>> AddSourceAsync([FromBody] RunbookSourceInput input, CancellationToken cancellationToken)
	{
		var source = await _sourceManagementService.AddSourceAsync(input, cancellationToken);
		return Created($"/api/runbooks/sources/{source.Id}", source);
	}

	[HttpPut("sources/{sourceId}/enabled")]
	[Authorize(Roles = "admin")]
	public async Task<ActionResult<RunbookSourceStatus>> SetSourceEnabledAsync(string sourceId, [FromBody] RunbookSourceEnabledRequest request, CancellationToken cancellationToken) =>
		Ok(await _sourceManagementService.SetEnabledAsync(sourceId, request.Enabled, cancellationToken));

	[HttpPost("sources/{sourceId}/synchronize")]
	[Authorize(Roles = "admin")]
	public async Task<ActionResult<RunbookSourceStatus>> SynchronizeSourceAsync(string sourceId, CancellationToken cancellationToken) =>
		Ok(await _sourceManagementService.SynchronizeAsync(sourceId, cancellationToken));

	[HttpDelete("sources/{sourceId}")]
	[Authorize(Roles = "admin")]
	public async Task<IActionResult> RemoveSourceAsync(string sourceId, CancellationToken cancellationToken) =>
		await _sourceManagementService.RemoveSourceAsync(sourceId, cancellationToken) ? NoContent() : NotFound();

	[HttpGet("search")]
	[ProducesResponseType(typeof(RunbookRetrievalDiagnosticsResult), StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status400BadRequest)]
	public async Task<ActionResult<RunbookRetrievalDiagnosticsResult>> SearchAsync(
		[FromQuery] string query,
		[FromQuery] string? serviceName = null,
		[FromQuery] string? environment = null,
		[FromQuery] int maxResults = 5,
		CancellationToken cancellationToken = default)
	{
		if (string.IsNullOrWhiteSpace(query))
		{
			return BadRequest("Query is required.");
		}

		var result = await _diagnosticsService.SearchAsync(
			new RunbookRetrievalDiagnosticsRequest
			{
				Query = query,
				ServiceName = serviceName,
				Environment = environment,
				MaxResults = maxResults
			},
			cancellationToken);

		return Ok(result);
	}
}

public sealed record RunbookSourceEnabledRequest(bool Enabled);
