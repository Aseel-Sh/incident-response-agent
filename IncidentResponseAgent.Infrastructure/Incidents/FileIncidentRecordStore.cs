using System.Text.Json;
using System.Text.RegularExpressions;
using IncidentResponseAgent.Application.Incidents;
using IncidentResponseAgent.Domain.Incidents;
using Microsoft.Extensions.Options;

namespace IncidentResponseAgent.Infrastructure.Incidents;

public sealed class FileIncidentRecordStore : IIncidentRecordStore
{
	private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
	{
		WriteIndented = true
	};

	private readonly SemaphoreSlim _fileLock = new(1, 1);
	private readonly string _filePath;
	private readonly string _workflowFilePath;
	private readonly IApprovedKnowledgePublisher? _approvedKnowledgePublisher;

	public FileIncidentRecordStore(IOptions<IncidentStorageOptions> options, IApprovedKnowledgePublisher? approvedKnowledgePublisher = null)
	{
		_approvedKnowledgePublisher = approvedKnowledgePublisher;
		var configuredPath = options.Value?.IncidentRecordsPath;
		if (!string.IsNullOrWhiteSpace(configuredPath))
		{
			_filePath = Path.GetFullPath(Environment.ExpandEnvironmentVariables(configuredPath));
			_workflowFilePath = Path.Combine(Path.GetDirectoryName(_filePath)!, $"{Path.GetFileNameWithoutExtension(_filePath)}-workflow.json");
			var configuredDirectory = Path.GetDirectoryName(_filePath);
			if (!string.IsNullOrWhiteSpace(configuredDirectory))
			{
				Directory.CreateDirectory(configuredDirectory);
			}

			return;
		}

		var rootFolder = Path.Combine(
			Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
			"IncidentResponseAgent");

		Directory.CreateDirectory(rootFolder);
		_filePath = Path.Combine(rootFolder, "incident-records.json");
		_workflowFilePath = Path.Combine(rootFolder, "incident-workflow.json");
	}

	public async Task SaveAsync(Incident incident, IncidentAnalysisResult analysisResult, CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(incident);
		ArgumentNullException.ThrowIfNull(analysisResult);
		cancellationToken.ThrowIfCancellationRequested();

		await _fileLock.WaitAsync(cancellationToken);
		try
		{
			var records = await ReadRecordsAsync(cancellationToken);
			var now = DateTimeOffset.UtcNow;
			var isExisting = records.TryGetValue(incident.Id, out var existing);
			records[incident.Id] = new IncidentAnalysisRecord
			{
				Incident = incident,
				AnalysisResult = analysisResult,
				Status = isExisting ? existing!.Status : "new",
				CreatedAtUtc = isExisting ? existing!.CreatedAtUtc : now,
				UpdatedAtUtc = now,
				CandidateId = existing?.CandidateId,
				MergedIntoIncidentId = existing?.MergedIntoIncidentId,
				ProposedKnowledgeUpdate = existing?.ProposedKnowledgeUpdate,
				Feedback = existing?.Feedback ?? Array.Empty<IncidentAnalysisFeedback>(),
				Timeline = (existing?.Timeline ?? Array.Empty<IncidentTimelineEvent>()).Concat(isExisting
					? [Event("analysis completed", "Evidence-grounded incident analysis completed.", now)]
					: [Event("incident created", "Manual incident created.", now, actor: "user"), Event("incident confirmed", "Manual incident confirmed for analysis.", now, actor: "user"), Event("analysis completed", "Evidence-grounded incident analysis completed.", now)]).ToArray()
			};

			await WriteRecordsAsync(records.Values, cancellationToken);
		}
		finally
		{
			_fileLock.Release();
		}
	}

	public async Task<IncidentAnalysisRecord?> GetByIncidentIdAsync(Guid incidentId, CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();

		await _fileLock.WaitAsync(cancellationToken);
		try
		{
			var records = await ReadRecordsAsync(cancellationToken);
			return records.TryGetValue(incidentId, out var record) ? record : null;
		}
		finally
		{
			_fileLock.Release();
		}
	}

