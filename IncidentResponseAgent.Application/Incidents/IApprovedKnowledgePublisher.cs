namespace IncidentResponseAgent.Application.Incidents;

public interface IApprovedKnowledgePublisher
{
	Task PublishAsync(Guid proposalId, Guid incidentId, string title, string content, CancellationToken cancellationToken = default);

	Task RemoveAsync(Guid proposalId, CancellationToken cancellationToken = default);
}
