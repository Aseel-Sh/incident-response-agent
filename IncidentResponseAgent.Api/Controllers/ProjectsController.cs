using IncidentResponseAgent.Infrastructure.Tools;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace IncidentResponseAgent.Api.Controllers;

[ApiController]
[Route("api/projects")]
public sealed class ProjectsController : ControllerBase
{
	private readonly OperationalDataOptions _options;
	private readonly IOperationalProjectRegistry _registry;

	public ProjectsController(IOptions<OperationalDataOptions> options, IOperationalProjectRegistry registry)
	{
		_options = options.Value ?? new OperationalDataOptions();
		_registry = registry;
	}

	[HttpGet]
	[ProducesResponseType(typeof(IReadOnlyList<ProjectWorkspaceResponse>), StatusCodes.Status200OK)]
	public ActionResult<IReadOnlyList<ProjectWorkspaceResponse>> GetProjects()
	{
		var configuredIds = _options.Projects.Count > 0
			? _options.Projects.Select(item => NormalizeProjectId(item.Id)).ToHashSet(StringComparer.OrdinalIgnoreCase)
			: new[] { NormalizeProjectId(_options.ProjectId) }.ToHashSet(StringComparer.OrdinalIgnoreCase);
		var projects = _registry.GetProjects().Select(project => ToResponse(project, !configuredIds.Contains(NormalizeProjectId(project.Id)))).ToArray();

		return Ok(projects);
	}

	[HttpPost]
	[ProducesResponseType(typeof(ProjectWorkspaceResponse), StatusCodes.Status201Created)]
	public async Task<ActionResult<ProjectWorkspaceResponse>> AddProjectAsync([FromBody] ProjectWorkspaceInput input, CancellationToken cancellationToken)
	{
		var project = await _registry.AddProjectAsync(new OperationalProjectOptions
		{
			Id = input.Id,
			Name = input.Name,
			LogEntriesPath = input.LogEntriesPath,
			MetricSamplesPath = input.MetricSamplesPath,
			SourceHealthEndpoint = input.SourceHealthEndpoint,
			HighErrorRateThreshold = input.HighErrorRateThreshold,
			CriticalErrorRateThreshold = input.CriticalErrorRateThreshold,
			QueueDepthWarningThreshold = input.QueueDepthWarningThreshold,
			LatencyWarningThresholdMs = input.LatencyWarningThresholdMs,
			LatencyCriticalThresholdMs = input.LatencyCriticalThresholdMs,
			LogPatternCountThreshold = input.LogPatternCountThreshold,
			DetectionWindowMinutes = input.DetectionWindowMinutes,
			MaxDetectedIncidents = input.MaxDetectedIncidents
		}, cancellationToken);

		return Created($"/api/projects/{project.Id}", ToResponse(project, removable: true));
	}

	[HttpDelete("{projectId}")]
	public async Task<IActionResult> RemoveProjectAsync(string projectId, CancellationToken cancellationToken) =>
		await _registry.RemoveProjectAsync(projectId, cancellationToken) ? NoContent() : NotFound();

	private ProjectWorkspaceResponse ToResponse(OperationalProjectOptions project, bool removable) => new(
		NormalizeProjectId(project.Id),
		string.IsNullOrWhiteSpace(project.Name) ? NormalizeProjectId(project.Id) : project.Name.Trim(),
		project.LogEntriesPath ?? string.Empty,
		project.MetricSamplesPath ?? string.Empty,
		project.SourceHealthEndpoint ?? string.Empty,
		new ProjectThresholdResponse(
			project.HighErrorRateThreshold ?? _options.HighErrorRateThreshold,
			project.CriticalErrorRateThreshold ?? _options.CriticalErrorRateThreshold,
			project.QueueDepthWarningThreshold ?? _options.QueueDepthWarningThreshold,
			project.LatencyWarningThresholdMs ?? _options.LatencyWarningThresholdMs,
			project.LatencyCriticalThresholdMs ?? _options.LatencyCriticalThresholdMs,
			project.LogPatternCountThreshold ?? _options.LogPatternCountThreshold,
			project.DetectionWindowMinutes ?? _options.DetectionWindowMinutes,
			project.MaxDetectedIncidents ?? _options.MaxDetectedIncidents),
		removable);

	private static string NormalizeProjectId(string? projectId) =>
		string.IsNullOrWhiteSpace(projectId) ? "default" : projectId.Trim();
}

public sealed record ProjectWorkspaceResponse(
	string Id,
	string Name,
	string LogEntriesPath,
	string MetricSamplesPath,
	string SourceHealthEndpoint,
	ProjectThresholdResponse Thresholds,
	bool Removable = false);

public sealed record ProjectWorkspaceInput
{
	public required string Id { get; init; }
	public required string Name { get; init; }
	public string? LogEntriesPath { get; init; }
	public string? MetricSamplesPath { get; init; }
	public string? SourceHealthEndpoint { get; init; }
	public decimal? HighErrorRateThreshold { get; init; }
	public decimal? CriticalErrorRateThreshold { get; init; }
	public decimal? QueueDepthWarningThreshold { get; init; }
	public decimal? LatencyWarningThresholdMs { get; init; }
	public decimal? LatencyCriticalThresholdMs { get; init; }
	public int? LogPatternCountThreshold { get; init; }
	public int? DetectionWindowMinutes { get; init; }
	public int? MaxDetectedIncidents { get; init; }
}

public sealed record ProjectThresholdResponse(
	decimal HighErrorRateThreshold,
	decimal CriticalErrorRateThreshold,
	decimal QueueDepthWarningThreshold,
	decimal LatencyWarningThresholdMs,
	decimal LatencyCriticalThresholdMs,
	int LogPatternCountThreshold,
	int DetectionWindowMinutes,
	int MaxDetectedIncidents);
