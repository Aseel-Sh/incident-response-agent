using System.ComponentModel.DataAnnotations;

namespace IncidentResponseAgent.Api.Contracts.Signals;

public sealed record IngestLogEntryRequest : IValidatableObject
{
	public DateTimeOffset? Timestamp { get; init; }

	[Required]
	[MaxLength(120)]
	public required string Source { get; init; }

	[Required]
	[RegularExpression("Trace|Debug|Information|Warning|Error|Critical", ErrorMessage = "Level must be Trace, Debug, Information, Warning, Error, or Critical.")]
	public required string Level { get; init; }

	[Required]
	[MaxLength(2000)]
	public required string Message { get; init; }

	[MaxLength(120)]
	public string? CorrelationId { get; init; }

	[MaxLength(120)]
	public string? ProjectId { get; init; }

	public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
	{
		if (string.IsNullOrWhiteSpace(Source))
		{
			yield return new ValidationResult("Source is required.", [nameof(Source)]);
		}

		if (string.IsNullOrWhiteSpace(Level))
		{
			yield return new ValidationResult("Level is required.", [nameof(Level)]);
		}

		if (string.IsNullOrWhiteSpace(Message))
		{
			yield return new ValidationResult("Message is required.", [nameof(Message)]);
		}
	}
}
