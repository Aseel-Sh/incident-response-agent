using System.Text.Json;
using IncidentResponseAgent.Application.Incidents;
using IncidentResponseAgent.Infrastructure.Tools;
using Microsoft.Extensions.Options;

namespace IncidentResponseAgent.Api.Services;

public sealed class ServerIncidentMonitoringService : BackgroundService, IIncidentMonitoringCoordinator
{
	private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };
	private readonly IIncidentSignalMonitor _monitor;
	private readonly IIncidentRecordStore _store;
	private readonly ILogger<ServerIncidentMonitoringService> _logger;
	private readonly MonitoringRuntimeOptions _options;
	private readonly OperationalDataOptions _operationalOptions;
	private readonly SemaphoreSlim _scanLock = new(1, 1);
	private bool _enabled;
	private bool _scanInProgress;
	private int _intervalSeconds;
	private string? _lastError;

	public ServerIncidentMonitoringService(IIncidentSignalMonitor monitor, IIncidentRecordStore store, IOptions<MonitoringRuntimeOptions> options, IOptions<OperationalDataOptions> operationalOptions, ILogger<ServerIncidentMonitoringService> logger)
	{
		_monitor = monitor;
		_store = store;
		_options = options.Value ?? new MonitoringRuntimeOptions();
		_operationalOptions = operationalOptions.Value ?? new OperationalDataOptions();
		_logger = logger;
		var persisted = LoadState();
		_enabled = persisted?.Enabled ?? _options.Enabled;
		_intervalSeconds = Math.Clamp(persisted?.PollingIntervalSeconds ?? _options.PollingIntervalSeconds, 5, 3600);
	}

	public async Task<IncidentMonitoringState> GetStateAsync(string? projectId = null, CancellationToken cancellationToken = default) =>
		new() { Enabled = _enabled, PollingIntervalSeconds = IntervalSeconds, ScanInProgress = _scanInProgress, LastScan = await _store.GetLastScanAsync(projectId, cancellationToken), LastError = _lastError };

	public async Task<IncidentMonitoringState> PauseAsync(CancellationToken cancellationToken = default)
	{
		_enabled = false;
		await SaveEnabledStateAsync(cancellationToken);
		return await GetStateAsync(cancellationToken: cancellationToken);
	}

	public async Task<IncidentMonitoringState> ResumeAsync(CancellationToken cancellationToken = default)
	{
		_enabled = true;
		await SaveEnabledStateAsync(cancellationToken);
		return await GetStateAsync(cancellationToken: cancellationToken);
	}

	public async Task<IncidentMonitoringState> SetPollingIntervalAsync(int seconds, CancellationToken cancellationToken = default)
	{
		_intervalSeconds = Math.Clamp(seconds, 5, 3600);
		await SaveEnabledStateAsync(cancellationToken);
		return await GetStateAsync(cancellationToken: cancellationToken);
	}

	public async Task<IncidentMonitoringState> ScanNowAsync(string? projectId = null, CancellationToken cancellationToken = default)
	{
		if (!await _scanLock.WaitAsync(0, cancellationToken)) return await GetStateAsync(projectId, cancellationToken);
		var started = DateTimeOffset.UtcNow;
		var scopedProjectId = ProjectId(projectId);
		var scopedScan = !string.IsNullOrWhiteSpace(projectId) && !projectId.Equals("all", StringComparison.OrdinalIgnoreCase);
		_scanInProgress = true;
		try
		{
			var candidates = await _monitor.DetectAsync(cancellationToken);
			var completed = DateTimeOffset.UtcNow;
			var projectIds = scopedScan ? [scopedProjectId] : ProjectIds(candidates);
			foreach (var id in projectIds)
			{
				var projectCandidates = candidates.Where(candidate => string.Equals(ProjectId(candidate.ProjectId), id, StringComparison.OrdinalIgnoreCase)).ToArray();
				var scan = new MonitoringScanRecord { ProjectId = id, StartedAtUtc = started, CompletedAtUtc = completed, CandidateCount = projectCandidates.Length, ScannedSourceCount = 2, ErrorCount = CountUnavailableFileSources(id), DurationMilliseconds = (completed - started).TotalMilliseconds, Status = "completed" };
				await _store.SaveCandidatesAsync(projectCandidates, scan, cancellationToken);
			}
			_lastError = null;
		}
		catch (Exception exception) when (exception is not OperationCanceledException)
		{
			_lastError = exception.Message;
			var completed = DateTimeOffset.UtcNow;
			try
			{
				var projectIds = scopedScan ? [scopedProjectId] : ConfiguredProjectIds();
				foreach (var id in projectIds)
				{
					await _store.SaveCandidatesAsync([], new MonitoringScanRecord { ProjectId = id, StartedAtUtc = started, CompletedAtUtc = completed, ScannedSourceCount = 2, ErrorCount = 1, DurationMilliseconds = (completed - started).TotalMilliseconds, Status = "failed" }, cancellationToken);
				}
			}
			catch (Exception persistenceException) when (persistenceException is not OperationCanceledException) { _logger.LogError(persistenceException, "Could not persist the failed monitoring scan."); }
			_logger.LogError(exception, "Server monitoring scan failed.");
		}
		finally { _scanInProgress = false; _scanLock.Release(); }
		return await GetStateAsync(projectId, cancellationToken);
	}

	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		await Task.Delay(TimeSpan.FromSeconds(Math.Clamp(_options.StartupDelaySeconds, 0, 300)), stoppingToken);
		while (!stoppingToken.IsCancellationRequested)
		{
			if (_enabled)
			{
				try { await ScanNowAsync(cancellationToken: stoppingToken); }
				catch (Exception exception) when (exception is not OperationCanceledException) { _lastError = exception.Message; _logger.LogError(exception, "Unexpected monitoring loop failure; the schedule will continue."); }
			}
			await Task.Delay(TimeSpan.FromSeconds(IntervalSeconds), stoppingToken);
		}
	}

	private int IntervalSeconds => _intervalSeconds;
	private int CountUnavailableFileSources(string projectId)
	{
		var project = _operationalOptions.Projects.FirstOrDefault(item => string.Equals(item.Id, projectId, StringComparison.OrdinalIgnoreCase));
		var logsPath = project?.LogEntriesPath ?? _operationalOptions.LogEntriesPath;
		var metricsPath = project?.MetricSamplesPath ?? _operationalOptions.MetricSamplesPath;
		return new[]
		{
			ResolveSourcePath(logsPath, "Data", "logs.json"),
			ResolveSourcePath(metricsPath, "Data", "metrics.json")
		}.Count(path => !File.Exists(path));
	}

	private IReadOnlyList<string> ProjectIds(IReadOnlyList<DetectedIncidentCandidate> candidates)
	{
		var ids = ConfiguredProjectIds().Concat(candidates.Select(candidate => ProjectId(candidate.ProjectId))).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
		return ids.Length == 0 ? ["default"] : ids;
	}

	private IReadOnlyList<string> ConfiguredProjectIds() =>
		_operationalOptions.Projects.Count > 0
			? _operationalOptions.Projects.Select(project => ProjectId(project.Id)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray()
			: [ProjectId(_operationalOptions.ProjectId)];

	private static string ProjectId(string? projectId) => string.IsNullOrWhiteSpace(projectId) ? "default" : projectId.Trim();

	private static string ResolveSourcePath(string? configuredPath, params string[] fallbackSegments) =>
		!string.IsNullOrWhiteSpace(configuredPath)
			? Path.GetFullPath(Environment.ExpandEnvironmentVariables(configuredPath))
			: Path.Combine([AppContext.BaseDirectory, .. fallbackSegments]);

	private string StatePath => string.IsNullOrWhiteSpace(_options.StatePath)
		? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "IncidentResponseAgent", "monitoring-state.json")
		: Path.GetFullPath(Environment.ExpandEnvironmentVariables(_options.StatePath));

	private PersistedState? LoadState()
	{
		try { return File.Exists(StatePath) ? JsonSerializer.Deserialize<PersistedState>(File.ReadAllText(StatePath), JsonOptions) : null; }
		catch { return null; }
	}

	private async Task SaveEnabledStateAsync(CancellationToken cancellationToken)
	{
		Directory.CreateDirectory(Path.GetDirectoryName(StatePath)!);
		await File.WriteAllTextAsync(StatePath, JsonSerializer.Serialize(new PersistedState(_enabled, _intervalSeconds), JsonOptions), cancellationToken);
	}

	private sealed record PersistedState(bool Enabled, int PollingIntervalSeconds);
}