	public async Task<IReadOnlyList<IncidentAnalysisRecord>> GetRecentAsync(int maxResults, CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();

		await _fileLock.WaitAsync(cancellationToken);
		try
		{
			var count = maxResults <= 0 ? 1 : maxResults;
			var records = (await ReadRecordsAsync(cancellationToken)).Values
				.OrderByDescending(record => record.CreatedAtUtc)
				.Take(count)
				.ToArray();

			return records;
		}
		finally
		{
			_fileLock.Release();
		}
	}

	public async Task SaveCandidatesAsync(IReadOnlyList<DetectedIncidentCandidate> candidates, MonitoringScanRecord scan, CancellationToken cancellationToken = default)
	{
		await _fileLock.WaitAsync(cancellationToken);
		try
		{
			var state = await ReadWorkflowStateAsync(cancellationToken);
			var records = await ReadRecordsAsync(cancellationToken);
			foreach (var candidate in candidates)
			{
				if (state.Candidates.Any(item => item.Id == candidate.Id)) continue;
				var probe = ToIncident(candidate);
				var active = records.Values.Where(record => record.Status is "new" or "active" or "mitigated").ToArray();
				var duplicate = active.FirstOrDefault(record => SameScope(probe, record.Incident));
				var similar = active.Select(record => ToSimilarMatch(probe, Tokenize(BuildIncidentText(probe)), record)).Where(match => match.Score >= 0.45).OrderByDescending(match => match.Score).Take(3).ToArray();
				state.Candidates.Add(candidate with
				{
					DuplicateIncidentId = duplicate?.Incident.Id,
					SimilarIncidents = similar,
					Timeline = [
						Event("scan started", $"Monitoring scan {scan.Id} started.", scan.StartedAtUtc),
						Event("candidate detected", $"Candidate detected from {candidate.Source}.", candidate.DetectedAtUtc, evidenceReference: string.Join(", ", candidate.Signals)),
						Event("scan completed", $"Monitoring scan {scan.Id} completed with {scan.CandidateCount} candidate(s).", scan.CompletedAtUtc)
					]
				});
			}
			state.Scans.Add(scan);
			state.Scans = state.Scans.OrderByDescending(item => item.CompletedAtUtc).Take(100).ToList();
			await WriteWorkflowStateAsync(state, cancellationToken);
		}
		finally { _fileLock.Release(); }
	}

	public async Task<IReadOnlyList<DetectedIncidentCandidate>> GetCandidatesAsync(CancellationToken cancellationToken = default)
	{
		await _fileLock.WaitAsync(cancellationToken);
		try { return (await ReadWorkflowStateAsync(cancellationToken)).Candidates.OrderByDescending(item => item.DetectedAtUtc).ToArray(); }
		finally { _fileLock.Release(); }
	}

	public async Task<MonitoringScanRecord?> GetLastScanAsync(CancellationToken cancellationToken = default)
	{
		await _fileLock.WaitAsync(cancellationToken);
		try { return (await ReadWorkflowStateAsync(cancellationToken)).Scans.OrderByDescending(item => item.CompletedAtUtc).FirstOrDefault(); }
		finally { _fileLock.Release(); }
	}

