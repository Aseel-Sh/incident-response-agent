using System.ComponentModel.DataAnnotations;

namespace IncidentResponseAgent.Api.Contracts.Signals;

public sealed record IngestMetricSampleRequest
{
	[Required]
	[MaxLength(120)]
	public required string MetricName { get; init; }

	[Required]
	[MaxLength(120)]
	public required string ServiceName { get; init; }

	[Required]
	[MaxLength(80)]
	public required string Environment { get; init; }

	public DateTimeOffset? Timestamp { get; init; }

	[Range(typeof(decimal), "-1000000000", "1000000000")]
	public required decimal Value { get; init; }
}
