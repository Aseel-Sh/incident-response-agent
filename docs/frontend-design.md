# Incident response workbench

The workbench preserves the existing workflows and API contracts. The September 16 reliability pass adds compact visualizations computed from saved incident records, without inventing metrics, integrations, topology, or analysis capabilities. See [the current engineering report](reliability-2026-09-16.md) and [complete manual QA guide](manual-qa.md).

## Audit and decisions

| Before | After | Reason |
| --- | --- | --- |
| A fixed 760px content column with a hidden page title | A full-width workspace, visible page identity, and grouped navigation | Use desktop space for operational work |
| Incident cards with many badges | Aligned severity, identity, status, owner, evidence source, activity, and age columns | Make triage scannable |
| Saved incidents could push candidates off the first page | Candidates precede saved incidents; explicit Open queue filter | Keep incoming signals visible |
| History fetched only 12 records | Fetch up to 100 recent records, with existing ten-row pagination | Broaden search and lifecycle visibility without changing the API |
| Ownership form above the selected incident's title | Identity, SEV, status, service/project, owner, timestamps, and analysis state lead the detail view | Establish context before controls |
| Similar treatments for facts, hypotheses, and unknowns | Facts use evidence rails; hypotheses use H1/H2 numbering; unknowns use a dashed validation-gap section | Distinguish observation from inference without a rainbow |
| Provider internals appeared before the investigation | Compact provider/fallback and degradation disclosure; expandable diagnostics | Keep caveats visible and internals secondary |
| Large source-registration forms dominated their pages | Native expandable setup forms and visible source inventories | Prioritize inspecting current state |
| Guessed incident IDs, missing metadata, and similarity fallback percentages | IDs derived from records, explicit missing values, and actual scores | Avoid presenting invented information |

## Shared visual system

`frontend/src/console.css` contains the workbench design layer; existing component hooks stay stable. Neutral surfaces, thin separators, 13px body text, 20–24px principal headings, and monospace identifiers/logs establish hierarchy. Most content is divided into sections rather than nested cards. Severity uses restrained red, orange, ochre, and blue, always accompanied by SEV text. Other color is reserved for state, actionable warnings, and primary actions.

Dark mode uses charcoal surfaces and independently chosen text, border, evidence, and severity colors. Repeated interactions do not animate. Reduced motion is respected. Controls retain visible focus, current-navigation and pressed-filter states. Incident dialogs isolate the background, trap focus, and restore it on close.

## Earlier redesign validation (historical)

- Final runs after authentication: **32 Playwright tests passed** (19 authentication/core/monitoring/mobile, 8 AI/RAG, 5 learning), with retries disabled and isolated fixture resets between suites. All **93 backend tests** passed, including anonymous access, permission checks, expiry, CSRF, logout, safe return URLs, and the production identity guard.
- Production Vite build and checked-in API static assets regenerated.
- Core/monitoring/mobile Playwright suite covers navigation, themes, project management, creation, severity, lifecycle, acknowledgement, follow-up, filtering, pagination, deletion, signal ingestion, candidate decisions, similarity, keyboard interaction, and contrast.
- Learning suite covers feedback, action outcomes, knowledge approval/rejection, and linked-session evidence.
- AI/RAG suite uses deterministic local model/embedding fixtures to cover retrieval, missing/conflicting evidence, structured-output retries, model failure, and degraded embeddings. Fixture data must be reset between AI and learning suites: approved learning documents otherwise change retrieval expectations.
- Playwright MCP inspection included desktop, mobile, light/dark, populated detail, empty/failed analysis, and secondary tools. Overflow checks covered 390, 700, 900, and 1100px widths; the desktop detail was also checked at 1440px. Axe checks found and verified fixes for RAG definition-list markup and dark Resolve contrast.
- React Doctor 0.9.14: final uncommitted-file scan against HEAD, including untracked files, completed with React detected and zero diagnostics. The earlier focused design scan also returned no diagnostics. Its score service was unavailable, so no health score is claimed.

## Contrast follow-up

The initial neutral palette was too muted in practice. Both themes now have stronger secondary text, separators, field boundaries, and surface separation. Queue labels and incident metadata are larger. Selected navigation and filters use blue; resolution and successful outcomes use green; warnings retain amber. Severity remains the strongest recurring color cue.

Legacy theme selectors were overriding shared action styles and leaving old semantic colors in dark mode. Shared action selectors and explicit dark semantic aliases now give both themes consistent meaning. The production build passed, React Doctor reported zero diagnostics, and Playwright/axe checks passed across the main screens and populated detail in both themes. The mobile queue had no horizontal overflow at 390px.

## Authentication entry and access control

The sign-in page follows the same neutral surfaces, blue primary action, typography, and intentional light/dark palette. Loading, unavailable authentication, expired/cancelled/failed sign-in, and insufficient permissions have dedicated states. Auth0 owns credential entry and recovery; this app does not collect passwords. The workspace is lazily loaded only after the server confirms access, and expired API sessions remove the workspace before returning to sign-in. Account identity and sign-out are visible in the sidebar.

Server authorization now applies by default, including readiness diagnostics. Cookie CSRF checks use the authentication result rather than assuming a particular OIDC identity label. Local automatic identity is disabled by default and rejected outside Development/Testing. The live tenant must authorize the browser application's user-delegated API access before Auth0 will show login.

## Remaining boundaries

- Queue/history counts and filters operate on the loaded recent-record window, not an unlimited server-side history search.
- Confirmed records expose evidence sources; the recent-record response does not expose a separate original detector-source field. The UI labels the sources it actually has.
- Model and embedding behavior was verified with local deterministic providers, not paid external services. An unavailable provider remains visible rather than being replaced with invented analysis.
- React now owns the normal JSX layout, dashboard counts/charts, pending-review discovery, proposal editor/actions, and runbook source inventory/actions. API requests, identity, diagnostics, analytics, and formatting have focused modules. The controller still owns the remaining workflow rendering; the migration is incremental.

## September 16 reliability follow-up

Lifecycle colors are shared by counters, rows, filters, details, timeline events, and status charts: candidates blue, confirmed purple, active amber, mitigated cyan, resolved green. Each theme has its own contrast-checked palette, always paired with text. Severity retains its labeled container without a decorative pseudo-element.

The collapsible overview shows creation-date counts, severity distribution, and current lifecycle from the current project’s loaded recent incidents (at most 100). It excludes candidates and discloses UTC dates and missing timestamps. It is not a lifetime analytics service and does not claim acknowledgement or resolution-time averages.

Resolution reveals a reviewable proposal and queue-level pending-review entry. Approval, Markdown publication, and retrieval are distinct states; publication does not claim that indexing succeeded. Source counts describe sections found in Markdown, rather than claiming those sections are already indexed. Refer to the current engineering report for verification results and remaining boundaries; the historical counts above are not the current test totals.
