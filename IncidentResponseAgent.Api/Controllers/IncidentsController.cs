using IncidentResponseAgent.Api.Contracts.Incidents;
using IncidentResponseAgent.Application.Incidents;
using IncidentResponseAgent.Domain.Incidents;
using Microsoft.AspNetCore.Mvc;
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

    public IncidentsController(
        IAnalyzeIncidentUseCase analyzeIncidentUseCase,
        IGetRecentIncidentAnalysesUseCase getRecentIncidentAnalysesUseCase,
        IIncidentSignalMonitor incidentSignalMonitor)
    {
        _analyzeIncidentUseCase = analyzeIncidentUseCase;
        _getRecentIncidentAnalysesUseCase = getRecentIncidentAnalysesUseCase;
        _incidentSignalMonitor = incidentSignalMonitor;
    }

    [HttpPost("analyze")]
    [ProducesResponseType(typeof(IncidentAnalysisResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IncidentAnalysisResponse>> AnalyzeAsync(
        [FromBody] IncidentSubmissionRequest request,
        CancellationToken cancellationToken)
    {
        var incident = new Incident(
            Guid.NewGuid(),
            request.Title,
            request.Description,
            ParseSeverity(request.Severity),
            request.ServiceName,
            request.Environment,
            request.Timestamp,
            request.Tags);

        var result = await _analyzeIncidentUseCase.AnalyzeAsync(incident, request.SessionId, cancellationToken);

        return Ok(new IncidentAnalysisResponse
        {
            SessionId = result.SessionId,
            SessionTurnNumber = result.SessionTurnNumber,
            SessionContextSummary = result.SessionContextSummary,
            IncidentSummary = result.IncidentSummary,
            AnalysisText = result.AnalysisText,
            RetrievedEvidence = result.Evidence.Select(item => new IncidentResponseAgent.Api.Contracts.Incidents.IncidentAnalysisEvidenceItem
            {
                Summary = item.Summary,
                Source = item.Source,
                Details = item.Details
            }).ToArray(),
            RootCauseHypotheses = result.Hypotheses.Select(hypothesis => new IncidentResponseAgent.Api.Contracts.Incidents.IncidentHypothesis
            {
                Description = hypothesis.Description,
                InferenceStrength = hypothesis.InferenceStrength,
                Confidence = hypothesis.Confidence,
                SupportingEvidence = hypothesis.SupportingEvidence,
                EvidenceReferences = hypothesis.EvidenceReferences
            }).ToArray(),
            RecommendedActions = result.RecommendedActions.Select(action => new IncidentResponseAgent.Api.Contracts.Incidents.IncidentActionRecommendation
            {
                Description = action.Description,
                Priority = action.Priority,
                Rationale = action.Rationale,
                SupportingSignals = action.SupportingSignals
            }).ToArray(),
            Confidence = result.Confidence,
            Notes = result.Notes
        });
    }

    [HttpGet("recent")]
    [ProducesResponseType(typeof(IReadOnlyList<RecentIncidentAnalysisResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<RecentIncidentAnalysisResponse>>> GetRecentAsync(
        [FromQuery] int maxResults = 10,
        CancellationToken cancellationToken = default)
    {
        var results = await _getRecentIncidentAnalysesUseCase.ExecuteAsync(maxResults, cancellationToken);

        return Ok(results.Select(result => new RecentIncidentAnalysisResponse
        {
            IncidentId = result.IncidentId,
            IncidentSummary = result.IncidentSummary,
            AnalysisText = result.AnalysisText,
            SessionId = result.SessionId,
            SessionTurnNumber = result.SessionTurnNumber,
            Confidence = result.Confidence,
            Notes = result.Notes,
            CreatedAtUtc = result.CreatedAtUtc
        }).ToArray());
    }

    [HttpGet("detected")]
    [ProducesResponseType(typeof(IReadOnlyList<DetectedIncidentResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<DetectedIncidentResponse>>> GetDetectedAsync(
        CancellationToken cancellationToken)
    {
        var results = await _incidentSignalMonitor.DetectAsync(cancellationToken);
        return Ok(results.Select(result => new DetectedIncidentResponse
        {
            Id = result.Id,
            Title = result.Title,
            Description = result.Description,
            Severity = result.Severity.ToString(),
            ServiceName = result.ServiceName,
            Environment = result.Environment,
            DetectedAtUtc = result.DetectedAtUtc,
            Source = result.Source,
            Signals = result.Signals,
            SuggestedTags = result.SuggestedTags
        }).ToArray());
    }

    private static IncidentSeverity ParseSeverity(string severity)
    {
        return Enum.Parse<IncidentSeverity>(severity, ignoreCase: true);
    }
}
