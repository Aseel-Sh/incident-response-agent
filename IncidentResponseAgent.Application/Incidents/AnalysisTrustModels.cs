namespace IncidentResponseAgent.Application.Incidents;

public sealed record GroundedIncidentClaim
{
	public required string Claim { get; init; }
	public IReadOnlyList<string> EvidenceReferences { get; init; } = Array.Empty<string>();
}

public sealed record IncidentRunbookMatch
{
	public required string Id { get; init; }
	public required string Title { get; init; }
	public required string Summary { get; init; }
}

public sealed record AnalysisQualityScore
{
	public string EvidenceCoverage { get; init; } = "Low";
	public string RunbookMatchQuality { get; init; } = "Low";
	public string RecommendationSpecificity { get; init; } = "Low";
	public IReadOnlyList<string> MissingData { get; init; } = Array.Empty<string>();
	public string ProviderUsed { get; init; } = "unknown";
	public string FallbackStatus { get; init; } = "not used";
}

public sealed record AnalysisProviderTransparency
{
	public string ModelProvider { get; init; } = "unknown";
	public string? Model { get; init; }
	public string EmbeddingProvider { get; init; } = "unknown";
	public string VectorStore { get; init; } = "unknown";
	public string RagStatus { get; init; } = "unknown";
	public bool UsedModelFallback { get; init; }
	public string? FallbackReason { get; init; }
	public bool IsDegraded { get; init; }
	public string? DegradedReason { get; init; }
	public bool UsedStructuredOutputRetry { get; init; }
	public string? StructuredOutputRetryReason { get; init; }
}

public sealed record IncidentAnalysisFeedback
{
	public Guid Id { get; init; } = Guid.NewGuid();
	public required string AnalysisUsefulness { get; init; }
	public required string RecommendationCorrectness { get; init; }
	public IReadOnlyList<string> ReasonTags { get; init; } = Array.Empty<string>();
	public string? RecommendationDescription { get; init; }
	public string? Comments { get; init; }
	public required DateTimeOffset SubmittedAtUtc { get; init; }
}
