# UI functionality matrix

Update this table whenever a visible interactive control is added, removed, or changes behavior.

| Page | Button/Tab | Expected Behavior | Actual Behavior | Needed? | Useful? | Broken? | Test Exists? |
|---|---|---|---|---|---|---|---|
| Global | Dashboard tab | Open candidate and active incident backlog | Activates dashboard and preserves URL hash | Yes | Yes | No | Playwright shell E2E |
| Global | Analysis tab | Open current/manual analysis workspace | Activates analysis view | Yes | Yes | No | Playwright shell E2E |
| Global | History tab | Load and display persisted analyses | Activates history and reloads records | Yes | Yes | No | Playwright shell E2E |
| Global | Sources tab | Show actual source status and configured locations | Reloads operational sources | Yes | Yes | No | Playwright shell E2E |
| Global | Ingestion tab | Post log/metric samples | Opens functional ingestion forms | Yes | Yes | No | Playwright shell E2E + provider tests |
| Global | RAG tab | Run retrieval diagnostics | Opens and executes current query | Yes | Yes | No | Playwright shell E2E + retrieval tests |
| Global | Evaluation tab | Run bundled analysis scenarios | Loads scenario catalog and allows execution | Yes | Yes | No | Playwright shell E2E + evaluation tests |
| Global | Monitor tab | Show/pause/resume scans | Opens persisted monitor controls | Yes | Yes | No | Playwright shell E2E + monitor tests |
| Global | Config tab | Show effective source/provider configuration | Renders read-only verified configuration | Yes | Yes | No | Playwright shell E2E |
| Global | Theme button | Toggle light/dark theme | Toggles `data-theme` | No | Yes | No | Playwright shell E2E |
| Global | Help button | Explain navigation | Shows concise help toast | No | Yes | No | Playwright shell E2E |
| Sources | Source status cards | Show only configured/bundled sources with verified connected, pending, or missing state | Renders backend source inventory and never treats missing/pending as connected | Yes | Yes | No | Playwright monitoring/detection E2E |
| Dashboard | Search | Filter tickets and candidates | Filters by title, service, environment, source, signals, and tags | Yes | Yes | No | Playwright manual incident E2E |
| Dashboard | Status chips | Filter by candidate/active/mitigated/resolved | Applies selected status and resets pagination | Yes | Yes | No | Playwright manual incident E2E |
| Dashboard | Create Incident | Open manual candidate form | Opens/reset form with current detection time | Yes | Yes | No | Playwright manual incident E2E |
| Dashboard | Confirm candidate | Create confirmed incident and analyze | Persists confirmation without silently starting work; shows grounded result | Yes | Yes | No | Playwright monitoring/detection E2E |
| Dashboard | False positive | Exclude candidate from incidents/learning | Persists decision after confirmation dialog | Yes | Yes | No | Playwright monitoring/detection E2E |
| Dashboard | Ignore | Persist ignored candidate | Persists decision after confirmation dialog | Yes | Yes | No | Playwright monitoring/detection E2E |
| Dashboard | Merge duplicate | Merge candidate evidence into target incident | Available only with a real duplicate ID; persists merge without creating another incident | Yes | Yes | No | Playwright monitoring/detection E2E |
| Dashboard | Open incident | Open persisted incident detail | Opens history modal using actual record | Yes | Yes | No | Playwright history E2E |
| Analysis | Sample | Fill a realistic sample incident | Populates form without submitting | No | Yes | No | No |
| Analysis | Create candidate and confirm | Persist manual candidate, confirm, analyze | Runs complete candidate lifecycle; preserves follow-up session ID and renders grounded model/fallback output | Yes | Yes | No | Playwright manual/follow-up + AI/RAG E2E |
| Analysis | Provider information | Disclose model, embedding provider, vector store, RAG, retry, degraded mode, and fallback | Displays persisted providers and explicit `RAG degraded` / fallback reasons | Yes | Yes | No | Playwright AI/RAG E2E |
| Analysis | Known facts / hypotheses / unknowns / evidence | Separate claims by epistemic status and cite evidence | Every tested fact, hypothesis, and action maps to a supplied source reference | Yes | Yes | No | Playwright AI/RAG grounding gates |
| Analysis | Runbook matches | Show retrieved relevant sections and disclose empty retrieval | Displays matching sections; empty results explicitly state that absence is not proof no runbook exists | Yes | Yes | No | Playwright AI/RAG E2E |
| Analysis | Copy evidence | Copy exact evidence text | Copies selected evidence and shows toast | No | Yes | No | No |
| Analysis | Similar History | Open actual matched incident | Loads only an eligible persisted incident by real GUID or reports unavailable | Yes | Yes | No | Playwright learning-loop E2E |
| Analysis | Similar Compare | Compare current and actual prior incident | Shows real metadata, shared signals, and approved action outcomes | Yes | Yes | No | Playwright learning-loop E2E |
| Analysis | Log outcome | Persist worked/partial/failed result | Saves outcome, appends timeline evidence, and explains approval gate for global reuse | Yes | Yes | No | Playwright learning-loop E2E + store tests |
| Analysis | Save feedback | Persist quality/correctness ratings and reason tags | Validates all ratings/reason tags and saves them to incident history | Yes | Yes | No | Playwright learning-loop E2E |
| Analysis | Rate this recommendation | Select a recommendation for correctness feedback | Copies the exact recommendation into the feedback form and focuses it | Yes | Yes | No | No |
| History | Search | Search persisted incident title, description, metadata, tags, IDs, and sessions | Filters immediately and resets pagination | Yes | Yes | No | Playwright history E2E |
| History | Filters | Filter persisted incidents | Filters service, status, session, severity, confidence | Yes | Yes | No | Playwright history/follow-up E2E |
| History | Refresh | Reload persisted incidents | Reloads and rerenders records | Yes | Yes | No | Playwright severity E2E |
| History | Row/title | Open incident detail | Opens structured, evidence-grounded detail | Yes | Yes | No | Playwright manual/history E2E |
| History modal | Continue session | Start linked follow-up | Opens clean form with retained session ID and exposes earlier same-session outcomes without globally approving them | Yes | Yes | No | Playwright follow-up + learning-loop E2E |
| History modal | Copy session ID | Copy exact session identifier | Copies session ID | No | Yes | No | No |
| History modal | Work/Mitigate/Resolve/Reopen | Transition lifecycle | Start Work is explicit; all transitions persist status, timeline, and proposal generation | Yes | Yes | No | Playwright lifecycle E2E |
| History modal | Approve/Reject knowledge | Human-gate reusable learning | Persists edited content and decision; only resolved approved records enter global similarity/action reuse | Yes | Yes | No | Playwright learning-loop E2E + store similarity test |
| History modal | Delete incident | Permanently remove incident and retrieval references | Requires confirmation and removes record/references | Yes | Yes | No | Playwright deletion/retrieval E2E |
| History modal | Close / backdrop / Escape | Dismiss detail | All three paths restore focus to the opener | Yes | Yes | No | Playwright accessibility E2E |
| Ingestion | Post Signal | Append log sample | Persists submitted sample, reports acceptance, and makes it available to detection | Yes | Yes | No | Playwright monitoring/detection E2E |
| Ingestion | Post Metric | Append metric sample | Persists submitted sample, reports acceptance, and makes it available to detection | Yes | Yes | No | Playwright monitoring/detection E2E |
| Ingestion | Rescan | Run deterministic monitoring scan | Invokes persisted scan endpoint and displays backend scan facts | Yes | Yes | No | Playwright monitoring/detection E2E |
| RAG | Search | Retrieve actual runbook chunks and diagnostics | Displays provider, vector store, RAG status, and scores; common single-token overlap no longer bypasses relevance thresholds | Yes | Yes | No | Playwright AI/RAG E2E + retrieval tests |
| Evaluation | Run | Populate and analyze selected scenario | Executes scenario through normal workflow | No | Yes | No | Evaluation tests |
| Monitor | Pause/Resume | Control browser polling | Stops/starts polling, persists the browser setting, and restores it truthfully | Yes | Yes | No | Playwright monitoring/detection E2E |
| Monitor | Manual Refresh | Run scan immediately | Calls persisted scan endpoint and displays its timestamp, duration, source count, candidates, and errors | Yes | Yes | No | Playwright monitoring/detection E2E |
| Monitor | Polling slider | Set browser polling interval | Persists interval locally and restarts polling | Yes | Yes | No | No |

## Removed misleading controls

- Removed fake readonly source-path fields and the nonfunctional “Prometheus endpoint (coming soon)” field. Source configuration is explicitly described as appsettings/environment managed.
- Removed fabricated similar-incident IDs, dates, environments, and remediation text. Comparisons now use API-provided incident records only.
