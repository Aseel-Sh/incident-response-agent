using System.ComponentModel.DataAnnotations;

namespace IncidentResponseAgent.Api.Contracts.Signals;

public sealed record IngestMetricSampleRequest : IValidatableObject
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

	public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
	{
		if (string.IsNullOrWhiteSpace(MetricName))
		{
			yield return new ValidationResult("Metric name is required.", [nameof(MetricName)]);
		}

		if (string.IsNullOrWhiteSpace(ServiceName))
		{
			yield return new ValidationResult("Service name is required.", [nameof(ServiceName)]);
		}

		if (string.IsNullOrWhiteSpace(Environment))
		{
			yield return new ValidationResult("Environment is required.", [nameof(Environment)]);
		}
	}
}