	public async Task<Incident> ConfirmCandidateAsync(string candidateId, CancellationToken cancellationToken = default)
	{
		await _fileLock.WaitAsync(cancellationToken);
		try
		{
			var state = await ReadWorkflowStateAsync(cancellationToken);
			var index = state.Candidates.FindIndex(item => item.Id == candidateId);
			if (index < 0) throw new KeyNotFoundException($"Candidate {candidateId} was not found.");
			var candidate = state.Candidates[index];
			if (candidate.Status != "candidate") throw new InvalidOperationException("Only undecided candidates can be confirmed.");
			var incident = ToIncident(candidate);
			var now = DateTimeOffset.UtcNow;
			var placeholder = new IncidentAnalysisResult { IncidentId = incident.Id, IncidentSummary = incident.Title, AnalysisText = "", AnalysisProvider = "pending", SessionId = "", Confidence = "Low" };
			var records = await ReadRecordsAsync(cancellationToken);
			records[incident.Id] = new IncidentAnalysisRecord
			{
				Incident = incident, AnalysisResult = placeholder, Status = "new", CreatedAtUtc = now, UpdatedAtUtc = now, CandidateId = candidate.Id,
				Timeline = candidate.Timeline.Concat([Event("incident created", "Incident created from candidate.", now), Event("incident confirmed", "Candidate confirmed by a human.", now, actor: "user"), Event("analysis started", "Evidence collection and analysis started.", now)]).ToArray()
			};
			state.Candidates[index] = candidate with { Status = "confirmed", Timeline = candidate.Timeline.Append(Event("incident confirmed", $"Confirmed as incident {incident.Id}.", now, actor: "user")).ToArray() };
			await WriteRecordsAsync(records.Values, cancellationToken);
			await WriteWorkflowStateAsync(state, cancellationToken);
			return incident;
		}
		finally { _fileLock.Release(); }
	}

	public async Task<DetectedIncidentCandidate> DecideCandidateAsync(string candidateId, string decision, Guid? mergeIntoIncidentId = null, CancellationToken cancellationToken = default)
	{
		var normalized = decision.Trim().ToLowerInvariant();
		if (normalized is not ("false_positive" or "ignored" or "merged")) throw new ArgumentException("Decision must be false_positive, ignored, or merged.", nameof(decision));
		await _fileLock.WaitAsync(cancellationToken);
		try
		{
			var state = await ReadWorkflowStateAsync(cancellationToken);
			var index = state.Candidates.FindIndex(item => item.Id == candidateId);
			if (index < 0) throw new KeyNotFoundException($"Candidate {candidateId} was not found.");
			var candidate = state.Candidates[index];
			if (candidate.Status != "candidate") throw new InvalidOperationException("Candidate already has a decision.");
			var now = DateTimeOffset.UtcNow;
			var eventType = normalized == "false_positive" ? "false positive" : normalized;
			if (normalized == "merged")
			{
				if (mergeIntoIncidentId is null) throw new ArgumentException("A target incident is required when merging.", nameof(mergeIntoIncidentId));
				var records = await ReadRecordsAsync(cancellationToken);
				if (!records.TryGetValue(mergeIntoIncidentId.Value, out var target)) throw new KeyNotFoundException($"Target incident {mergeIntoIncidentId} was not found.");
				records[target.Incident.Id] = target with { UpdatedAtUtc = now, Timeline = target.Timeline.Append(Event("merged", $"Candidate {candidate.Id} merged into this incident.", now, actor: "user", evidenceReference: string.Join(", ", candidate.Signals))).ToArray() };
				await WriteRecordsAsync(records.Values, cancellationToken);
			}
			var updated = candidate with { Status = normalized, Timeline = candidate.Timeline.Append(Event(eventType, normalized == "merged" ? $"Merged into incident {mergeIntoIncidentId}." : $"Candidate marked {normalized.Replace('_', ' ')}.", now, actor: "user")).ToArray() };
			state.Candidates[index] = updated;
			await WriteWorkflowStateAsync(state, cancellationToken);
			return updated;
		}
		finally { _fileLock.Release(); }
	}

