using IncidentResponseAgent.Application.Tools;

namespace IncidentResponseAgent.Infrastructure.Tools;

public sealed class FakeLogSearchProvider : ILogSearchProvider
{
	public Task<LogSearchResult> SearchAsync(LogSearchRequest request, CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(request);
		cancellationToken.ThrowIfCancellationRequested();

		if (string.IsNullOrWhiteSpace(request.Query))
		{
			throw new ArgumentException("Log search query cannot be empty.", nameof(request));
		}

		var maxResults = request.MaxResults <= 0 ? 1 : Math.Min(request.MaxResults, 3);
		var anchorTime = request.EndTime ?? DateTimeOffset.UtcNow;
		var entries = new List<LogSearchEntry>(maxResults);

		for (var index = 0; index < maxResults; index++)
		{
			entries.Add(new LogSearchEntry
			{
				Timestamp = anchorTime.AddMinutes(-(index + 1)),
				Source = string.IsNullOrWhiteSpace(request.ServiceName) ? "platform" : request.ServiceName,
				Level = index == 0 ? "Error" : "Warning",
				Message = BuildMessage(request, index),
				CorrelationId = request.ServiceName is null ? null : $"corr-{request.ServiceName}-{index + 1}"
			});
		}

		return Task.FromResult(new LogSearchResult
		{
			Entries = entries
		});
	}

	private static string BuildMessage(LogSearchRequest request, int index)
	{
		var environment = string.IsNullOrWhiteSpace(request.Environment) ? "unspecified environment" : request.Environment;
		var serviceName = string.IsNullOrWhiteSpace(request.ServiceName) ? "impacted service" : request.ServiceName;

		return index switch
		{
			0 => $"Detected incident-related error while searching '{request.Query}' for {serviceName} in {environment}.",
			1 => $"Follow-up warning suggests related dependency instability for {serviceName}.",
			_ => $"Additional supporting log match for '{request.Query}'."
		};
	}
}