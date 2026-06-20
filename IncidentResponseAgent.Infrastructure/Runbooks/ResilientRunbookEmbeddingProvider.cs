using Microsoft.Extensions.Logging;

namespace IncidentResponseAgent.Infrastructure.Runbooks;

internal sealed class ResilientRunbookEmbeddingProvider : IRunbookEmbeddingProvider
{
	private readonly IRunbookEmbeddingProvider _fallback;
	private readonly ILogger<ResilientRunbookEmbeddingProvider> _logger;
	private readonly IRunbookEmbeddingProvider? _primary;
	private volatile bool _useFallbackOnly;
	private string? _failureReason;

	public ResilientRunbookEmbeddingProvider(
		IRunbookEmbeddingProvider? primary,
		IRunbookEmbeddingProvider fallback,
		ILogger<ResilientRunbookEmbeddingProvider> logger)
	{
		_primary = primary;
		_fallback = fallback;
		_logger = logger;
	}

	public string ProviderName
	{
		get
		{
			if (_primary is null)
			{
				return _fallback.ProviderName;
			}

			return _useFallbackOnly
				? $"{_primary.ProviderName}-failed/{_fallback.ProviderName}"
				: _primary.ProviderName;
		}
	}

	public string ModelName
	{
		get
		{
			if (_primary is null)
			{
				return _fallback.ModelName;
			}

			return _useFallbackOnly
				? $"{_primary.ModelName}->fallback:{_fallback.ModelName}"
				: _primary.ModelName;
		}
	}

	public int Dimensions => _useFallbackOnly || _primary is null ? _fallback.Dimensions : _primary.Dimensions;

	public bool IsDegraded => _useFallbackOnly;

	public string? DegradedReason => _useFallbackOnly
		? $"Primary embedding provider {_primary?.ProviderName} failed ({_failureReason ?? "unknown failure"}); {_fallback.ProviderName} is serving embeddings."
		: null;

	public async Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default)
	{
		if (_primary is null || _useFallbackOnly)
		{
			return await _fallback.GenerateEmbeddingAsync(text, cancellationToken).ConfigureAwait(false);
		}

		try
		{
			var embedding = await _primary.GenerateEmbeddingAsync(text, cancellationToken).ConfigureAwait(false);
			if (embedding.Length > 0)
			{
				return embedding;
			}

			_logger.LogWarning("Primary embedding provider {ProviderName} returned an empty vector. Falling back to {FallbackProviderName}.", _primary.ProviderName, _fallback.ProviderName);
			_failureReason = "empty embedding vector";
			_useFallbackOnly = true;
		}
		catch (OperationCanceledException)
		{
			throw;
		}
		catch (Exception exception)
		{
			_failureReason = SanitizeFailure(exception);
			_useFallbackOnly = true;
			_logger.LogWarning(exception, "Primary embedding provider failed. Provider={ProviderName} FailureReason={FailureReason} FallbackProvider={FallbackProviderName}.", _primary.ProviderName, _failureReason, _fallback.ProviderName);
		}

		return await _fallback.GenerateEmbeddingAsync(text, cancellationToken).ConfigureAwait(false);
	}

	private static string SanitizeFailure(Exception exception)
	{
		var message = string.IsNullOrWhiteSpace(exception.Message) ? exception.GetType().Name : exception.Message.ReplaceLineEndings(" ").Trim();
		if (message.Length > 300) message = string.Concat(message.AsSpan(0, 297), "...");
		return $"{exception.GetType().Name}: {message}";
	}
}
