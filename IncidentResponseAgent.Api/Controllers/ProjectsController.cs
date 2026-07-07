using IncidentResponseAgent.Infrastructure.Tools;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace IncidentResponseAgent.Api.Controllers;

[ApiController]
[Route("api/projects")]
public sealed class ProjectsController : ControllerBase
{
	private readonly OperationalDataOptions _options;

	public ProjectsController(IOptions<OperationalDataOptions> options)
	{
		_options = options.Value ?? new OperationalDataOptions();
	}

	[HttpGet]
	[ProducesResponseType(typeof(IReadOnlyList<ProjectWorkspaceResponse>), StatusCodes.Status200OK)]
	public ActionResult<IReadOnlyList<ProjectWorkspaceResponse>> GetProjects()
	{
		var projects = _options.Projects.Count > 0
			? _options.Projects.Select(ToResponse).ToArray()
			: [new ProjectWorkspaceResponse(
				NormalizeProjectId(_options.ProjectId),
				string.IsNullOrWhiteSpace(_options.ProjectName) ? "Default project" : _options.ProjectName.Trim(),
				_options.LogEntriesPath ?? string.Empty,
				_options.MetricSamplesPath ?? string.Empty,
				_options.SourceHealthEndpoint ?? string.Empty,
				new ProjectThresholdResponse(
					_options.HighErrorRateThreshold,
					_options.CriticalErrorRateThreshold,
					_options.QueueDepthWarningThreshold,
					_options.LatencyWarningThresholdMs,
					_options.LatencyCriticalThresholdMs,
					_options.LogPatternCountThreshold,
					_options.DetectionWindowMinutes,
					_options.MaxDetectedIncidents))];

		return Ok(projects);
	}

	private ProjectWorkspaceResponse ToResponse(OperationalProjectOptions project) => new(
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
			project.MaxDetectedIncidents ?? _options.MaxDetectedIncidents));

	private static string NormalizeProjectId(string? projectId) =>
		string.IsNullOrWhiteSpace(projectId) ? "default" : projectId.Trim();
}

public sealed record ProjectWorkspaceResponse(
	string Id,
	string Name,
	string LogEntriesPath,
	string MetricSamplesPath,
	string SourceHealthEndpoint,
	ProjectThresholdResponse Thresholds);

public sealed record ProjectThresholdResponse(
	decimal HighErrorRateThreshold,
	decimal CriticalErrorRateThreshold,
	decimal QueueDepthWarningThreshold,
	decimal LatencyWarningThresholdMs,
	decimal LatencyCriticalThresholdMs,
	int LogPatternCountThreshold,
	int DetectionWindowMinutes,
	int MaxDetectedIncidents);