	public async Task<string> UpdateStatusAsync(Guid incidentId, string status, CancellationToken cancellationToken = default)
	{
		var normalizedStatus = NormalizeIncidentStatus(status);

		await _fileLock.WaitAsync(cancellationToken);
		try
		{
			var records = await ReadRecordsAsync(cancellationToken);
			if (!records.TryGetValue(incidentId, out var record))
			{
				throw new KeyNotFoundException($"Incident record {incidentId} was not found.");
			}

			var now = DateTimeOffset.UtcNow;
			var eventType = normalizedStatus switch { "mitigated" => "mitigated", "resolved" => "resolved", "active" when record.Status == "resolved" => "reopened", "active" => "work started", _ => normalizedStatus };
			var proposal = record.ProposedKnowledgeUpdate;
			var timeline = record.Timeline.Append(Event(eventType, $"Incident status changed from {record.Status} to {normalizedStatus}.", now, actor: "user")).ToList();
			if (normalizedStatus == "resolved" && proposal is null)
			{
				proposal = BuildKnowledgeUpdate(record, now);
				timeline.Add(Event("runbook update generated", "A proposed knowledge update was generated for human review.", now));
			}
			records[incidentId] = record with { Status = normalizedStatus, UpdatedAtUtc = now, ProposedKnowledgeUpdate = proposal, Timeline = timeline };
			await WriteRecordsAsync(records.Values, cancellationToken);
			return normalizedStatus;
		}
		finally
		{
			_fileLock.Release();
		}
	}

	public async Task<IncidentAnalysisRecord> AddTimelineEventAsync(Guid incidentId, IncidentTimelineEvent timelineEvent, CancellationToken cancellationToken = default)
	{
		await _fileLock.WaitAsync(cancellationToken);
		try
		{
			var records = await ReadRecordsAsync(cancellationToken);
			if (!records.TryGetValue(incidentId, out var record)) throw new KeyNotFoundException($"Incident record {incidentId} was not found.");
			var updated = record with { UpdatedAtUtc = timelineEvent.OccurredAtUtc, Timeline = record.Timeline.Append(timelineEvent).ToArray() };
			records[incidentId] = updated;
			await WriteRecordsAsync(records.Values, cancellationToken);
			return updated;
		}
		finally { _fileLock.Release(); }
	}

	public async Task<ProposedKnowledgeUpdate> ReviewKnowledgeUpdateAsync(Guid incidentId, string decision, string? content, string? notes, CancellationToken cancellationToken = default)
	{
		var normalized = decision.Trim().ToLowerInvariant();
		if (normalized is not ("approved" or "rejected")) throw new ArgumentException("Decision must be approved or rejected.", nameof(decision));
		await _fileLock.WaitAsync(cancellationToken);
		try
		{
			var records = await ReadRecordsAsync(cancellationToken);
			if (!records.TryGetValue(incidentId, out var record)) throw new KeyNotFoundException($"Incident record {incidentId} was not found.");
			if (record.ProposedKnowledgeUpdate is null) throw new InvalidOperationException("No proposed knowledge update exists.");
			var now = DateTimeOffset.UtcNow;
			var updatedProposal = record.ProposedKnowledgeUpdate with { Status = normalized, Content = string.IsNullOrWhiteSpace(content) ? record.ProposedKnowledgeUpdate.Content : content.Trim(), ReviewNotes = notes?.Trim(), ReviewedAtUtc = now };
			if (_approvedKnowledgePublisher is not null && normalized == "rejected")
			{
				await _approvedKnowledgePublisher.RemoveAsync(updatedProposal.Id, cancellationToken);
			}
			records[incidentId] = record with { ProposedKnowledgeUpdate = updatedProposal, UpdatedAtUtc = now, Timeline = record.Timeline.Append(Event($"runbook update {normalized}", $"Proposed knowledge update {normalized} by a human.", now, actor: "user")).ToArray() };
			await WriteRecordsAsync(records.Values, cancellationToken);
			if (_approvedKnowledgePublisher is not null && normalized == "approved")
			{
				await _approvedKnowledgePublisher.PublishAsync(updatedProposal.Id, updatedProposal.Title, updatedProposal.Content, cancellationToken);
			}
			return updatedProposal;
		}
		finally { _fileLock.Release(); }
	}

