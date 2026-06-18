# UI functionality matrix

Update this table whenever a visible interactive control is added, removed, or changes behavior.

| Page | Button/Tab | Expected Behavior | Actual Behavior | Needed? | Useful? | Broken? | Test Exists? |
|---|---|---|---|---|---|---|---|
| Global | Dashboard tab | Open candidate and active incident backlog | Activates dashboard and preserves URL hash | Yes | Yes | No | Static selector check |
| Global | Analysis tab | Open current/manual analysis workspace | Activates analysis view | Yes | Yes | No | Static selector check |
| Global | History tab | Load and display persisted analyses | Activates history and reloads records | Yes | Yes | No | Static selector check |
| Global | Sources tab | Show actual source status and configured locations | Reloads operational sources | Yes | Yes | No | API tests indirect |
| Global | Ingestion tab | Post log/metric samples | Opens functional ingestion forms | Yes | Yes | No | Provider tests |
| Global | RAG tab | Run retrieval diagnostics | Opens and executes current query | Yes | Yes | No | Retrieval tests |
| Global | Evaluation tab | Run bundled analysis scenarios | Loads scenario catalog and allows execution | Yes | Yes | No | Evaluation tests |
| Global | Monitor tab | Show/pause/resume scans | Opens persisted monitor controls | Yes | Yes | No | Monitor tests |
| Global | Config tab | Show effective source/provider configuration | Renders read-only verified configuration | Yes | Yes | No | API tests indirect |
| Global | Theme button | Toggle light/dark theme | Toggles `data-theme` | No | Yes | No | No |
| Global | Help button | Explain navigation | Shows concise help toast | No | Yes | No | No |
| Dashboard | Search | Filter tickets and candidates | Filters by title, service, environment, source, signals, and tags | Yes | Yes | No | No |
| Dashboard | Status chips | Filter by candidate/active/mitigated/resolved | Applies selected status and resets pagination | Yes | Yes | No | No |
| Dashboard | Create Incident | Open manual candidate form | Opens/reset form with current detection time | Yes | Yes | No | No |
| Dashboard | Confirm candidate | Create confirmed incident and analyze | Persists confirmation, starts linked analysis, shows result | Yes | Yes | No | Store tests |
| Dashboard | False positive | Exclude candidate from incidents/learning | Persists decision after confirmation dialog | Yes | Yes | No | Store regression test |
| Dashboard | Ignore | Persist ignored candidate | Persists decision after confirmation dialog | Yes | Yes | No | Store tests indirect |
| Dashboard | Merge duplicate | Merge candidate evidence into target incident | Available only with a real duplicate ID; persists merge | Yes | Yes | No | Store tests indirect |
| Dashboard | Open incident | Open persisted incident detail | Opens history modal using actual record | Yes | Yes | No | No |
| Analysis | Sample | Fill a realistic sample incident | Populates form without submitting | No | Yes | No | No |
| Analysis | Create candidate and confirm | Persist manual candidate, confirm, analyze | Runs complete candidate lifecycle; preserves follow-up session ID | Yes | Yes | No | Request/store tests |
| Analysis | Copy evidence | Copy exact evidence text | Copies selected evidence and shows toast | No | Yes | No | No |
| Analysis | Similar History | Open actual matched incident | Loads record by real incident GUID or reports unavailable | Yes | Yes | No | No |
| Analysis | Similar Compare | Compare current and actual prior incident | Shows real metadata, shared signals, and action outcomes | Yes | Yes | No | No |
| Analysis | Log outcome | Persist worked/partial/failed result | Saves outcome and explains approval gate for reuse | Yes | Yes | No | Store tests |
| Analysis | Save feedback | Persist quality/correctness ratings and reason tags | Validates required ratings and saves to incident record | Yes | Yes | No | Store/API tests needed |
| Analysis | Rate this recommendation | Select a recommendation for correctness feedback | Copies the exact recommendation into the feedback form and focuses it | Yes | Yes | No | No |
| History | Filters | Filter persisted incidents | Filters service, status, session, severity, confidence | Yes | Yes | No | No |
| History | Refresh | Reload persisted incidents | Reloads and rerenders records | Yes | Yes | No | No |
| History | Row/title | Open incident detail | Opens structured, evidence-grounded detail | Yes | Yes | No | No |
| History modal | Continue session | Start linked follow-up | Opens clean form with retained session ID | Yes | Yes | No | Session tests |
| History modal | Copy session ID | Copy exact session identifier | Copies session ID | No | Yes | No | No |
| History modal | Work/Mitigate/Resolve/Reopen | Transition lifecycle | Persists status, timeline, and proposal generation | Yes | Yes | No | Store tests indirect |
| History modal | Approve/Reject knowledge | Human-gate reusable learning | Persists edited content and review decision | Yes | Yes | No | Store similarity test |
| History modal | Delete incident | Permanently remove incident and retrieval references | Requires confirmation and removes record/references | Yes | Yes | No | Delete test |
| History modal | Close / backdrop / Escape | Dismiss detail | All three dismissal paths work | Yes | Yes | No | No |
| Ingestion | Post Signal | Append log sample | Persists submitted sample and reports result | Yes | Yes | No | Provider tests indirect |
| Ingestion | Post Metric | Append metric sample | Persists submitted sample and reports result | Yes | Yes | No | Provider tests indirect |
| Ingestion | Rescan | Run deterministic monitoring scan | Invokes persisted scan endpoint | Yes | Yes | No | Monitor tests |
| RAG | Search | Retrieve actual runbook chunks and diagnostics | Displays provider, vector store, RAG status, and scores | Yes | Yes | No | Retrieval tests |
| Evaluation | Run | Populate and analyze selected scenario | Executes scenario through normal workflow | No | Yes | No | Evaluation tests |
| Monitor | Pause/Resume | Control browser polling | Stops/starts interval without claiming server monitor stopped | Yes | Yes | No | No |
| Monitor | Manual Refresh | Run scan immediately | Calls persisted scan endpoint and updates status | Yes | Yes | No | Monitor tests |
| Monitor | Polling slider | Set browser polling interval | Persists interval locally and restarts polling | Yes | Yes | No | No |

## Removed misleading controls

- Removed fake readonly source-path fields and the nonfunctional “Prometheus endpoint (coming soon)” field. Source configuration is explicitly described as appsettings/environment managed.
- Removed fabricated similar-incident IDs, dates, environments, and remediation text. Comparisons now use API-provided incident records only.
