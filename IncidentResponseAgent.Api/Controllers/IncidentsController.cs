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

    public IncidentsController(IAnalyzeIncidentUseCase analyzeIncidentUseCase)
    {
        _analyzeIncidentUseCase = analyzeIncidentUseCase;
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

        var result = await _analyzeIncidentUseCase.AnalyzeAsync(incident, cancellationToken);

        return Ok(new IncidentAnalysisResponse
        {
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

    private static IncidentSeverity ParseSeverity(string severity)
    {
        return Enum.Parse<IncidentSeverity>(severity, ignoreCase: true);
    }
}