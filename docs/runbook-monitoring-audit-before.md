# Runbook, monitoring, ingestion, and evaluation audit (before source-management work)

Date: 2026-06-20

## Runbooks

- The bundled source of truth is Markdown under `IncidentResponseAgent.Infrastructure/Runbooks/KnowledgeBase`. The project copies those files to the application output.
- `Runbooks:SemanticRetrieval:KnowledgeBasePath` can point at one local or mounted directory. When it is empty, the service resolves an output-relative `Runbooks/KnowledgeBase` directory.
- There is no upload flow or external documentation connector. A local Git working tree can be used only indirectly by configuring its directory; Git state and synchronization are not managed.
- `MarkdownRunbookChunker` splits documents on heading boundaries, numbered steps, horizontal rules, and code-fence boundaries. Each chunk carries its heading path and searchable document context.
- `SemanticRunbookRetrievalService` embeds chunks, stores document/chunk metadata and vectors in SQLite, and optionally mirrors vectors to Qdrant.
- Re-indexing is content-aware. A source fingerprint watches Markdown path, length, and last-write time; the index stores a SHA-256 content hash plus embedding provider/model. Changed documents are re-embedded and missing documents are deleted from SQLite and Qdrant.
- Approved incident-learning drafts are written as `approved-{proposalId}.md` by `MarkdownApprovedKnowledgePublisher`. Rejected drafts are not written; deleting an incident removes its published approved file.
- An approved file becomes searchable on the next retrieval because the source fingerprint changes. Existing tests verify add/remove re-indexing.
- The UI exposes RAG result source paths and approved filenames, but before this work it had no source registry, source health/sync status, or source-management flow.

## Monitoring and telemetry

- Logs and metrics are read from configurable JSON files by `LocalJsonLogSearchProvider` and `LocalJsonMetricsProvider`.
- HTTP ingestion persists into those same files, so manual signals enter the same detection and analysis pipeline.
- Detection is deterministic: metric thresholds and repeated log patterns create candidate incidents. AI is not the detector.
- Scans are server endpoints invoked by the browser. There is no hosted server-side polling loop; closing the browser stops scheduled scans.
- Last scan and candidates are persisted by `FileIncidentRecordStore`. A page read does not itself create a scan.
- Candidate IDs include signal timestamps/values. Store-level duplicate/similar matching prevents some duplicate incident behavior, but repeated changing samples can still create new candidate IDs.
- Recovery is observable in telemetry but does not currently add a recovery timeline event or automatically change incident state.

## Signal Ingestion

- The tab is a real persistence and source-validation tool, not UI-only state.
- Validation errors use API problem responses; accepted values preserve timestamps and source/service attribution.
- Its purpose is ambiguous in the existing copy. It should be presented as diagnostics/manual ingestion, not as a primary production connector.

## Evaluation

- The application has a rubric evaluator and two deterministic scenario definitions.
- The API only lists scenarios. It does not execute a complete evaluation campaign or write JSON/Markdown reports.
- Existing Playwright suites cover core, monitoring, AI/RAG, and learning-loop behavior with deterministic fixtures, but there is no aggregate metrics report or before/after comparison artifact.

## Gaps this work targets

1. Persisted runbook source registry with directory and local Git-working-tree sources.
2. Reachability checks, explicit synchronization, indexed counts, failures, enable/disable, and removal.
3. Version metadata for approved knowledge.
4. A separate external telemetry producer and an exercised connection to the JSON adapters.
5. Server-owned monitoring state and polling.
6. A 12-scenario evaluation runner with machine-readable and Markdown output.
