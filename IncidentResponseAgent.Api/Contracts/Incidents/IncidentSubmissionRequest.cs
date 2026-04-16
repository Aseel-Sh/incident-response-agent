using System.ComponentModel.DataAnnotations;

namespace IncidentResponseAgent.Api.Contracts.Incidents;

public sealed record IncidentSubmissionRequest : IValidatableObject
{
    private static readonly HashSet<string> AllowedSeverities = new(StringComparer.OrdinalIgnoreCase)
    {
        "Low",
        "Medium",
        "High",
        "Critical"
    };

    [Required]
    [StringLength(200, MinimumLength = 1)]
    public string Title { get; init; } = string.Empty;

    [Required]
    [StringLength(5000, MinimumLength = 1)]
    public string Description { get; init; } = string.Empty;

    [Required]
    [StringLength(20, MinimumLength = 1)]
    public string Severity { get; init; } = string.Empty;

    [StringLength(200)]
    public string? ServiceName { get; init; }

    [StringLength(100)]
    public string? Environment { get; init; }

    [StringLength(100)]
    public string? SessionId { get; init; }

    public DateTimeOffset? Timestamp { get; init; }

    public IReadOnlyList<string>? Tags { get; init; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!AllowedSeverities.Contains(Severity))
        {
            yield return new ValidationResult(
                "Severity must be one of: Low, Medium, High, Critical.",
                [nameof(Severity)]);
        }

        if (Tags is { Count: > 10 })
        {
            yield return new ValidationResult(
                "No more than 10 tags are allowed.",
                [nameof(Tags)]);
        }

        if (Tags is null)
        {
            yield break;
        }

        for (var index = 0; index < Tags.Count; index++)
        {
            var tag = Tags[index];

            if (string.IsNullOrWhiteSpace(tag))
            {
                yield return new ValidationResult(
                    $"Tag at position {index + 1} cannot be empty.",
                    [nameof(Tags)]);
                continue;
            }

            if (tag.Length > 50)
            {
                yield return new ValidationResult(
                    $"Tag '{tag}' cannot exceed 50 characters.",
                    [nameof(Tags)]);
            }
        }
    }
}