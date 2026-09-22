# Repository reliability and UX pass — September 16, 2026

Implementation and deterministic verification completed in the existing working tree. Existing user changes were preserved; no commit, deployment, Auth0 permission change, or normal-app fallback enablement was performed in this pass. The complete [112-case manual walkthrough](manual-qa.md) is a separate deliverable, not a claim that all 112 cases were manually executed.

## Findings and changes

| Area | Finding and resulting behavior |
| --- | --- |
| Structured model output | A requested schema is not proof that a provider returned valid JSON. The malformed-output retry previously dropped strict response formatting; framework deserialization failures could escape before the normal validation path. The retry now retains the strict schema, handles JSON failures through a typed clean error, and remains bounded to one retry. A provider rejecting schema transport with HTTP 400/422 gets a deliberately disclosed prompt-schema attempt. Arbitrary prose is no longer trimmed into an apparent JSON object. |
| Hidden fallback | `AnalyzeIncidentUseCase` could synthesize deterministic structured output when parsing returned null even if the agent had not used fallback. It now fails explicitly unless the agent actually selected the configured fallback branch. The normal application's local fallback stays disabled. |
| Error reporting | Raw .NET JSON parser details stay in server diagnostics. Users receive a readable failure and can retry; failed confirmations preserve their incident. Empty/rate-limited failures no longer claim local analysis ran when it did not. Retry controls prevent duplicate clicks. |
| Generated knowledge | Resolving creates the proposal under the existing rules; the earlier UI buried it in detail/history without a strong next step. Resolution now reveals it and pending proposals have queue-level review buttons. Re-resolving preserves an existing proposal rather than silently replacing a review. |
| Publication reliability | Previously approval could be saved before Markdown publication. Publication is now attempted before approval persistence; an I/O failure leaves the proposal pending with a stored failure reason and supports retry. Markdown uses a temporary file and atomic replacement. A later incident-save failure attempts compensation. This is not a crash-proof distributed transaction. |
| Publication vs retrieval | The UI separates pending, rejected, approved-but-unverified historical records, and successfully published Markdown. It offers a RAG search to verify retrieval and explicitly says indexing can fail or be delayed. It never labels mere approval as indexed. Learning tests verify that approved Markdown is subsequently retrieved and rejected/deleted knowledge is excluded. |
| Identity | Claims-based friendly names are displayed; email addresses and opaque subject IDs are not turned into guessed names. Stable subject IDs and historical audit identifiers are retained. Previously stored audit names can resolve through the authenticated responder directory. Unknown identities fall back to “Responder.” |
| Lifecycle | Mitigated cyan and resolved green are distinct throughout counters, rows, detail, filters, timeline, and charts. Confirmed uses purple, candidates blue, work in progress amber. An older counter selector that overrode the palette was found visually and fixed. Severity badges no longer have a decorative marker. |
| Diagnostics | Missing/invalid diagnostic values are omitted or explicitly unavailable; actual zero-duration values remain valid. Timeout source labels are readable. Embedding failure no longer implies local embeddings ran. A missing session shows an explanation instead of empty copy/continue controls. |
| Source management | Source rows now render through React with escaped text, busy controls, and persistent errors. An HTTP 200 sync response containing `lastError` or an unreachable source cannot show success. Source section counts are labeled as source sections, not falsely as indexed sections. Unreadable Markdown and corrupt monitor state now produce meaningful diagnostics instead of silent catch behavior. |
| Other UX | Notifications can be dismissed. Monitor state follows actual polling state rather than the count of reachable sources. Empty evaluation catalogs stay empty; guessed scenarios and title-derived metadata were removed. Tab-loading failures display errors. Keyboard focus, narrow layouts, and both themes remain supported. |

The original historical provider response was not retained in this checkout, so the exact bytes that produced the reported error cannot be attributed to a particular live model call. The affected request/parse/retry paths were inspected and malformed JSON was reproduced with deterministic provider responses. No malformed text is repaired into fabricated evidence.

## Architecture and cleanup

- Removed `frontend/src/shell.html` and its raw-HTML mounting path. `ConsoleLayout.jsx` renders normal JSX.
- Added React `DashboardOverview`, `KnowledgeProposal`/editor/actions, `DetailEnhancements`, and `RunbookSources`/actions. Small external stores provide a transitional bridge to existing controller workflows; React owns these components' rendering and local editing/busy/error state.
- Extracted `api-client.js`, `presentation.js`, `diagnostics.js`, `identity.js`, and `analytics.js`. API handling retains CSRF and session-expiry behavior.
- Removed definition-only helpers (`projectBadge`, `renderHypothesisBlock`, `renderRunbookSteps`, inferred scenario/severity/service/environment helpers), the obsolete knowledge-review delegated handler, the old source HTML renderer, and unused `.stat-*` styling after checking references.
- Retained deliberate fallback interfaces, test seams, serial provider evaluation, and working controller flows. The remaining controller is still substantial; this is an incremental migration, not a claim of a complete React rewrite.
- No chart dependency or new runtime package was needed. The compact labeled bar lists show creation dates, severity, and lifecycle for the scoped recent-record window, with honest empty states and missing-timestamp handling. They do not imply all-time totals or unsupported timing/source metrics.

