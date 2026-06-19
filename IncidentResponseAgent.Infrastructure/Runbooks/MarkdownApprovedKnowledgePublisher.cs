using System.Text;
using IncidentResponseAgent.Application.Incidents;
using Microsoft.Extensions.Options;

namespace IncidentResponseAgent.Infrastructure.Runbooks;

public sealed class MarkdownApprovedKnowledgePublisher : IApprovedKnowledgePublisher
{
	private readonly RunbookRetrievalOptions _options;

	public MarkdownApprovedKnowledgePublisher(IOptions<RunbookRetrievalOptions> options)
	{
		_options = options.Value ?? new RunbookRetrievalOptions();
	}

	public async Task PublishAsync(Guid proposalId, string title, string content, CancellationToken cancellationToken = default)
	{
		if (string.IsNullOrWhiteSpace(content)) throw new ArgumentException("Approved knowledge content is required.", nameof(content));
		var directory = ResolveKnowledgeBasePath();
		Directory.CreateDirectory(directory);
		var document = $"# {title.Trim()}\n\ntags: approved, incident-learning\n\n{content.Trim()}\n";
		await File.WriteAllTextAsync(GetPath(directory, proposalId), document, new UTF8Encoding(false), cancellationToken).ConfigureAwait(false);
	}

	public Task RemoveAsync(Guid proposalId, CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();
		var path = GetPath(ResolveKnowledgeBasePath(), proposalId);
		if (File.Exists(path)) File.Delete(path);
		return Task.CompletedTask;
	}

	private static string GetPath(string directory, Guid proposalId) => Path.Combine(directory, $"approved-{proposalId:N}.md");

	private string ResolveKnowledgeBasePath()
	{
		if (!string.IsNullOrWhiteSpace(_options.KnowledgeBasePath))
		{
			return Path.GetFullPath(Environment.ExpandEnvironmentVariables(_options.KnowledgeBasePath));
		}

		var candidates = new[]
		{
			Path.Combine(AppContext.BaseDirectory, "Runbooks", "KnowledgeBase"),
			Path.Combine(AppContext.BaseDirectory, "KnowledgeBase", "Runbooks"),
			Path.Combine(AppContext.BaseDirectory, "KnowledgeBase")
		};
		return candidates.FirstOrDefault(Directory.Exists) ?? candidates[0];
	}
}