	public async Task<bool> DeleteAsync(Guid incidentId, CancellationToken cancellationToken = default)
	{
		await _fileLock.WaitAsync(cancellationToken);
		try
		{
			var records = await ReadRecordsAsync(cancellationToken);
			if (!records.Remove(incidentId, out var deletedRecord))
			{
				return false;
			}
			if (_approvedKnowledgePublisher is not null && deletedRecord.ProposedKnowledgeUpdate is { } proposal)
			{
				await _approvedKnowledgePublisher.RemoveAsync(proposal.Id, cancellationToken);
			}

			await WriteRecordsAsync(records.Values, cancellationToken);
			var workflow = await ReadWorkflowStateAsync(cancellationToken);
			workflow.Candidates = workflow.Candidates.Select(candidate => candidate with
			{
				DuplicateIncidentId = candidate.DuplicateIncidentId == incidentId ? null : candidate.DuplicateIncidentId,
				SimilarIncidents = candidate.SimilarIncidents.Where(item => item.IncidentId != incidentId).ToArray()
			}).ToList();
			await WriteWorkflowStateAsync(workflow, cancellationToken);
			return true;
		}
		finally
		{
			_fileLock.Release();
		}
	}

	public async Task<IncidentActionOutcome> AddActionOutcomeAsync(
		Guid incidentId,
		string description,
		string status,
		CancellationToken cancellationToken = default)
	{
		if (string.IsNullOrWhiteSpace(description))
		{
			throw new ArgumentException("Action outcome description is required.", nameof(description));
		}

		var normalizedStatus = NormalizeOutcomeStatus(status);
		var outcomeId = Guid.NewGuid();
		var outcome = new IncidentActionOutcome
		{
			Id = outcomeId,
			Description = description.Trim(),
			Status = normalizedStatus,
			LoggedAtUtc = DateTimeOffset.UtcNow,
			EvidenceReference = $"action:{outcomeId:N}"
		};

		await _fileLock.WaitAsync(cancellationToken);
		try
		{
			var records = await ReadRecordsAsync(cancellationToken);
			if (!records.TryGetValue(incidentId, out var record))
			{
				throw new KeyNotFoundException($"Incident record {incidentId} was not found.");
			}

			var outcomes = record.AnalysisResult.ActionOutcomes
				.Append(outcome)
				.OrderByDescending(item => item.LoggedAtUtc)
				.Take(20)
				.OrderBy(item => item.LoggedAtUtc)
				.ToArray();

			records[incidentId] = record with
			{
				AnalysisResult = record.AnalysisResult with { ActionOutcomes = outcomes },
				UpdatedAtUtc = outcome.LoggedAtUtc,
				Timeline = record.Timeline.Append(Event("action recorded", $"Action outcome recorded as {outcome.Status}: {outcome.Description}", outcome.LoggedAtUtc, actor: "user", evidenceReference: outcome.EvidenceReference)).ToArray()
			};

			await WriteRecordsAsync(records.Values, cancellationToken);
			return outcome;
		}
		finally
		{
			_fileLock.Release();
		}
	}

	public async Task<IncidentAnalysisFeedback> AddFeedbackAsync(Guid incidentId, IncidentAnalysisFeedback feedback, CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(feedback);
		await _fileLock.WaitAsync(cancellationToken);
		try
		{
			var records = await ReadRecordsAsync(cancellationToken);
			if (!records.TryGetValue(incidentId, out var record)) throw new KeyNotFoundException($"Incident record {incidentId} was not found.");
			var updated = record with
			{
				Feedback = record.Feedback.Append(feedback).TakeLast(50).ToArray(),
				UpdatedAtUtc = feedback.SubmittedAtUtc,
				Timeline = record.Timeline.Append(Event("analysis feedback recorded", $"Analysis feedback recorded as {feedback.AnalysisUsefulness}; recommendations rated {feedback.RecommendationCorrectness}.", feedback.SubmittedAtUtc, actor: "user")).ToArray()
			};
			records[incidentId] = updated;
			await WriteRecordsAsync(records.Values, cancellationToken);
			return feedback;
		}
		finally { _fileLock.Release(); }
	}

