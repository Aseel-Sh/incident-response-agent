namespace IncidentResponseAgent.Api.Contracts.Incidents;

public sealed record IncidentSubmissionRequest
{
    public string Title { get; init; } = string.Empty;

    public string Description { get; init; } = string.Empty;

    public string Severity { get; init; } = string.Empty;

    public string? ServiceName { get; init; }

    public string? Environment { get; init; }

    public DateTimeOffset? Timestamp { get; init; }

    public IReadOnlyList<string>? Tags { get; init; }
}