using System.Text.Json;
using Microsoft.Extensions.Options;

namespace IncidentResponseAgent.Infrastructure.Tools;

public interface IOperationalProjectRegistry
{
	IReadOnlyList<OperationalProjectOptions> GetProjects();
	Task<IReadOnlyList<OperationalProjectOptions>> GetProjectsAsync(CancellationToken cancellationToken = default);
	Task<OperationalProjectOptions> AddProjectAsync(OperationalProjectOptions project, CancellationToken cancellationToken = default);
	Task<bool> RemoveProjectAsync(string projectId, CancellationToken cancellationToken = default);
}

public sealed class OperationalProjectRegistry : IOperationalProjectRegistry
{
	private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };
	private readonly SemaphoreSlim _lock = new(1, 1);
	private readonly OperationalDataOptions _options;

	public OperationalProjectRegistry(IOptions<OperationalDataOptions> options)
	{
		_options = options.Value ?? new OperationalDataOptions();
	}

	public IReadOnlyList<OperationalProjectOptions> GetProjects()
	{
		var configured = ConfiguredProjects();
		var custom = ReadCustomProjects();
		return configured.Concat(custom)
			.GroupBy(project => NormalizeProjectId(project.Id), StringComparer.OrdinalIgnoreCase)
			.Select(group => group.First())
			.ToArray();
	}

	public Task<IReadOnlyList<OperationalProjectOptions>> GetProjectsAsync(CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();
		return Task.FromResult(GetProjects());
	}

	public async Task<OperationalProjectOptions> AddProjectAsync(OperationalProjectOptions project, CancellationToken cancellationToken = default)
	{
		var normalized = Normalize(project);
		await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
		try
		{
			var configured = ConfiguredProjects();
			if (configured.Any(item => NormalizeProjectId(item.Id).Equals(normalized.Id, StringComparison.OrdinalIgnoreCase)))
			{
				throw new InvalidOperationException("That project is managed by configuration and cannot be replaced from the UI.");
			}

			var custom = ReadCustomProjects().Where(item => !NormalizeProjectId(item.Id).Equals(normalized.Id, StringComparison.OrdinalIgnoreCase)).ToList();
			custom.Add(normalized);
			await WriteCustomProjectsAsync(custom, cancellationToken).ConfigureAwait(false);
			return normalized;
		}
		finally
		{
			_lock.Release();
		}
	}

	public async Task<bool> RemoveProjectAsync(string projectId, CancellationToken cancellationToken = default)
	{
		var id = NormalizeProjectId(projectId);
		await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
		try
		{
			var custom = ReadCustomProjects().ToList();
			var removed = custom.RemoveAll(item => NormalizeProjectId(item.Id).Equals(id, StringComparison.OrdinalIgnoreCase)) > 0;
			if (removed)
			{
				await WriteCustomProjectsAsync(custom, cancellationToken).ConfigureAwait(false);
			}
			return removed;
		}
		finally
		{
			_lock.Release();
		}
	}

	private IReadOnlyList<OperationalProjectOptions> ConfiguredProjects()
	{
		if (_options.Projects.Count > 0)
		{
			return _options.Projects.Select(Normalize).ToArray();
		}

		return [Normalize(new OperationalProjectOptions
		{
			Id = _options.ProjectId,
			Name = _options.ProjectName,
			LogEntriesPath = _options.LogEntriesPath,
			MetricSamplesPath = _options.MetricSamplesPath,
			SourceHealthEndpoint = _options.SourceHealthEndpoint,
			HighErrorRateThreshold = _options.HighErrorRateThreshold,
			CriticalErrorRateThreshold = _options.CriticalErrorRateThreshold,
			QueueDepthWarningThreshold = _options.QueueDepthWarningThreshold,
			LatencyWarningThresholdMs = _options.LatencyWarningThresholdMs,
			LatencyCriticalThresholdMs = _options.LatencyCriticalThresholdMs,
			HealthCheckFailureThreshold = _options.HealthCheckFailureThreshold,
			HealthCheckCriticalFailureThreshold = _options.HealthCheckCriticalFailureThreshold,
			LogPatternCountThreshold = _options.LogPatternCountThreshold,
			DetectionWindowMinutes = _options.DetectionWindowMinutes,
			MaxDetectedIncidents = _options.MaxDetectedIncidents
		})];
	}

	private IReadOnlyList<OperationalProjectOptions> ReadCustomProjects()
	{
		var path = RegistryPath;
		if (!File.Exists(path))
		{
			return Array.Empty<OperationalProjectOptions>();
		}

		try
		{
			var projects = JsonSerializer.Deserialize<OperationalProjectOptions[]>(File.ReadAllText(path), JsonOptions) ?? [];
			return projects.Select(Normalize).ToArray();
		}
		catch
		{
			return Array.Empty<OperationalProjectOptions>();
		}
	}

	private async Task WriteCustomProjectsAsync(IReadOnlyCollection<OperationalProjectOptions> projects, CancellationToken cancellationToken)
	{
		Directory.CreateDirectory(Path.GetDirectoryName(RegistryPath)!);
		await File.WriteAllTextAsync(RegistryPath, JsonSerializer.Serialize(projects, JsonOptions), cancellationToken).ConfigureAwait(false);
	}

	private OperationalProjectOptions Normalize(OperationalProjectOptions project)
	{
		var id = NormalizeProjectId(project.Id);
		return new OperationalProjectOptions
		{
			Id = id,
			Name = string.IsNullOrWhiteSpace(project.Name) ? id : project.Name.Trim(),
			LogEntriesPath = project.LogEntriesPath?.Trim(),
			MetricSamplesPath = project.MetricSamplesPath?.Trim(),
			SourceHealthEndpoint = project.SourceHealthEndpoint?.Trim(),
			HighErrorRateThreshold = project.HighErrorRateThreshold ?? _options.HighErrorRateThreshold,
			CriticalErrorRateThreshold = project.CriticalErrorRateThreshold ?? _options.CriticalErrorRateThreshold,
			QueueDepthWarningThreshold = project.QueueDepthWarningThreshold ?? _options.QueueDepthWarningThreshold,
			LatencyWarningThresholdMs = project.LatencyWarningThresholdMs ?? _options.LatencyWarningThresholdMs,
			LatencyCriticalThresholdMs = project.LatencyCriticalThresholdMs ?? _options.LatencyCriticalThresholdMs,
			HealthCheckFailureThreshold = project.HealthCheckFailureThreshold ?? _options.HealthCheckFailureThreshold,
			HealthCheckCriticalFailureThreshold = project.HealthCheckCriticalFailureThreshold ?? _options.HealthCheckCriticalFailureThreshold,
			LogPatternCountThreshold = project.LogPatternCountThreshold ?? _options.LogPatternCountThreshold,
			DetectionWindowMinutes = project.DetectionWindowMinutes ?? _options.DetectionWindowMinutes,
			MaxDetectedIncidents = project.MaxDetectedIncidents ?? _options.MaxDetectedIncidents
		};
	}

	private static string NormalizeProjectId(string? projectId) =>
		string.IsNullOrWhiteSpace(projectId) ? "default" : projectId.Trim().ToLowerInvariant();

	private string RegistryPath => string.IsNullOrWhiteSpace(_options.ProjectRegistryPath)
		? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "IncidentResponseAgent", "projects.json")
		: Path.GetFullPath(Environment.ExpandEnvironmentVariables(_options.ProjectRegistryPath));
}
