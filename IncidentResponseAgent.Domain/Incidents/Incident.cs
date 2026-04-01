namespace IncidentResponseAgent.Domain.Incidents;

public sealed class Incident
{
    public Incident(
        Guid id,
        string title,
        string description,
        IncidentSeverity severity,
        string? serviceName = null,
        string? environment = null,
        DateTimeOffset? timestamp = null,
        IReadOnlyCollection<string>? tags = null)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Incident id cannot be empty.", nameof(id));
        }

        Title = string.IsNullOrWhiteSpace(title)
            ? throw new ArgumentException("Incident title cannot be empty.", nameof(title))
            : title.Trim();

        Description = string.IsNullOrWhiteSpace(description)
            ? throw new ArgumentException("Incident description cannot be empty.", nameof(description))
            : description.Trim();

        if (severity == IncidentSeverity.Unknown)
        {
            throw new ArgumentException("Incident severity must be specified.", nameof(severity));
        }

        Id = id;
        Severity = severity;
        ServiceName = string.IsNullOrWhiteSpace(serviceName) ? null : serviceName.Trim();
        Environment = string.IsNullOrWhiteSpace(environment) ? null : environment.Trim();
        Timestamp = timestamp;
        Tags = tags is null
            ? Array.Empty<string>()
            : tags.Where(tag => !string.IsNullOrWhiteSpace(tag)).Select(tag => tag.Trim()).ToArray();
    }

    public Guid Id { get; }

    public string Title { get; }

    public string Description { get; }

    public IncidentSeverity Severity { get; }

    public string? ServiceName { get; }

    public string? Environment { get; }

    public DateTimeOffset? Timestamp { get; }

    public IReadOnlyCollection<string> Tags { get; }
}