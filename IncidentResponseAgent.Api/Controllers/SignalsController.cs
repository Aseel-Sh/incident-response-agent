using System.Text.Json;
using IncidentResponseAgent.Api.Contracts.Signals;
using IncidentResponseAgent.Application.Tools;
using IncidentResponseAgent.Infrastructure.Tools;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace IncidentResponseAgent.Api.Controllers;

[ApiController]
[Route("api/signals")]
public sealed class SignalsController : ControllerBase
{
	private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
	{
		WriteIndented = true,
		PropertyNameCaseInsensitive = true
	};

	private static readonly SemaphoreSlim FileLock = new(1, 1);
	private readonly OperationalDataOptions _options;

	public SignalsController(IOptions<OperationalDataOptions> options)
	{
		_options = options.Value ?? new OperationalDataOptions();
	}

	[HttpPost("logs")]
	[ProducesResponseType(typeof(IngestSignalResponse), StatusCodes.Status202Accepted)]
	public async Task<ActionResult<IngestSignalResponse>> IngestLogAsync(
		[FromBody] IngestLogEntryRequest request,
		CancellationToken cancellationToken)
	{
		var project = ResolveProject(request.ProjectId);
		var path = ResolvePath(project?.LogEntriesPath ?? _options.LogEntriesPath, Path.Combine("Tools", "SampleData", "logs.json"));
		var entry = new LogSearchEntry
		{
			Timestamp = request.Timestamp ?? DateTimeOffset.UtcNow,
			Source = request.Source.Trim(),
			Level = request.Level.Trim(),
			Message = request.Message.Trim(),
			CorrelationId = string.IsNullOrWhiteSpace(request.CorrelationId) ? null : request.CorrelationId.Trim()
		};

		await FileLock.WaitAsync(cancellationToken).ConfigureAwait(false);
		try
		{
			var entries = await ReadJsonArrayAsync<LogSearchEntry>(path, cancellationToken).ConfigureAwait(false);
			entries.Add(entry);
			await WriteJsonArrayAsync(path, entries, cancellationToken).ConfigureAwait(false);
		}
		finally
		{
			FileLock.Release();
		}

		return Accepted(new IngestSignalResponse { Status = "accepted", Location = path });
	}

	[HttpPost("metrics")]
	[ProducesResponseType(typeof(IngestSignalResponse), StatusCodes.Status202Accepted)]
	public async Task<ActionResult<IngestSignalResponse>> IngestMetricAsync(
		[FromBody] IngestMetricSampleRequest request,
		CancellationToken cancellationToken)
	{
		var project = ResolveProject(request.ProjectId);
		var path = ResolvePath(project?.MetricSamplesPath ?? _options.MetricSamplesPath, Path.Combine("Tools", "SampleData", "metrics.json"));
		var metricName = request.MetricName.Trim();
		var serviceName = request.ServiceName.Trim();
		var environment = request.Environment.Trim();
		var sample = new MetricSample
		{
			Timestamp = request.Timestamp ?? DateTimeOffset.UtcNow,
			Value = request.Value
		};

		await FileLock.WaitAsync(cancellationToken).ConfigureAwait(false);
		try
		{
			var series = await ReadJsonArrayAsync<MetricSeriesDocument>(path, cancellationToken).ConfigureAwait(false);
			var existing = series.FirstOrDefault(item =>
				item.MetricName.Equals(metricName, StringComparison.OrdinalIgnoreCase) &&
				item.ServiceName.Equals(serviceName, StringComparison.OrdinalIgnoreCase) &&
				item.Environment.Equals(environment, StringComparison.OrdinalIgnoreCase));

			if (existing is null)
			{
				series.Add(new MetricSeriesDocument
				{
					MetricName = metricName,
					ServiceName = serviceName,
					Environment = environment,
					Samples = [sample]
				});
			}
			else
			{
				existing.Samples.Add(sample);
			}

			await WriteJsonArrayAsync(path, series, cancellationToken).ConfigureAwait(false);
		}
		finally
		{
			FileLock.Release();
		}

		return Accepted(new IngestSignalResponse { Status = "accepted", Location = path });
	}

	private static async Task<List<T>> ReadJsonArrayAsync<T>(string path, CancellationToken cancellationToken)
	{
		if (!System.IO.File.Exists(path))
		{
			return [];
		}

		await using var stream = System.IO.File.OpenRead(path);
		return await JsonSerializer.DeserializeAsync<List<T>>(stream, SerializerOptions, cancellationToken).ConfigureAwait(false)
			?? [];
	}

	private static async Task WriteJsonArrayAsync<T>(string path, IReadOnlyCollection<T> values, CancellationToken cancellationToken)
	{
		var directory = Path.GetDirectoryName(path);
		if (!string.IsNullOrWhiteSpace(directory))
		{
			Directory.CreateDirectory(directory);
		}

		await using var stream = System.IO.File.Create(path);
		await JsonSerializer.SerializeAsync(stream, values, SerializerOptions, cancellationToken).ConfigureAwait(false);
	}

	private static string ResolvePath(string? configuredPath, string defaultRelativePath)
	{
		if (!string.IsNullOrWhiteSpace(configuredPath))
		{
			return Path.GetFullPath(Environment.ExpandEnvironmentVariables(configuredPath));
		}

		return Path.Combine(AppContext.BaseDirectory, defaultRelativePath);
	}

	private OperationalProjectOptions? ResolveProject(string? projectId)
	{
		if (string.IsNullOrWhiteSpace(projectId) || projectId.Equals("all", StringComparison.OrdinalIgnoreCase))
		{
			return null;
		}

		return _options.Projects.FirstOrDefault(project => project.Id.Equals(projectId.Trim(), StringComparison.OrdinalIgnoreCase));
	}

	private sealed class MetricSeriesDocument
	{
		public required string MetricName { get; init; }

		public required string ServiceName { get; init; }

		public required string Environment { get; init; }

		public List<MetricSample> Samples { get; init; } = [];
	}
}
