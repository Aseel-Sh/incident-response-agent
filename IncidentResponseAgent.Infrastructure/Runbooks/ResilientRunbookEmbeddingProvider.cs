using Microsoft.Extensions.Logging;

namespace IncidentResponseAgent.Infrastructure.Runbooks;

internal sealed class ResilientRunbookEmbeddingProvider : IRunbookEmbeddingProvider
{
	private readonly IRunbookEmbeddingProvider _fallback;
	private readonly bool _allowFallback;
	private readonly ILogger<ResilientRunbookEmbeddingProvider> _logger;
	private readonly IRunbookEmbeddingProvider? _primary;
	private volatile bool _useFallbackOnly;
	private string? _failureReason;

	public ResilientRunbookEmbeddingProvider(
		IRunbookEmbeddingProvider? primary,
		IRunbookEmbeddingProvider fallback,
		bool allowFallback,
		ILogger<ResilientRunbookEmbeddingProvider> logger)
	{
		_primary = primary;
		_fallback = fallback;
		_allowFallback = allowFallback;
		_logger = logger;
	}

	public string ProviderName
	{
		get
		{
			if (_primary is null)
			{
				return _allowFallback ? _fallback.ProviderName : "external-embedding-unconfigured";
			}

			return _useFallbackOnly && _allowFallback
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
				return _allowFallback ? _fallback.ModelName : "not configured";
			}

			return _useFallbackOnly && _allowFallback
				? $"{_primary.ModelName}->fallback:{_fallback.ModelName}"
				: _primary.ModelName;
		}
	}

	public int Dimensions => (_useFallbackOnly || _primary is null) && _allowFallback ? _fallback.Dimensions : _primary?.Dimensions ?? 0;

	public bool IsDegraded => _useFallbackOnly || _primary is null;

	public string? DegradedReason
	{
		get
		{
			if (_primary is null)
			{
				return _allowFallback
					? $"External embedding provider is not configured; {_fallback.ProviderName} is serving embeddings because local fallback is enabled."
					: "External embedding provider is not configured and local embedding fallback is disabled.";
			}

			if (!_useFallbackOnly) return null;
			return _allowFallback
				? $"Primary embedding provider {_primary.ProviderName} failed ({_failureReason ?? "unknown failure"}); {_fallback.ProviderName} is serving embeddings because local fallback is enabled."
				: $"Primary embedding provider {_primary.ProviderName} failed ({_failureReason ?? "unknown failure"}); local embedding fallback is disabled.";
		}
	}

	public async Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default)
	{
		if (_primary is null)
		{
			if (_allowFallback)
			{
				return await _fallback.GenerateEmbeddingAsync(text, cancellationToken).ConfigureAwait(false);
			}

			throw new InvalidOperationException("External embedding provider is not configured. Set Runbooks:SemanticRetrieval:ApiKey or HF_TOKEN, or explicitly enable AllowLocalEmbeddingFallback for offline demo mode.");
		}

		if (_useFallbackOnly)
		{
			if (_allowFallback)
			{
				return await _fallback.GenerateEmbeddingAsync(text, cancellationToken).ConfigureAwait(false);
			}

			throw new InvalidOperationException(DegradedReason);
		}

		try
		{
			var embedding = await _primary.GenerateEmbeddingAsync(text, cancellationToken).ConfigureAwait(false);
			if (embedding.Length > 0)
			{
				return embedding;
			}

			_logger.LogWarning(
				"Primary embedding provider {ProviderName} returned an empty vector. LocalFallbackEnabled={LocalFallbackEnabled} FallbackProvider={FallbackProviderName}.",
				_primary.ProviderName,
				_allowFallback,
				_fallback.ProviderName);
			_failureReason = "empty embedding vector";
			_useFallbackOnly = true;
			if (!_allowFallback)
			{
				throw new InvalidOperationException(DegradedReason);
			}
		}
		catch (OperationCanceledException)
		{
			throw;
		}
		catch (Exception exception)
		{
			_failureReason = SanitizeFailure(exception);
			_useFallbackOnly = true;
			if (!_allowFallback)
			{
				_logger.LogWarning(exception, "Primary embedding provider failed and local fallback is disabled. Provider={ProviderName} FailureReason={FailureReason}.", _primary.ProviderName, _failureReason);
				throw new InvalidOperationException(DegradedReason, exception);
			}
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
