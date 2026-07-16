using IncidentResponseAgent.Domain.Incidents;

namespace IncidentResponseAgent.Application.Incidents;

public sealed record IncidentAnalysisRecord
{
	public required Incident Incident { get; init; }

	public string ProjectId { get; init; } = "default";

	public required IncidentAnalysisResult AnalysisResult { get; init; }

	public string Status { get; init; } = "new";

	public string? Assignee { get; init; }

	public string? AcknowledgedBy { get; init; }

	public DateTimeOffset? AcknowledgedAtUtc { get; init; }

	public required DateTimeOffset CreatedAtUtc { get; init; }

	public DateTimeOffset UpdatedAtUtc { get; init; }

	public string? CandidateId { get; init; }

	public Guid? MergedIntoIncidentId { get; init; }

	public IReadOnlyList<IncidentTimelineEvent> Timeline { get; init; } = Array.Empty<IncidentTimelineEvent>();

	public ProposedKnowledgeUpdate? ProposedKnowledgeUpdate { get; init; }

	public IReadOnlyList<IncidentAnalysisFeedback> Feedback { get; init; } = Array.Empty<IncidentAnalysisFeedback>();
}
