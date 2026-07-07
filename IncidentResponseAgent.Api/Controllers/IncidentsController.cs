using IncidentResponseAgent.Api.Contracts.Incidents;
using IncidentResponseAgent.Application.Incidents;
using IncidentResponseAgent.Domain.Incidents;
using IncidentResponseAgent.Infrastructure.Tools;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using ApplicationAnalysisActionRecommendation = IncidentResponseAgent.Application.Incidents.IncidentActionRecommendation;
using ApplicationAnalysisEvidenceItem = IncidentResponseAgent.Application.Incidents.IncidentAnalysisEvidenceItem;
using ApplicationAnalysisHypothesis = IncidentResponseAgent.Application.Incidents.IncidentHypothesis;

namespace IncidentResponseAgent.Api.Controllers;

[ApiController]
[Route("api/incidents")]
public sealed class IncidentsController : ControllerBase
{
    private readonly IAnalyzeIncidentUseCase _analyzeIncidentUseCase;
    private readonly IGetRecentIncidentAnalysesUseCase _getRecentIncidentAnalysesUseCase;
    private readonly IIncidentSignalMonitor _incidentSignalMonitor;
    private readonly IIncidentRecordStore _incidentRecordStore;
    private readonly OperationalDataOptions _operationalDataOptions;

    public IncidentsController(
        IAnalyzeIncidentUseCase analyzeIncidentUseCase,
        IGetRecentIncidentAnalysesUseCase getRecentIncidentAnalysesUseCase,
        IIncidentSignalMonitor incidentSignalMonitor,
        IIncidentRecordStore incidentRecordStore,
        IOptions<OperationalDataOptions>? operationalDataOptions = null)
    {
        _analyzeIncidentUseCase = analyzeIncidentUseCase;
        _getRecentIncidentAnalysesUseCase = getRecentIncidentAnalysesUseCase;
        _incidentSignalMonitor = incidentSignalMonitor;
        _incidentRecordStore = incidentRecordStore;
        _operationalDataOptions = operationalDataOptions?.Value ?? new OperationalDataOptions();
    }

