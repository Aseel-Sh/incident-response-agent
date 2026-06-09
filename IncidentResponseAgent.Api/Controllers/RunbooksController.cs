using IncidentResponseAgent.Application.Runbooks;
using Microsoft.AspNetCore.Mvc;

namespace IncidentResponseAgent.Api.Controllers;

[ApiController]
[Route("api/runbooks")]
public sealed class RunbooksController : ControllerBase
{
	private readonly IRunbookRetrievalDiagnosticsService _diagnosticsService;

	public RunbooksController(IRunbookRetrievalDiagnosticsService diagnosticsService)
	{
		_diagnosticsService = diagnosticsService;
	}

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