## Verification

Commands run from the repository root (Playwright retries disabled):

```powershell
dotnet build IncidentResponseAgent.slnx --artifacts-path .tmp/auth-build --no-restore --verbosity quiet
dotnet test IncidentResponseAgent.Tests/IncidentResponseAgent.Tests.csproj --artifacts-path .tmp/auth-build --no-restore --verbosity quiet
node --test frontend/tests/presentation.test.mjs
npm.cmd run build
dotnet publish IncidentResponseAgent.Api/IncidentResponseAgent.Api.csproj --artifacts-path .tmp/auth-build -o .tmp/e2e-app --no-restore --verbosity quiet
npx.cmd playwright test --retries=0
npx.cmd playwright test -c playwright.ai.config.ts --retries=0
npx.cmd playwright test -c playwright.learning.config.ts --retries=0
npx.cmd playwright test -c playwright.batched-rag.config.ts --retries=0
npx.cmd playwright test -c playwright.autoscan.config.ts --retries=0
node node_modules/react-doctor/dist/cli.js --json --blocking none --yes --scope full --no-telemetry --no-supply-chain --json-out .tmp/reliability-pass/doctor-final.json
node node_modules/react-doctor/dist/cli.js --json --blocking none --yes --scope changed --base HEAD --include-untracked --no-telemetry --no-supply-chain --json-out .tmp/reliability-pass/doctor-changed.json
git diff --check
```

| Check | Result |
| --- | --- |
| Solution build | Passed, 0 warnings / 0 errors |
| Backend tests | 123 passed |
| Node presentation tests | 5 passed |
| Vite production build and API publish | Passed |
| Main Playwright suite | 27 passed: authentication, roles, core workflows, projects, monitoring/ingestion/detection, lifecycle, charts, source errors, accessibility, mobile |
| AI/RAG browser suite | 11 passed: strict structured response, empty/malformed retry, timeouts, unavailable provider, retrieval and fallback disclosure |
| Learning browser suite | 5 passed: ratings/outcomes, editing, approval/publication, subsequent retrieval, exclusions, linked sessions |
| Batched retrieval/fallback-disabled suite | 3 passed: batched embedding, quota failure/recovery, failed incident preservation with no local analysis |
| Autonomous monitoring suite | 1 passed: scanning without a browser, pause/resume, deduplication |
| Accessibility | Axe/contrast checks passed on both themes and populated detail; keyboard focus trapping/restoration and mobile overflow checks passed |
| Diff whitespace check | Passed; Git reports ordinary LF/CRLF normalization notices |

The 47 browser tests use isolated deterministic providers/data, not a live LLM. The AI/learning fixture directory must be reset between independent campaigns because published learning documents and linked sessions intentionally persist. `node e2e/scripts/prepare.mjs` was used for initial fixture preparation; subsequent learning rerun reset only the verified `.tmp/e2e-ai` path and copied `e2e/fixtures/ai` inputs before running it. A warm-index batch assertion and duplicate-title selector were also corrected: the batch suite now gets a unique index path and the lifecycle test a unique title. Initial failures are not counted as passes; the table reports the successful reruns after diagnosis.

New backend coverage checks name-claim precedence, bounded malformed-response retry with strict schema retained, prohibition of implicit local fallback, and publication failure/retry. Frontend regressions check diagnostics, identities, empty/populated analytics, rendered lifecycle colors, severity markers, proposal discovery, real evaluation catalogs, and escaped source text/error handling. Existing workflow suites exercise the refactored components.

In-app browser inspection on the isolated manual QA server covered empty/populated charts in light and dark themes, manual creation, active → mitigated → resolved transitions, pending-proposal discovery, the source inventory, and a 390px layout. No captured console errors/warnings appeared during that walkthrough. The QA harness was started successfully with local analysis fallback disabled. Its synthetic data stays under `.tmp/manual-qa`.

## React Doctor and boundaries

React Doctor 0.9.14 was run locally with telemetry and remote supply-chain checks disabled. Baseline full scan: 0 errors, 8 warnings. Final full snapshot: 0 errors, 10 warnings—7 in generated React/Playwright bundles, 1 intentional serial evaluation loop, and 2 suggestions to use native `<dialog>`. The generated-artifact count varies as build/test output accumulates, so raw warning totals are not a source-quality score. The dynamic source HTML sink, repeated membership checks, and new component complexity finding were removed. The changed-source scan reports two new dialog suggestions and three fixed diagnostics relative to HEAD.

