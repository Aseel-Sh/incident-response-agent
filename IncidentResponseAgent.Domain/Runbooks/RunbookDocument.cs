namespace IncidentResponseAgent.Domain.Runbooks;

public sealed class RunbookDocument
{
	public RunbookDocument(
		string id,
		string title,
		string summary,
		string content,
		IReadOnlyCollection<string>? tags = null)
	{
		Id = string.IsNullOrWhiteSpace(id)
			? throw new ArgumentException("Runbook id cannot be empty.", nameof(id))
			: id.Trim();

		Title = string.IsNullOrWhiteSpace(title)
			? throw new ArgumentException("Runbook title cannot be empty.", nameof(title))
			: title.Trim();

		Summary = string.IsNullOrWhiteSpace(summary)
			? throw new ArgumentException("Runbook summary cannot be empty.", nameof(summary))
			: summary.Trim();

		Content = string.IsNullOrWhiteSpace(content)
			? throw new ArgumentException("Runbook content cannot be empty.", nameof(content))
			: content.Trim();

		Tags = tags is null
			? Array.Empty<string>()
			: tags.Where(tag => !string.IsNullOrWhiteSpace(tag)).Select(tag => tag.Trim()).ToArray();
	}

	public string Id { get; }

	public string Title { get; }

	public string Summary { get; }

	public string Content { get; }

	public IReadOnlyCollection<string> Tags { get; }
}