using System.Text.Json;
using IncidentResponseAgent.Application.Tools;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace IncidentResponseAgent.Infrastructure.Tools;

public sealed class LocalJsonMetricsProvider : IMetricsProvider, IMetricSeriesCatalog
{
	private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
	{
		PropertyNameCaseInsensitive = true
	};

	private readonly ILogger<LocalJsonMetricsProvider> _logger;
	private readonly OperationalDataOptions _options;

	public LocalJsonMetricsProvider(
		IOptions<OperationalDataOptions> options,
		ILogger<LocalJsonMetricsProvider> logger)
	{
		_options = options.Value ?? new OperationalDataOptions();
		_logger = logger;
	}

	public async Task<MetricsQueryResult> QueryAsync(MetricsQueryRequest request, CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(request);
		cancellationToken.ThrowIfCancellationRequested();

		if (string.IsNullOrWhiteSpace(request.MetricName))
		{
			throw new ArgumentException("Metric name cannot be empty.", nameof(request));
		}

		var series = await LoadSeriesAsync(cancellationToken).ConfigureAwait(false);
		var serviceName = string.IsNullOrWhiteSpace(request.ServiceName) ? "platform" : request.ServiceName;
		var environment = string.IsNullOrWhiteSpace(request.Environment) ? "unspecified" : request.Environment;
		var matchedSeries = series.FirstOrDefault(item =>
			item.MetricName.Equals(request.MetricName, StringComparison.OrdinalIgnoreCase) &&
			(string.IsNullOrWhiteSpace(request.ServiceName) || item.ServiceName.Equals(request.ServiceName, StringComparison.OrdinalIgnoreCase)) &&
			(string.IsNullOrWhiteSpace(request.Environment) || EnvironmentsMatch(item.Environment, request.Environment)));

		if (matchedSeries is null)
		{
			if (!_options.UseDeterministicFallbacks)
			{
				_logger.LogInformation("No local metric series matched {MetricName} for {ServiceName}/{Environment}. Returning an empty local result.", request.MetricName, serviceName, environment);
				return new MetricsQueryResult
				{
					MetricName = $"{request.MetricName} ({serviceName}/{environment})",
					Samples = Array.Empty<MetricSample>()
				};
			}

			_logger.LogInformation("No local metric series matched {MetricName} for {ServiceName}/{Environment}. Returning deterministic fallback samples because Tools:OperationalData:UseDeterministicFallbacks is enabled.", request.MetricName, serviceName, environment);
			return await new DeterministicFallbackMetricsProvider().QueryAsync(request, cancellationToken).ConfigureAwait(false);
		}

		var samples = matchedSeries.Samples
			.Where(sample => request.StartTime is null || sample.Timestamp >= request.StartTime.Value)
			.Where(sample => request.EndTime is null || sample.Timestamp <= request.EndTime.Value)
			.OrderBy(sample => sample.Timestamp)
			.ToArray();

		if (samples.Length == 0 && (request.StartTime is not null || request.EndTime is not null))
		{
			_logger.LogInformation("Metric query {MetricName} matched a local series for {ServiceName}/{Environment}, but no samples were inside the requested time window. Returning an empty local result.", request.MetricName, serviceName, environment);
			return new MetricsQueryResult
			{
				MetricName = $"{request.MetricName} ({serviceName}/{environment})",
				Samples = Array.Empty<MetricSample>()
			};
		}

		if (samples.Length == 0)
		{
			samples = matchedSeries.Samples.OrderBy(sample => sample.Timestamp).TakeLast(5).ToArray();
		}

		_logger.LogInformation("Metric query {MetricName} returned {Count} local samples for {ServiceName}/{Environment}.", request.MetricName, samples.Length, serviceName, environment);
		return new MetricsQueryResult
		{
			MetricName = $"{request.MetricName} ({serviceName}/{environment})",
			Samples = samples
		};
	}

	public async Task<IReadOnlyList<MetricSeries>> ListSeriesAsync(CancellationToken cancellationToken = default)
	{
		return await LoadSeriesAsync(cancellationToken).ConfigureAwait(false);
	}

	private async Task<IReadOnlyList<MetricSeries>> LoadSeriesAsync(CancellationToken cancellationToken)
	{
		var path = ResolvePath();
		if (!File.Exists(path))
		{
			_logger.LogWarning("Local metric sample file {Path} was not found.", path);
			return Array.Empty<MetricSeries>();
		}

		await using var stream = File.OpenRead(path);
		return await JsonSerializer.DeserializeAsync<MetricSeries[]>(stream, SerializerOptions, cancellationToken).ConfigureAwait(false)
			?? Array.Empty<MetricSeries>();
	}

	private string ResolvePath()
	{
		if (!string.IsNullOrWhiteSpace(_options.MetricSamplesPath))
		{
			return Path.GetFullPath(Environment.ExpandEnvironmentVariables(_options.MetricSamplesPath));
		}

		return Path.Combine(AppContext.BaseDirectory, "Tools", "SampleData", "metrics.json");
	}

	private static bool EnvironmentsMatch(string configuredEnvironment, string requestedEnvironment)
	{
		if (configuredEnvironment.Equals(requestedEnvironment, StringComparison.OrdinalIgnoreCase))
		{
			return true;
		}

		return NormalizeEnvironment(configuredEnvironment).Equals(NormalizeEnvironment(requestedEnvironment), StringComparison.OrdinalIgnoreCase);
	}

	private static string NormalizeEnvironment(string environment) =>
		environment.Trim().ToLowerInvariant() switch
		{
			"prod" => "production",
			"prd" => "production",
			"stage" => "staging",
			"stg" => "staging",
			"dev" => "development",
			_ => environment.Trim().ToLowerInvariant()
		};

}