The existing dialogs retain explicit focus trapping, Escape handling, background isolation, and focus restoration, all exercised by browser tests. They were not rewritten merely to silence a preference rule. Generated React element identifiers are not application authentication cryptography; the evaluator is serial to avoid provider bursts and preserve per-case measurements. These findings were assessed, not silently suppressed. No numeric Doctor health score is available.

Automatic approval review rejected the network-enabled Doctor scan because its outbound source/metadata payload was not verified. The local scan succeeded; remote scoring and supply-chain checks were not run.

Live Auth0 sign-in and external OpenRouter embeddings/Qdrant were verified in the follow-up below. Real second-account role assignment, Auth0 administration, and paid-provider behavior were not changed or re-verified. Earlier observations are recorded separately in `validation-2026-09-14.md`. The normal API was restarted with the changed backend code. Runbook sources are workspace-wide, while incident views are project-scoped; the project selector is not a tenant security boundary. Assignee discovery includes people who have signed in, not a full external organization directory.

## Manual handoff

## Live follow-up and additional fixes

- Restored Docker Desktop after its stale `dockerInference` runtime socket prevented startup. Archived its runtime `run` directory after confirming the backend had exited, then started the existing Qdrant service with `docker compose up -d qdrant`. No container volume or incident data was reset. Started the existing telemetry demo on port 5268 and the normal API on HTTPS 7104 / HTTP 5155.
- Verified real Auth0 sign-in in the in-app browser. The admin account displays its nickname claim (`aseel.sh52`) and appears in the assignee list. No identity permissions were changed.
- Verified live OpenRouter embeddings (`nvidia/nemotron-3-embed-1b:free`) and Qdrant: 209 sections were upserted, and the live QA incident retrieved two runbooks with `Status=available`, `EmbeddingProvider=openrouter`, and `VectorStore=qdrant`.
- Fixed Qdrant recovery after startup unavailability: later requests probe the service again and restore cached vectors. A healthy remote store returning no matches no longer silently queries SQLite. Diagnostics identify the actual Qdrant endpoint/collection instead of the cache database path.
- Confirmed by failing request-serialization tests that the generic Microsoft AI schema adapter omitted `json_schema.strict`. Native SDK options now send `strict: true` on initial and retry requests, plus OpenRouter `provider.require_parameters: true`. Tests inspect the actual serialized request body. See [OpenRouter structured output requirements](https://openrouter.ai/docs/guides/features/structured-outputs) and the [SDK extension-field mechanism](https://github.com/openai/openai-dotnet/blob/main/examples/Chat/Example10_AdditionalProperties.cs).
- Added transport validation before the SDK accesses `choices[0]`. Empty completions, output-token truncation, and provider errors embedded in HTTP 200 responses enter the bounded failure path. Numeric provider codes produce readable explanations without exposing raw provider content. Truncation gets additional output allowance on the single retry, capped at 4096 tokens.
- Added recovery controls for old pending incidents, refresh after failed retries, and unassessed confidence/quality states when no analysis completed. The live incident remains saved without a fabricated session or analysis.
- The synthetic live case is `INC-FF8274A3` (`ff8274a3-441c-463e-912c-d15e35089986`), titled `QA live provider verification 2026-09-16`, service `qa-checkout-verification`, environment `qa`. It explicitly disclaims a real outage. Reused this incident for retries; no mitigation or runbook publication was performed.
- The configured Nemotron free analysis endpoint returned malformed JSON with `finish_reason=stop`, one truncated response, and an upstream error with code 502 inside HTTP 200 even after strict enforcement and capability routing. `nex-agi/nex-n2.5-mini:free` then exceeded the 75-second application timeout. `liquid/lfm-2.5-2.6b:free` passed the same live incident on its first attempt in about 16 seconds, with valid JSON, two Qdrant runbook matches, and no fallback. It is now saved in local user secrets and set as the repository default. Local analysis and embedding fallback remain disabled in the normal app.
- Reverification: 123 backend tests, 5 helper tests, 27 main browser tests, 11 AI/RAG tests, 5 learning tests, 3 batched/fallback-disabled tests, and 1 autonomous-monitor test passed. The latest frontend state change also passed the five-test reliability suite. React Doctor's latest local changed-source scan has zero errors and the same two assessed dialog suggestions.

Follow [manual-qa.md](manual-qa.md) for the exact setup, 112 numbered cases, predictable log/metric payloads, provider failure controls, persistence checks, normal-account checks, and the final PASS / FAIL / BLOCKED checklist. The isolated harness is started with `node e2e/scripts/manual-qa.mjs` after build/publish. Use the normal authenticated app separately for genuine identity/provider checks; keep its local fallback settings disabled.
