using IncidentResponseAgent.Infrastructure.Runbooks;

namespace IncidentResponseAgent.Api.Services;

public sealed class DependencyReadinessProbe(IHttpClientFactory clients)
{
    public async Task<string> QdrantAsync(RunbookRetrievalOptions options, CancellationToken cancellationToken)
    {
        if (!Uri.TryCreate(options.QdrantEndpoint, UriKind.Absolute, out var endpoint)
            || endpoint.Scheme is not ("http" or "https") || !string.IsNullOrEmpty(endpoint.UserInfo)) return "unavailable";
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(2));
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(endpoint.ToString().TrimEnd('/') + "/readyz"));
            if (!string.IsNullOrWhiteSpace(options.QdrantApiKey)) request.Headers.Add("api-key", options.QdrantApiKey);
            using var client = clients.CreateClient("readiness");
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
            return response.IsSuccessStatusCode ? "reachable" : "unavailable";
        }
        catch (Exception exception) when (exception is HttpRequestException or OperationCanceledException)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return "unavailable";
        }
    }

    public static string Persistence(IEnumerable<string?> paths)
    {
        if (paths.Any(string.IsNullOrWhiteSpace)) return "unavailable";
        try
        {
            foreach (var path in paths)
            {
                var directory = Path.GetDirectoryName(Path.GetFullPath(path!))!;
                Directory.CreateDirectory(directory);
                var probe = Path.Combine(directory, ".readiness-" + Guid.NewGuid().ToString("N"));
                using var stream = new FileStream(probe, FileMode.CreateNew, FileAccess.Write, FileShare.None, 1, FileOptions.DeleteOnClose);
                stream.WriteByte(0);
                stream.Flush(true);
            }
            return "reachable";
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        { return "unavailable"; }
    }
}
