using IncidentResponseAgent.Api.Contracts.Incidents;
using IncidentResponseAgent.Application.Incidents;
using IncidentResponseAgent.Domain.Incidents;
using Microsoft.AspNetCore.Mvc;

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
            RetrievedEvidence = result.Evidence,
            RootCauseHypotheses = result.Hypotheses,
            RecommendedActions = result.RecommendedActions,
            Confidence = result.Confidence,
            Notes = result.Notes
        });
    }

    private static IncidentSeverity ParseSeverity(string severity)
    {
        return Enum.Parse<IncidentSeverity>(severity, ignoreCase: true);
    }
}