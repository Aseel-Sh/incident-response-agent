using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace IncidentResponseAgent.Agent.Incidents;

// Validate the provider envelope before the SDK assumes choices[0] exists.
// Only metadata is logged; model content and credentials are never logged here.
internal sealed class CompletionResponseHandler(HttpClient client, ILogger logger) : HttpMessageHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        using var forwarded = new HttpRequestMessage(request.Method, request.RequestUri) { Version = request.Version, VersionPolicy = request.VersionPolicy };
        foreach (var header in request.Headers) forwarded.Headers.TryAddWithoutValidation(header.Key, header.Value);
        if (request.Content is not null)
        {
            forwarded.Content = new ByteArrayContent(await request.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false));
            foreach (var header in request.Content.Headers) forwarded.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }
        var response = await client.SendAsync(forwarded, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode || !request.RequestUri!.AbsolutePath.EndsWith("/chat/completions", StringComparison.Ordinal)) return response;
        try
        {
            using var envelope = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false));
            var root = envelope.RootElement;
            var count = root.TryGetProperty("choices", out var choices) && choices.ValueKind == JsonValueKind.Array ? choices.GetArrayLength() : 0;
            var finish = count > 0 && choices[0].TryGetProperty("finish_reason", out var reason) && reason.ValueKind == JsonValueKind.String ? reason.GetString() : null;
            logger.LogInformation("Completion envelope received. ChoiceCount={ChoiceCount} FinishReason={FinishReason} ProviderError={ProviderError}.", count, finish, root.TryGetProperty("error", out _));
            if (root.TryGetProperty("error", out var providerError) && providerError.ValueKind == JsonValueKind.Object)
            {
                var errorCode = providerError.TryGetProperty("code", out var code) && code.ValueKind == JsonValueKind.Number && code.TryGetInt32(out var numericCode) ? numericCode : 0;
                logger.LogWarning("Completion provider reported an error inside a successful HTTP response. ProviderErrorCode={ProviderErrorCode}.", errorCode);
                var explanation = errorCode switch
                {
                    429 => "The model provider reported a rate or quota limit.",
                    402 => "The model provider reported insufficient credits.",
                    400 => "The model provider rejected the requested analysis parameters.",
                    404 => "No model provider endpoint is available for the requested parameters.",
                    502 or 503 => "The upstream model provider is unavailable.",
                    _ => "The model provider returned an error instead of an analysis."
                };
                throw new ModelStructuredOutputException(explanation + " No analysis was accepted.");
            }
            if (count > 0 && choices[0].TryGetProperty("message", out var message) && message.TryGetProperty("content", out var content) && content.ValueKind == JsonValueKind.String)
            {
                var text = content.GetString()?.Trim() ?? "";
                var shape = text.StartsWith("```") ? "markdown-fence" : text.StartsWith('{') ? "json-object-prefix" : text.StartsWith('[') ? "json-array-prefix" : text.Length == 0 ? "empty" : "other-text";
                var validJson = false;
                try { using var parsed = JsonDocument.Parse(text); validJson = true; } catch (JsonException) { }
                logger.LogInformation("Completion content metadata. Characters={Characters} Format={Format} ValidJson={ValidJson}.", text.Length, shape, validJson);
            }
            if (count == 0) throw new ModelStructuredOutputException("The model provider returned no completion choices. No analysis was accepted.");
            if (finish == "length") throw new ModelStructuredOutputException("The model reached its output token limit before completing the analysis JSON.");
            return response;
        }
        catch
        {
            response.Dispose();
            throw;
        }
    }
}
