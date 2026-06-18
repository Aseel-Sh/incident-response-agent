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
    private readonly IIncidentRecordStore _incidentRecordStore;

    public IncidentsController(
        IAnalyzeIncidentUseCase analyzeIncidentUseCase,
        IGetRecentIncidentAnalysesUseCase getRecentIncidentAnalysesUseCase,
        IIncidentSignalMonitor incidentSignalMonitor,
        IIncidentRecordStore incidentRecordStore)
    {
        _analyzeIncidentUseCase = analyzeIncidentUseCase;
        _getRecentIncidentAnalysesUseCase = getRecentIncidentAnalysesUseCase;
        _incidentSignalMonitor = incidentSignalMonitor;
        _incidentRecordStore = incidentRecordStore;
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
            IncidentId = result.IncidentId,
            SessionId = result.SessionId,
            SessionTurnNumber = result.SessionTurnNumber,
            SessionContextSummary = result.SessionContextSummary,
            IncidentSummary = result.IncidentSummary,
            AnalysisText = result.AnalysisText,
            AnalysisProvider = result.AnalysisProvider,
            AnalysisModel = result.AnalysisModel,
            UsedFallbackAnalysis = result.UsedFallbackAnalysis,
            FallbackReason = result.FallbackReason,
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
            ActionOutcomes = result.ActionOutcomes.Select(ToOutcomeResponse).ToArray(),
            Confidence = result.Confidence,
            Notes = result.Notes
        });
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
        CancellationToken cancellationToken = default)
    {
        var results = await _getRecentIncidentAnalysesUseCase.ExecuteAsync(maxResults, cancellationToken);

        return Ok(results.Select(result => new RecentIncidentAnalysisResponse
        {
            IncidentId = result.IncidentId,
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

    private static ActionOutcomeResponse ToOutcomeResponse(IncidentActionOutcome outcome)
    {
        return new ActionOutcomeResponse
        {
            Description = outcome.Description,
            Status = outcome.Status,
            LoggedAtUtc = outcome.LoggedAtUtc
        };
    }
}
