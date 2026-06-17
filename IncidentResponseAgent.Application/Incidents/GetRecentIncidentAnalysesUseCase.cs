namespace IncidentResponseAgent.Application.Incidents;

public sealed class GetRecentIncidentAnalysesUseCase : IGetRecentIncidentAnalysesUseCase
{
	private readonly IIncidentRecordStore _incidentRecordStore;

	public GetRecentIncidentAnalysesUseCase(IIncidentRecordStore incidentRecordStore)
	{
		_incidentRecordStore = incidentRecordStore;
	}

	public async Task<IReadOnlyList<GetRecentIncidentAnalysesResult>> ExecuteAsync(int maxResults = 10, CancellationToken cancellationToken = default)
	{
		var records = await _incidentRecordStore.GetRecentAsync(maxResults, cancellationToken);

		return records.Select(record => new GetRecentIncidentAnalysesResult
		{
			IncidentId = record.Incident.Id,
			IncidentSummary = record.AnalysisResult.IncidentSummary,
			AnalysisText = record.AnalysisResult.AnalysisText,
			AnalysisProvider = record.AnalysisResult.AnalysisProvider,
			AnalysisModel = record.AnalysisResult.AnalysisModel,
			UsedFallbackAnalysis = record.AnalysisResult.UsedFallbackAnalysis,
			FallbackReason = record.AnalysisResult.FallbackReason,
			SessionId = record.AnalysisResult.SessionId,
			SessionTurnNumber = record.AnalysisResult.SessionTurnNumber,
			Confidence = record.AnalysisResult.Confidence,
			Notes = record.AnalysisResult.Notes,
			ActionOutcomes = record.AnalysisResult.ActionOutcomes,
			Status = record.Status,
			CreatedAtUtc = record.CreatedAtUtc
		}).ToArray();
	}
}