    [HttpPost("analyze")]
    [ProducesResponseType(typeof(IncidentAnalysisResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IncidentAnalysisResponse>> AnalyzeAsync(
        [FromBody] IncidentSubmissionRequest request,
        CancellationToken cancellationToken)
    {
        var now = request.Timestamp ?? DateTimeOffset.UtcNow;
        var candidate = BuildManualCandidate(request, now);
        await _incidentRecordStore.SaveCandidatesAsync([candidate], new MonitoringScanRecord { StartedAtUtc = now, CompletedAtUtc = now, CandidateCount = 1, Status = "manual" }, cancellationToken);
        var incident = await _incidentRecordStore.ConfirmCandidateAsync(candidate.Id, cancellationToken);

        var result = await _analyzeIncidentUseCase.AnalyzeAsync(incident, request.SessionId, cancellationToken);

        return Ok(ToAnalysisResponse(result));
    }

    [HttpPost("{incidentId:guid}/outcomes")]
    [ProducesResponseType(typeof(ActionOutcomeResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ActionOutcomeResponse>> AddOutcomeAsync(
        Guid incidentId,
        [FromBody] ActionOutcomeRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Description))
        {
            return BadRequest("Action outcome description is required.");
        }

        try
        {
            var outcome = await _incidentRecordStore.AddActionOutcomeAsync(
                incidentId,
                request.Description,
                request.Status,
                cancellationToken);

            return Ok(ToOutcomeResponse(outcome));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpPut("{incidentId:guid}/status")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<object>> UpdateStatusAsync(
        Guid incidentId,
        [FromBody] IncidentStatusRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var status = await _incidentRecordStore.UpdateStatusAsync(incidentId, request.Status, cancellationToken);
            return Ok(new { status });
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpDelete("{incidentId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteAsync(
        Guid incidentId,
        CancellationToken cancellationToken)
    {
        return await _incidentRecordStore.DeleteAsync(incidentId, cancellationToken)
            ? NoContent()
            : NotFound();
    }

    [HttpGet("recent")]
    [ProducesResponseType(typeof(IReadOnlyList<RecentIncidentAnalysisResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<RecentIncidentAnalysisResponse>>> GetRecentAsync(
        [FromQuery] int maxResults = 10,
        [FromQuery] string? projectId = null,
        CancellationToken cancellationToken = default)
    {
        var results = await _getRecentIncidentAnalysesUseCase.ExecuteAsync(maxResults, projectId, cancellationToken);

        return Ok(results.Select(result => new RecentIncidentAnalysisResponse
        {
            IncidentId = result.IncidentId,
            ProjectId = result.ProjectId,
            IncidentTitle = result.IncidentTitle,
            IncidentSummary = result.IncidentSummary,
            IncidentDescription = result.IncidentDescription,
            ServiceName = result.ServiceName,
            Environment = result.Environment,
            Severity = result.Severity,
            Tags = result.Tags,
            AnalysisText = result.AnalysisText,
            AnalysisProvider = result.AnalysisProvider,
            AnalysisModel = result.AnalysisModel,
            UsedFallbackAnalysis = result.UsedFallbackAnalysis,
            FallbackReason = result.FallbackReason,
            SessionId = result.SessionId,
            SessionTurnNumber = result.SessionTurnNumber,
            Confidence = result.Confidence,
            Notes = result.Notes,
            ActionOutcomes = result.ActionOutcomes.Select(ToOutcomeResponse).ToArray(),
            Status = result.Status,
            CreatedAtUtc = result.CreatedAtUtc,
            Timeline = result.Timeline.Select(ToTimelineResponse).ToArray(),
            ProposedKnowledgeUpdate = result.ProposedKnowledgeUpdate is null ? null : ToKnowledgeResponse(result.ProposedKnowledgeUpdate),
            Feedback = result.Feedback.Select(ToFeedbackResponse).ToArray(),
            KnownFacts = result.KnownFacts.Select(item => new GroundedClaimResponse(item.Claim, item.EvidenceReferences)).ToArray(),
            Unknowns = result.Unknowns,
            RunbookMatches = result.RunbookMatches.Select(item => new RunbookMatchResponse(item.Id, item.Title, item.Summary)).ToArray(),
            Hypotheses = result.Hypotheses.Select(item => new IncidentResponseAgent.Api.Contracts.Incidents.IncidentHypothesis { Description = item.Description, InferenceStrength = item.InferenceStrength, Confidence = item.Confidence, SupportingEvidence = item.SupportingEvidence, EvidenceReferences = item.EvidenceReferences }).ToArray(),
            RecommendedActions = result.RecommendedActions.Select(item => new IncidentResponseAgent.Api.Contracts.Incidents.IncidentActionRecommendation { Description = item.Description, Priority = item.Priority, Rationale = item.Rationale, SupportingSignals = item.SupportingSignals }).ToArray(),
            Evidence = result.Evidence.Select(item => new IncidentResponseAgent.Api.Contracts.Incidents.IncidentAnalysisEvidenceItem { Summary = item.Summary, Source = item.Source, Details = item.Details }).ToArray(),
            SimilarIncidents = result.SimilarIncidents.Select(ToSimilarResponse).ToArray(),
            Quality = new AnalysisQualityResponse(result.Quality.EvidenceCoverage, result.Quality.RunbookMatchQuality, result.Quality.RecommendationSpecificity, result.Quality.MissingData, result.Quality.ProviderUsed, result.Quality.FallbackStatus),
            ProviderTransparency = ToProviderResponse(result.ProviderTransparency)
        }).ToArray());
    }

    [HttpGet("detected")]
    [ProducesResponseType(typeof(IReadOnlyList<DetectedIncidentResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<DetectedIncidentResponse>>> GetDetectedAsync(
        [FromQuery] string? projectId,
        CancellationToken cancellationToken)
    {
        return Ok((await _incidentRecordStore.GetCandidatesAsync(projectId, cancellationToken)).Select(ToCandidateResponse).ToArray());
    }

    [HttpPost("scan")]
    public async Task<ActionResult<object>> ScanAsync(CancellationToken cancellationToken)
    {
        var started = DateTimeOffset.UtcNow;
        var results = await _incidentSignalMonitor.DetectAsync(cancellationToken);
        var completed = DateTimeOffset.UtcNow;
        var scan = new MonitoringScanRecord
        {
            StartedAtUtc = started,
            CompletedAtUtc = completed,
            CandidateCount = results.Count,
            ScannedSourceCount = 2,
            ErrorCount = CountUnavailableDetectionSources(),
            DurationMilliseconds = Math.Max(0, (completed - started).TotalMilliseconds)
        };
        await _incidentRecordStore.SaveCandidatesAsync(results, scan, cancellationToken);
        var candidates = await _incidentRecordStore.GetCandidatesAsync(projectId: null, cancellationToken);
        return Ok(new { scan, candidates = candidates.Select(ToCandidateResponse).ToArray() });
    }

    [HttpGet("monitoring/last-scan")]
    public async Task<ActionResult<object>> GetLastScanAsync([FromQuery] string? projectId, CancellationToken cancellationToken)
    {
        var scan = await _incidentRecordStore.GetLastScanAsync(projectId, cancellationToken);
        return Ok(new { scan, monitoredSourceCount = 2 });
    }

    [HttpPost("candidates/manual")]
    public async Task<ActionResult<DetectedIncidentResponse>> CreateManualCandidateAsync([FromBody] IncidentSubmissionRequest request, CancellationToken cancellationToken)
    {
        var now = request.Timestamp ?? DateTimeOffset.UtcNow;
        var candidate = BuildManualCandidate(request, now);
        await _incidentRecordStore.SaveCandidatesAsync([candidate], new MonitoringScanRecord { StartedAtUtc = now, CompletedAtUtc = now, CandidateCount = 1, Status = "manual" }, cancellationToken);
        var saved = (await _incidentRecordStore.GetCandidatesAsync(request.ProjectId, cancellationToken)).First(item => item.Id == candidate.Id);
        return Ok(ToCandidateResponse(saved));
    }

    [HttpPost("candidates/{candidateId}/confirm")]
    public async Task<ActionResult<IncidentAnalysisResponse>> ConfirmCandidateAsync(string candidateId, [FromQuery] string? sessionId, CancellationToken cancellationToken)
    {
        try
        {
            var incident = await _incidentRecordStore.ConfirmCandidateAsync(candidateId, cancellationToken);
            var result = await _analyzeIncidentUseCase.AnalyzeAsync(incident, sessionId, cancellationToken);
            return Ok(ToAnalysisResponse(result));
        }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (InvalidOperationException exception) { return Conflict(exception.Message); }
    }

    [HttpPost("candidates/{candidateId}/decision")]
    public async Task<ActionResult<DetectedIncidentResponse>> DecideCandidateAsync(string candidateId, [FromBody] CandidateDecisionRequest request, CancellationToken cancellationToken)
    {
        try { return Ok(ToCandidateResponse(await _incidentRecordStore.DecideCandidateAsync(candidateId, request.Decision, request.MergeIntoIncidentId, cancellationToken))); }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (InvalidOperationException exception) { return Conflict(exception.Message); }
        catch (ArgumentException exception) { return BadRequest(exception.Message); }
    }

    [HttpPost("{incidentId:guid}/knowledge-review")]
    public async Task<ActionResult<ProposedKnowledgeUpdateResponse>> ReviewKnowledgeAsync(Guid incidentId, [FromBody] KnowledgeReviewRequest request, CancellationToken cancellationToken)
    {
        try { return Ok(ToKnowledgeResponse(await _incidentRecordStore.ReviewKnowledgeUpdateAsync(incidentId, request.Decision, request.Content, request.Notes, cancellationToken))); }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (InvalidOperationException exception) { return Conflict(exception.Message); }
        catch (ArgumentException exception) { return BadRequest(exception.Message); }
    }

    [HttpPost("{incidentId:guid}/feedback")]
    public async Task<ActionResult<AnalysisFeedbackResponse>> AddFeedbackAsync(Guid incidentId, [FromBody] AnalysisFeedbackRequest request, CancellationToken cancellationToken)
    {
        var usefulness = request.AnalysisUsefulness?.Trim().ToLowerInvariant() ?? string.Empty;
        var correctness = request.RecommendationCorrectness?.Trim().ToLowerInvariant() ?? string.Empty;
		var reasonTags = request.ReasonTags ?? Array.Empty<string>();
        var allowedReasons = new HashSet<string>(["shallow", "missing evidence", "hallucinated evidence", "wrong sev", "wrong root cause", "bad remediation", "ignored runbook", "repeated failed past action", "repeated failed action", "other"], StringComparer.OrdinalIgnoreCase);
        if (usefulness is not ("useful" or "partially useful" or "not useful") || correctness is not ("correct" or "partially correct" or "wrong")) return BadRequest("Invalid feedback rating.");
        if (reasonTags.Any(tag => !allowedReasons.Contains(tag))) return BadRequest("One or more reason tags are invalid.");
        var feedback = new IncidentAnalysisFeedback
        {
            AnalysisUsefulness = usefulness, RecommendationCorrectness = correctness, ReasonTags = reasonTags.Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
            RecommendationDescription = request.RecommendationDescription?.Trim(), Comments = request.Comments?.Trim(), SubmittedAtUtc = DateTimeOffset.UtcNow
        };
        try { return Ok(ToFeedbackResponse(await _incidentRecordStore.AddFeedbackAsync(incidentId, feedback, cancellationToken))); }
        catch (KeyNotFoundException) { return NotFound(); }
    }

    private static IncidentSeverity ParseSeverity(string severity)
    {
        return Enum.Parse<IncidentSeverity>(severity, ignoreCase: true);
    }

    private int CountUnavailableDetectionSources()
    {
        var paths = new[]
        {
            ResolveOperationalPath(_operationalDataOptions.LogEntriesPath, Path.Combine("Tools", "SampleData", "logs.json")),
            ResolveOperationalPath(_operationalDataOptions.MetricSamplesPath, Path.Combine("Tools", "SampleData", "metrics.json"))
        };
        return paths.Count(path => !System.IO.File.Exists(path));
    }

    private static string ResolveOperationalPath(string? configuredPath, string defaultRelativePath) =>
        string.IsNullOrWhiteSpace(configuredPath)
            ? Path.Combine(AppContext.BaseDirectory, defaultRelativePath)
            : Path.GetFullPath(Environment.ExpandEnvironmentVariables(configuredPath));

    private static DetectedIncidentCandidate BuildManualCandidate(IncidentSubmissionRequest request, DateTimeOffset detectedAt) => new()
    {
        Id = $"manual-{Guid.NewGuid():N}", ProjectId = NormalizeProjectId(request.ProjectId), Title = request.Title, Description = request.Description, Severity = ParseSeverity(request.Severity),
        ServiceName = request.ServiceName, Environment = request.Environment, DetectedAtUtc = detectedAt, Source = "manual trigger",
        Signals = ["user-entered incident details"], SuggestedTags = request.Tags ?? Array.Empty<string>()
    };

    private static ActionOutcomeResponse ToOutcomeResponse(IncidentActionOutcome outcome)
    {
        return new ActionOutcomeResponse
        {
            Id = outcome.Id,
            Description = outcome.Description,
            Status = outcome.Status,
            LoggedAtUtc = outcome.LoggedAtUtc,
            EvidenceReference = outcome.EvidenceReference
        };
    }

    private static DetectedIncidentResponse ToCandidateResponse(DetectedIncidentCandidate result) => new()
    {
        Id = result.Id, ProjectId = NormalizeProjectId(result.ProjectId), Title = result.Title, Description = result.Description, Severity = result.Severity.ToString().ToLowerInvariant(),
        ServiceName = result.ServiceName, Environment = result.Environment, DetectedAtUtc = result.DetectedAtUtc, Source = result.Source,
        Signals = result.Signals, SuggestedTags = result.SuggestedTags, Status = result.Status, DuplicateIncidentId = result.DuplicateIncidentId,
        SimilarIncidents = result.SimilarIncidents.Select(ToSimilarResponse).ToArray(),
        Timeline = result.Timeline.Select(ToTimelineResponse).ToArray()
    };

    private static IncidentTimelineEventResponse ToTimelineResponse(IncidentTimelineEvent item) => new(item.Type, item.OccurredAtUtc, item.Summary, item.Actor, item.EvidenceReference);

    private static ProposedKnowledgeUpdateResponse ToKnowledgeResponse(ProposedKnowledgeUpdate item) => new(item.Id, item.Title, item.Content, item.Status, item.GeneratedAtUtc, item.ReviewedAtUtc, item.ReviewNotes);

    private static AnalysisFeedbackResponse ToFeedbackResponse(IncidentAnalysisFeedback item) => new(item.Id, item.AnalysisUsefulness, item.RecommendationCorrectness, item.ReasonTags, item.RecommendationDescription, item.Comments, item.SubmittedAtUtc);

    private static SimilarIncidentResponse ToSimilarResponse(SimilarIncidentMatch item) => new(item.IncidentId, item.IncidentSummary, item.ServiceName, item.Environment, item.CreatedAtUtc, item.Score, item.ResolutionSummary, item.SharedSignals, item.SuccessfulActions, item.FailedActions);

    private static IncidentAnalysisResponse ToAnalysisResponse(IncidentAnalysisResult result) => new()
    {
        IncidentId = result.IncidentId, ProjectId = result.ProjectId, SessionId = result.SessionId, SessionTurnNumber = result.SessionTurnNumber, SessionContextSummary = result.SessionContextSummary,
        IncidentSummary = result.IncidentSummary, Severity = result.Severity, AnalysisText = result.AnalysisText, AnalysisProvider = result.AnalysisProvider, AnalysisModel = result.AnalysisModel,
        UsedFallbackAnalysis = result.UsedFallbackAnalysis, FallbackReason = result.FallbackReason,
        RetrievedEvidence = result.Evidence.Select(item => new IncidentResponseAgent.Api.Contracts.Incidents.IncidentAnalysisEvidenceItem { Summary = item.Summary, Source = item.Source, Details = item.Details }).ToArray(),
        KnownFacts = result.KnownFacts.Select(item => new GroundedClaimResponse(item.Claim, item.EvidenceReferences)).ToArray(),
        Unknowns = result.Unknowns,
        RunbookMatches = result.RunbookMatches.Select(item => new RunbookMatchResponse(item.Id, item.Title, item.Summary)).ToArray(),
        RootCauseHypotheses = result.Hypotheses.Select(item => new IncidentResponseAgent.Api.Contracts.Incidents.IncidentHypothesis { Description = item.Description, InferenceStrength = item.InferenceStrength, Confidence = item.Confidence, SupportingEvidence = item.SupportingEvidence, EvidenceReferences = item.EvidenceReferences }).ToArray(),
        RecommendedActions = result.RecommendedActions.Select(item => new IncidentResponseAgent.Api.Contracts.Incidents.IncidentActionRecommendation { Description = item.Description, Priority = item.Priority, Rationale = item.Rationale, SupportingSignals = item.SupportingSignals }).ToArray(),
        ActionOutcomes = result.ActionOutcomes.Select(ToOutcomeResponse).ToArray(),
        SimilarIncidents = result.SimilarIncidents.Select(ToSimilarResponse).ToArray(),
        Quality = new AnalysisQualityResponse(result.Quality.EvidenceCoverage, result.Quality.RunbookMatchQuality, result.Quality.RecommendationSpecificity, result.Quality.MissingData, result.Quality.ProviderUsed, result.Quality.FallbackStatus),
        ProviderTransparency = ToProviderResponse(result.ProviderTransparency),
        Confidence = result.Confidence, Notes = result.Notes
    };

    private static string NormalizeProjectId(string? projectId) => string.IsNullOrWhiteSpace(projectId) ? "default" : projectId.Trim();

    private static ProviderTransparencyResponse ToProviderResponse(AnalysisProviderTransparency provider) => new(
        provider.ModelProvider, provider.Model, provider.EmbeddingProvider, provider.VectorStore, provider.RagStatus,
		provider.UsedModelFallback, provider.FallbackReason, provider.IsDegraded, provider.DegradedReason,
		provider.UsedStructuredOutputRetry, provider.StructuredOutputRetryReason, provider.AttemptedModelProvider,
		provider.AttemptedModel, provider.EvidenceGatheringDurationMilliseconds, provider.RagDurationMilliseconds,
		provider.ModelDurationMilliseconds, provider.FallbackStage, provider.TimeoutSource, provider.ModelResponseWarning);
}