	public async Task<IReadOnlyList<SimilarIncidentMatch>> FindSimilarAsync(
		Incident incident,
		int maxResults,
		CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(incident);
		cancellationToken.ThrowIfCancellationRequested();

		await _fileLock.WaitAsync(cancellationToken);
		try
		{
			var count = Math.Clamp(maxResults <= 0 ? 3 : maxResults, 1, 10);
			var queryTokens = Tokenize(BuildIncidentText(incident));
			var records = (await ReadRecordsAsync(cancellationToken)).Values
				.Where(record => record.Incident.Id != incident.Id)
				.Where(IsReusableKnowledge)
				.Select(record => ToSimilarMatch(incident, queryTokens, record))
				.Where(match => match.Score >= 0.18)
				.OrderByDescending(match => match.Score)
				.ThenByDescending(match => match.CreatedAtUtc)
				.Take(count)
				.ToArray();

			return records;
		}
		finally
		{
			_fileLock.Release();
		}
	}

	private async Task<Dictionary<Guid, IncidentAnalysisRecord>> ReadRecordsAsync(CancellationToken cancellationToken)
	{
		if (!File.Exists(_filePath))
		{
			return new Dictionary<Guid, IncidentAnalysisRecord>();
		}

		await using var stream = File.Open(_filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
		var records = await JsonSerializer.DeserializeAsync<List<IncidentAnalysisRecord>>(stream, SerializerOptions, cancellationToken)
			?? [];

		return records.Select(MigrateLegacyRecord).ToDictionary(record => record.Incident.Id, record => record);
	}

	private static IncidentAnalysisRecord MigrateLegacyRecord(IncidentAnalysisRecord record)
	{
		if (record.UpdatedAtUtc != default) return record;
		var severity = record.Incident.Severity switch
		{
			(IncidentSeverity)4 => IncidentSeverity.Sev1,
			(IncidentSeverity)3 => IncidentSeverity.Sev2,
			(IncidentSeverity)2 => IncidentSeverity.Sev3,
			(IncidentSeverity)1 => IncidentSeverity.Sev4,
			_ => IncidentSeverity.Sev5
		};
		var incident = new Incident(record.Incident.Id, record.Incident.Title, record.Incident.Description, severity, record.Incident.ServiceName, record.Incident.Environment, record.Incident.Timestamp, record.Incident.Tags);
		return record with { Incident = incident, UpdatedAtUtc = record.CreatedAtUtc, Timeline = [Event("incident created", "Migrated legacy analyzed incident.", record.CreatedAtUtc)] };
	}

	private async Task WriteRecordsAsync(IEnumerable<IncidentAnalysisRecord> records, CancellationToken cancellationToken)
	{
		await using var stream = File.Open(_filePath, FileMode.Create, FileAccess.Write, FileShare.None);
		await JsonSerializer.SerializeAsync(stream, records, SerializerOptions, cancellationToken);
	}

	private async Task<WorkflowState> ReadWorkflowStateAsync(CancellationToken cancellationToken)
	{
		if (!File.Exists(_workflowFilePath)) return new WorkflowState();
		await using var stream = File.Open(_workflowFilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
		return await JsonSerializer.DeserializeAsync<WorkflowState>(stream, SerializerOptions, cancellationToken) ?? new WorkflowState();
	}

	private async Task WriteWorkflowStateAsync(WorkflowState state, CancellationToken cancellationToken)
	{
		await using var stream = File.Open(_workflowFilePath, FileMode.Create, FileAccess.Write, FileShare.None);
		await JsonSerializer.SerializeAsync(stream, state, SerializerOptions, cancellationToken);
	}

	private static SimilarIncidentMatch ToSimilarMatch(
		Incident incident,
		HashSet<string> queryTokens,
		IncidentAnalysisRecord record)
	{
		var candidateTokens = Tokenize(BuildIncidentText(record.Incident, record.AnalysisResult));
		var shared = queryTokens.Intersect(candidateTokens, StringComparer.OrdinalIgnoreCase)
			.Order(StringComparer.OrdinalIgnoreCase)
			.Take(12)
			.ToArray();

		var score = queryTokens.Count == 0
			? 0d
			: shared.Length / (double)queryTokens.Count;

		if (!string.IsNullOrWhiteSpace(incident.ServiceName) &&
		    string.Equals(incident.ServiceName, record.Incident.ServiceName, StringComparison.OrdinalIgnoreCase))
		{
			score += 0.35;
		}

		if (!string.IsNullOrWhiteSpace(incident.Environment) &&
		    string.Equals(incident.Environment, record.Incident.Environment, StringComparison.OrdinalIgnoreCase))
		{
			score += 0.1;
		}

		if (incident.Severity == record.Incident.Severity)
		{
			score += 0.05;
		}

		return new SimilarIncidentMatch
		{
			IncidentId = record.Incident.Id,
			IncidentSummary = record.AnalysisResult.IncidentSummary,
			ServiceName = record.Incident.ServiceName ?? "unknown service",
			Environment = record.Incident.Environment ?? "unknown environment",
			ResolutionSummary = BuildResolutionSummary(record.AnalysisResult),
			Score = Math.Round(Math.Min(score, 1), 4),
			CreatedAtUtc = record.CreatedAtUtc,
			SharedSignals = shared,
			SuccessfulActions = record.AnalysisResult.ActionOutcomes.Where(item => item.Status is "worked" or "partial").Select(item => item.Description).ToArray(),
			FailedActions = record.AnalysisResult.ActionOutcomes.Where(item => item.Status == "failed").Select(item => item.Description).ToArray()
		};
	}

	private static string BuildIncidentText(Incident incident, IncidentAnalysisResult? analysis = null)
	{
		var parts = new[]
		{
			incident.Title,
			incident.Description,
			incident.ServiceName,
			incident.Environment,
			string.Join(' ', incident.Tags),
			analysis?.IncidentSummary,
			analysis?.Notes,
			string.Join(' ', analysis?.RecommendedActions.Select(action => action.Description) ?? Array.Empty<string>()),
			string.Join(' ', analysis?.ActionOutcomes.Select(outcome => $"{outcome.Status} {outcome.Description}") ?? Array.Empty<string>())
		};

		return string.Join(' ', parts.Where(part => !string.IsNullOrWhiteSpace(part)));
	}

	private static string BuildResolutionSummary(IncidentAnalysisResult analysis)
	{
		var firstAction = analysis.RecommendedActions.FirstOrDefault()?.Description;
		var workedOutcome = analysis.ActionOutcomes
			.LastOrDefault(outcome => string.Equals(outcome.Status, "worked", StringComparison.OrdinalIgnoreCase));
		if (workedOutcome is not null)
		{
			return $"Worked: {workedOutcome.Description}";
		}

		if (!string.IsNullOrWhiteSpace(firstAction))
		{
			return firstAction;
		}

		return string.IsNullOrWhiteSpace(analysis.Notes) ? analysis.IncidentSummary : analysis.Notes;
	}

	private static bool IsReusableKnowledge(IncidentAnalysisRecord record) =>
		record.Status == "resolved" && record.ProposedKnowledgeUpdate?.Status == "approved";

	private static bool SameScope(Incident left, Incident right) =>
		string.Equals(left.ServiceName, right.ServiceName, StringComparison.OrdinalIgnoreCase) &&
		string.Equals(left.Environment, right.Environment, StringComparison.OrdinalIgnoreCase) &&
		Tokenize(BuildIncidentText(left)).Intersect(Tokenize(BuildIncidentText(right)), StringComparer.OrdinalIgnoreCase).Count() >= 2;

	private static Incident ToIncident(DetectedIncidentCandidate candidate) => new(
		Guid.NewGuid(), candidate.Title, candidate.Description, candidate.Severity, candidate.ServiceName, candidate.Environment, candidate.DetectedAtUtc, candidate.SuggestedTags);

	private static IncidentTimelineEvent Event(string type, string summary, DateTimeOffset occurredAtUtc, string actor = "system", string? evidenceReference = null) => new()
	{
		Type = type, Summary = summary, OccurredAtUtc = occurredAtUtc, Actor = actor, EvidenceReference = evidenceReference
	};

	private static ProposedKnowledgeUpdate BuildKnowledgeUpdate(IncidentAnalysisRecord record, DateTimeOffset now)
	{
		var evidence = record.AnalysisResult.Evidence.Take(5).Select(item => $"- {item.Source}: {item.Summary}");
		var actions = record.AnalysisResult.ActionOutcomes.Count == 0
			? ["- No action outcome was recorded; review before approval."]
			: record.AnalysisResult.ActionOutcomes.Select(item => $"- [{item.Status}] {item.Description} ({item.EvidenceReference})");
		var futureSteps = record.AnalysisResult.RecommendedActions.Count == 0
			? ["- No evidence-grounded future step was generated."]
			: record.AnalysisResult.RecommendedActions.Take(5).Select(item => $"- {item.Description} (Evidence: {string.Join(", ", item.SupportingSignals)})");
		var severity = record.Incident.Severity switch
		{
			IncidentSeverity.Sev1 => "SEV-1", IncidentSeverity.Sev2 => "SEV-2", IncidentSeverity.Sev3 => "SEV-3",
			IncidentSeverity.Sev4 => "SEV-4", IncidentSeverity.Sev5 => "SEV-5", _ => "unknown"
		};
		return new ProposedKnowledgeUpdate
		{
			Title = $"Learning from {record.Incident.Title}", GeneratedAtUtc = now,
			Content = string.Join(Environment.NewLine,
				new[]
				{
					$"# {record.Incident.Title}", "", "## Incident context",
					$"- What happened: {record.Incident.Description}", $"- Severity: {severity}",
					$"- Service: {record.Incident.ServiceName ?? "unknown"}", $"- Environment: {record.Incident.Environment ?? "unknown"}",
					"", "## Grounded evidence"
				}.Concat(evidence)
				.Concat(["", "## Actions tried"])
				.Concat(actions)
				.Concat(["", "## Recommended future steps"])
				.Concat(futureSteps))
		};
	}

	private static HashSet<string> Tokenize(string value)
	{
		return Regex.Matches(value.ToLowerInvariant(), "[a-z0-9]+")
			.Select(match => match.Value)
			.Where(token => token.Length > 2)
			.ToHashSet(StringComparer.OrdinalIgnoreCase);
	}

	private static string NormalizeOutcomeStatus(string status)
	{
		var normalized = string.IsNullOrWhiteSpace(status) ? "worked" : status.Trim().ToLowerInvariant();
		return normalized is "worked" or "partial" or "failed" ? normalized : "worked";
	}

	private static string NormalizeIncidentStatus(string status)
	{
		var normalized = string.IsNullOrWhiteSpace(status) ? "active" : status.Trim().ToLowerInvariant();
		if (normalized is "ack" or "acknowledged")
		{
			return "active";
		}

		return normalized is "new" or "active" or "mitigated" or "resolved" ? normalized : "active";
	}

	private sealed class WorkflowState
	{
		public List<DetectedIncidentCandidate> Candidates { get; set; } = [];
		public List<MonitoringScanRecord> Scans { get; set; } = [];
	}
}
