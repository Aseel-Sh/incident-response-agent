# Incident workflow validation — 2026-09-14

## Changes and causes

| Before | After | Why |
| --- | --- | --- |
| Prompt-only model retry described nested evidence only as an array | Both attempts receive the complete response schema; retry receives validation feedback | Prevents a schema-rejected retry from guessing required evidence fields |
| Validation errors said only “schema-invalid” | Evidence errors identify the array index and required field types; hypotheses/actions validate arrays and strings | Improves diagnosis without silently accepting invented or malformed evidence |
| RAG had no Hugging Face credential | Explicit OpenRouter embedding provider using the existing key, with local fallback disabled | Restores an external embedding route |
| One HTTP request per runbook section | Batches of up to 32 sections locally; validates indexes and consistent vector dimensions | Reduces initial-index quota usage and preserves text/vector alignment |
| Provider failure could remain latched until restart | Next search retries the external provider and clears degradation on success | Recovers after temporary outages without enabling local fallback |
| Free-text assignment | Authenticated responder picker, Assign to me, Unassign, stable subject IDs | Makes assignment usable for actual users |
| Acknowledge could submit an edited assignment and overwrite the first actor | Acknowledgement preserves ownership and the first acknowledgement actor | Keeps coordination operations independent |
| Non-admin users saw administrative controls | Admin project/source/deletion/publication controls hidden; API protections independently tested | UI now reflects permissions |
| Identical timeline dots | Semantic blue/amber/green/red dots; labels/layout retained | Distinguishes action types without redesigning the timeline |
| No favicon/profile marker | Existing ir brand as SVG favicon; authenticated HTTPS profile picture or initials | Adds identity without introducing a new visual system |

## Automated results

All runs used zero Playwright retries. Test data and synthetic provider fixtures are separate from the live account. Fixture tests intentionally cover both fallback-enabled and fallback-disabled configurations; the actual application keeps all local fallbacks disabled.

- 111 backend tests passed: authentication, cookie/CSRF, authorization, persistence, project isolation, analysis validation, external provider contracts, retrieval and learning behavior.
- 22 main Playwright tests passed: anonymous/expired/denied sessions; admin and responder presentation; manual incidents; real-user directory assignment validation, self-assignment, picker assignment, unassignment and acknowledgement; mitigation/resolution/reopening; follow-up sessions; filtering, pagination and deletion; project creation/switching/removal; log and metric ingestion, invalid payloads; sources and signal diagnostics; scan controls; candidates, confirmation, ignore, false-positive and merge; keyboard/focus and mobile layout.
- 8 AI/RAG Playwright tests passed: source connection/sync/disable/removal; grounded analysis; missing and conflicting evidence; structured-output retry; model failure; preservation of failed-analysis incidents; embedding failure disclosure.
- 5 learning Playwright tests passed: feedback; successful/partial/failed actions; proposed knowledge; edit/approve/publish/reject; excluded knowledge; future retrieval and linked investigation sessions.
- 1 autonomous-monitoring Playwright test passed: background scans without a browser, deduplication, pause, resume and persisted scan progress.
- 1 batched external-RAG Playwright test passed: batched indexing, reordered response indexes, quota failure with fallback disabled, and recovery without restart.
- Total browser tests: 37.
- React Doctor: 100/100, zero diagnostics on the final scan.
- Production frontend build and backend build succeeded.

## Contrast and visual checks

Axe WCAG A/AA checks passed for login states and populated incident details. Explicit color-contrast checks passed across queue, history, sources, diagnostics, RAG, monitoring and configuration in both themes. Desktop detail screenshots were visually inspected; mobile layout tests passed.

Semantic text/surface contrast ratios (not a claim about every possible external image or data state):

| Pair | Light | Dark |
| --- | ---: | ---: |
| Primary text / panel | 16.99 | 14.33 |
| Secondary text / panel | 7.89 | 9.13 |
| Blue / blue surface | 6.01 | 6.57 |
| Green / green surface | 5.61 | 7.39 |
| Amber / amber surface | 5.71 | 7.96 |
| Red / red surface | 6.18 | 7.20 |
| SEV-1 | 5.98 | 7.09 |
| SEV-2 | 5.61 | 7.58 |
| SEV-3 | 5.64 | 8.09 |
| SEV-4 | 5.81 | 6.93 |

## Local configuration and operational boundaries

- Auth0 configuration remains in tenant dev-3oy1wuxqxah4orvu. The live account has admin; role-aware UI is additionally covered with synthetic responder sessions and independently signed backend test tokens. No second real Auth0 user was created.
- Local User Secrets select OpenRouter / nvidia/nemotron-3-embed-1b:free, endpoint https://openrouter.ai/api/v1, BatchSize 32 and Qdrant collection incident_runbook_nemotron3_2048. The existing Qdrant collection was preserved.
- OpenRouter accepted a real external embedding request and returned 2048 dimensions. The paid-model probe hit the existing key spending limit; it was not changed. Initial per-section indexing exhausted the shared 50/day free quota; batching was then implemented and tested. The provider reported reset at 2026-09-15 00:00 UTC (7 PM America/Chicago).
- The configured telemetry producer was offline. Restarted the existing incident-telemetry-demo on http://127.0.0.1:5268; did not replace real source health with fabricated signals.
- The local responder directory lists people who have authenticated to this instance, not every Auth0 tenant user. Assignment is not an authorization grant. Auth0 provisioning remains in its dashboard.
- Resolve and mitigate record human decisions; they do not execute infrastructure changes. Resolution generates a reviewable draft rather than automatically publishing runbooks.

## Live verification completed

- After the quota reset, all 209 runbook sections were successfully indexed into Qdrant using external OpenRouter embeddings. RAG reported available.
- On September 15, retried the previously failed live incident INC-04152C93 (Conflicting checkout signals) through the signed-in app. The configured OpenRouter model nvidia/nemotron-3-super-120b-a12b:free passed strict-schema validation on its first attempt, with analysis completing in approximately 7.2 seconds.
- The live analysis retrieved three runbook matches; logs confirmed UsedFallback=False, RagStatus=available and RagDegraded=False. The UI showed completed analysis, provider disclosure, evidence references, recommendations and explicit missing-log/metric gaps. Low confidence is appropriate: matching operational evidence for that incident window is absent.
- The API and telemetry demo had stopped between sessions and were restarted for this verification. No Auth0 permissions, spending limits or fallback settings were changed during the final check.
- Inspected the live light-theme incident detail at the narrow in-app browser width; content remained readable without horizontal clipping.
