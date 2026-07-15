# Incident workflow architecture

## Audit of the previous implementation

| Area | Previous behavior | Assessment |
|---|---|---|
| Incident lifecycle | A detected signal was copied into the analysis form. The incident record was created only after analysis. Status was a string (`new`, `active`, `mitigated`, `resolved`). | Partial and untrustworthy: no candidate decision or transition history. |
| Monitoring | Polling read local JSON logs and metrics. Error-rate and queue-depth thresholds existed; any matching log group could create a signal. Scan state lived in browser local storage. | Partial: deterministic, but log thresholds and persisted scans were missing. |
| Analysis | Runbooks, logs, metrics, and token-similar incidents were gathered in parallel and passed to a model/fallback analyzer. | Implemented, but model-provided evidence and claims were accepted without validating their references. |
| RAG | Markdown runbooks were chunked and embedded into SQLite, with optional Qdrant and local fallback. | Implemented for bundled/configured runbooks. No approval-gated incident learning. |
| Runbooks | Static Markdown knowledge was immediately reusable. | Implemented for curated files; generated updates and review were missing. |
| Action outcomes | `worked`, `partial`, and `failed` outcomes were stored inside an analysis result. | Partial: no timeline entry, evidence reference, approval boundary, or safe reuse rule. |
| Persistence | Analyses used JSON; analysis sessions and runbook embeddings used SQLite. Candidates and scans were ephemeral. | Partial. |
| Similarity | Token overlap plus service, environment, and severity boosts searched every saved analysis. | Unsafe: unresolved or unreviewed records could influence analysis. |
| Deletion | Deleting a JSON incident removed it from the only incident similarity source. | Implemented, with UI confirmation. |

## New architecture and workflow

1. Configured log and metric sources are scanned by deterministic rules.
2. Each scan and newly observed candidate are persisted in the incident workflow sidecar.
3. Duplicate/similar active incidents are computed before a decision and shown with the candidate.
4. A human confirms, ignores, marks false positive, or merges the candidate.
5. Confirmation creates an active incident and immutable-style timeline entries before analysis begins.
6. Analysis gathers incident fields, log entries, metric samples, runbook matches, and only approved resolved incident learnings.
7. Model evidence, hypotheses, and actions survive parsing only when their source references exist in the collected evidence registry. The model is never used as a detector.
8. Responders record action outcomes, mitigate/resolve/reopen incidents, and retain truthful event timestamps.
9. Resolution generates a proposed Markdown knowledge update from grounded evidence and observed outcomes.
10. A human may edit and approve or reject the proposal. Only resolved incidents with approved proposals participate in future similarity and action-outcome reuse.
11. Deletion removes the source record used by local similarity retrieval, so deleted incidents cannot be returned.

## Schema changes

`IncidentSeverity` is now `Sev1` through `Sev5` (serialized as `sev1` through `sev5` by the API).

`IncidentAnalysisRecord` now stores `UpdatedAtUtc`, `CandidateId`, `MergedIntoIncidentId`, `Timeline`, and `ProposedKnowledgeUpdate` in addition to the incident, analysis, status, and creation time.

`IncidentActionOutcome` now has a stable ID and evidence reference. `DetectedIncidentCandidate` now stores decision status, duplicate/similar matches, and timeline. `MonitoringScanRecord` persists scan timing and result count. `ProposedKnowledgeUpdate` persists generated content, review state, review time, and reviewer notes.

The existing `incident-records.json` remains the incident aggregate file. A sibling `incident-records-workflow.json` (or default `incident-workflow.json`) stores candidates and scan history. SQLite session and runbook schemas are unchanged.

## Detection rules

- Request error rate: configured warning and critical thresholds.
- Latency: configured warning and critical millisecond thresholds for metric names containing `latency`.
- Health/repeated failures: configured thresholds for metric names containing `health` or `failure`.
- Queue backlog: configured queue-depth thresholds.
- Logs: matching error/warning/timeout/latency/backlog/failure/500 entries must meet `LogPatternCountThreshold` per source.
- Manual: a user-entered candidate has source `manual trigger` and must still be confirmed.

## Migration requirements

No database migration command is required. Existing JSON records are migrated on read: legacy numeric severities map `Critical -> Sev1`, `High -> Sev2`, `Medium -> Sev3`, and `Low -> Sev4`; legacy `new` records become active and receive a migration timeline event. The next write persists the new shape.

Back up the incident JSON before first production rollout. Existing incidents are deliberately not reusable learning until they are resolved and their generated proposal is explicitly approved. Deploy the API and static assets together because severity and candidate contracts changed.

## Remaining gaps

- Source connectors are still JSON/HTTP ingestion rather than production vendor integrations or a durable queue.
- Local similarity is lexical rather than a dedicated incident embedding index; approved runbooks retain the existing semantic index.
- The JSON aggregate uses a process-local lock and is appropriate for one API instance, not horizontally scaled writers.
- Authentication, roles, reviewer identity, audit signatures, retention policy, and tenant isolation are not implemented.
- Generated approved proposals remain incident knowledge records; promoting them into the curated Markdown runbook index is an explicit future publishing step.
- Thresholds can be scoped per project, but per-service rule management and anomaly baselines remain future work.
- Responder assignment, on-call escalation, incident roles, stakeholder communications, and postmortem task tracking are not implemented. Until they are, this is an investigation/workflow assistant rather than a complete incident-management platform.

## Analysis trust and feedback additions

Analysis responses now expose known facts with evidence references, hypotheses with validated references, explicit unknowns, exact runbook matches, deterministic quality scores, similar incidents with real metadata, and prior successful/failed action outcomes. Recommendations without a valid reference to collected evidence are removed before persistence or display.

Provider transparency is stored with every analysis: model provider/model, embedding provider, vector store actually used for the query, RAG status, model fallback status, and degraded-mode reason. A RAG exception produces an empty degraded retrieval result and model analysis continues. It does not trigger local analysis; the resilient model agent controls model fallback independently.

Responders can persist analysis usefulness, recommendation correctness, reason tags, an optional recommendation reference, and comments. Feedback is append-only within the incident aggregate and receives a timeline event. The UI control inventory and current manual-test status live in `docs/ui-functionality-matrix.md`.
