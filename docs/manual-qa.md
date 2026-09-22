# Incident Response Agent — complete manual QA walkthrough

Run this guide after building the current checkout. Record PASS, FAIL, or BLOCKED for each numbered case. A failing provider is an expected test outcome only in the explicitly named failure cases. A successful HTTP response alone does not prove an incident was analyzed: inspect `analysisState`, fallback disclosure, stored evidence, and the visible result.

## Two environments

Use the **isolated QA workspace** for destructive tests, synthetic signals, malformed responses, and fallback experiments. It uses a local deterministic provider on port 5299 and a separate API on 5298. This is explicitly synthetic test infrastructure, not evidence that OpenRouter or Auth0 works. It does not change the normal app's fallback settings or credentials.

Use the **normal authenticated app** at `https://localhost:7104` for actual Auth0 identities, roles, external OpenRouter, and Qdrant checks. Never perform the destructive reset below on its `.data` directory or live runbooks. Real provider tests consume provider quota and require its configured account to be available.

## Setup from a known state

Open PowerShell in `C:\Users\Aseel\source\repos\IncidentResponseAgent`. Install .NET 10 SDK and Node.js with npm. Run:

```powershell
npm.cmd ci
dotnet restore IncidentResponseAgent.slnx
npm.cmd run build
dotnet publish IncidentResponseAgent.Api/IncidentResponseAgent.Api.csproj --artifacts-path .tmp/auth-build -o .tmp/e2e-app --no-restore
node e2e/scripts/manual-qa.mjs
```

Keep the last terminal open. Visit `http://127.0.0.1:5298`. This serves the production frontend build; a separate Vite process is unnecessary. For frontend development instead, run `npm.cmd run dev` and use the Vite URL, checking `frontend/vite.config.mjs` for its API target. Do not assume Vite points at the isolated QA server.

The QA script keeps existing QA data across restarts. For a **fresh** run, stop it with Ctrl+C, then archive only its directory:

```powershell
$qaRoot = Join-Path (Get-Location) '.tmp/manual-qa'
$expectedQaRoot = 'C:\Users\Aseel\source\repos\IncidentResponseAgent\.tmp\manual-qa'
if ([IO.Path]::GetFullPath($qaRoot) -ne $expectedQaRoot) { throw 'Unexpected QA path' }
if (Test-Path -LiteralPath $qaRoot) {
  Move-Item -LiteralPath $qaRoot -Destination ($qaRoot + '-backup-' + (Get-Date -Format yyyyMMdd-HHmmss))
}
node e2e/scripts/manual-qa.mjs
```

This archives rather than deletes your test results. It does not clear browser preferences: use a fresh browser profile or clear site data **only for `127.0.0.1:5298`** when testing first-visit defaults.

For the normal app: start Docker Desktop, run `docker compose up -d`, run the configured telemetry demo if its source health URL is enabled, then `dotnet run --project IncidentResponseAgent.Api --launch-profile https`. Local Auth0/OpenRouter settings are in .NET User Secrets, not in this guide. Keep all normal-app local fallback settings false. Sign in yourself; never paste passwords, tokens, or client secrets into a report. `/health` checks the process; `/ready` requires authorization and reports dependency readiness. A healthy process is not proof that external providers are available.

## QA data and inspection helpers

Use a second PowerShell terminal. The following helpers are for the isolated QA API only (development identity, no bearer token required):

```powershell
$qaApi = 'http://127.0.0.1:5298'
function Send-Qa($path, $data, $method = 'POST') {
  Invoke-RestMethod ($qaApi + $path) -Method $method -ContentType 'application/json' -Body ($data | ConvertTo-Json -Depth 12)
}
function Qa-Log($service, $level, $message, $project = 'default') {
  Send-Qa '/api/signals/logs' @{ projectId=$project; source=$service; level=$level; message=$message; timestamp=[DateTimeOffset]::UtcNow.ToString('o'); correlationId='manual-qa' }
}
function Qa-Metric($service, $metric, $value, $project = 'default') {
  Send-Qa '/api/signals/metrics' @{ projectId=$project; serviceName=$service; environment='prod'; metricName=$metric; value=$value; timestamp=[DateTimeOffset]::UtcNow.ToString('o') }
}
function Qa-Incident($title, $severity = 'sev2', $service = 'checkout-api', $project = 'default') {
  Send-Qa '/api/incidents/analyze' @{ title=$title; description='Checkout database connection pool timeout; inspect logs and error-rate metrics.'; severity=$severity; serviceName=$service; environment='prod'; projectId=$project; tags=@('checkout','database','timeout') }
}
$qaRecent = { Invoke-RestMethod "$qaApi/api/incidents/recent?maxResults=100" }
```

Default QA stores: `.tmp/manual-qa/incidents.json` (incidents, candidates, timelines, reviews), `sessions.sqlite` (investigations), `logs.json`, `metrics.json`, `projects.json`, `responders.json`, `monitoring.json`, `sources.json`, `rag.sqlite`, and `knowledge/approved-<proposal-id-without-hyphens>.md`. Some files are created only after their first use. Use the API's returned ingestion `location` and Sources page to verify actual project paths. Do not edit JSON while the server is writing it.

Create a separate runbook source for connection/removal tests:

```powershell
New-Item -ItemType Directory -Force .tmp/manual-qa/connected-runbooks | Out-Null
@'
# Checkout connection pool recovery
Service: checkout-api
Environment: prod
Tags: checkout, database, connection, pool, timeout

## Diagnosis
Check connection-pool utilization and compare request error rate with the prior healthy window.

## Mitigation
Reduce incoming concurrency in one controlled step after responder approval.

## Verification
Verify that request errors decline and p95 latency returns below the alert threshold.
'@ | Set-Content .tmp/manual-qa/connected-runbooks/checkout-pool.md
```

### Common manual incident input

Click **Incidents → Create Incident**. Enter title `QA checkout pool timeout`, severity `SEV-2`, service `checkout-api`, environment `prod`, tags `checkout, database, timeout`, and description `Checkout requests time out while waiting for the database connection pool. Investigate the recent errors and compare request_error_rate.` Leave Session ID blank for a new investigation. Click **Create candidate & confirm**. This creates and confirms a candidate, then attempts analysis; a provider failure must preserve the incident.

Before evidence-dependent tests, run:

```powershell
Qa-Log 'checkout-api' 'Error' 'prod database connection pool timeout on checkout request'
Qa-Log 'checkout-api' 'Error' 'prod database connection pool timeout on second checkout request'
Qa-Metric 'checkout-api' 'request_error_rate' 45
Qa-Metric 'checkout-api' 'p95_latency' 2200
```

Use fresh timestamps and complete a signal batch within ten minutes. Detection uses a rolling window. Isolate threshold cases by service name; previously ingested failing samples can otherwise keep an alert active. Do not infer causation or root cause merely from a generated hypothesis.

### Deterministic failure controls

In the isolated provider, incident titles containing `Malformed once` produce one invalid response then a valid retry; `Malformed always` produce invalid JSON on both attempts; `Model unavailable` returns a provider failure; `Model timeout` delays longer than the QA model deadline. These switches apply only to the synthetic provider, not to OpenRouter. Use service `checkout-api`, environment `prod`, and the evidence batch above when testing evidence-backed local fallback.

Local fallback is disabled by default in the QA script. To test the explicitly enabled branch: Ctrl+C the QA script, run `node e2e/scripts/manual-qa.mjs --fallback`, perform only cases 58–60, then stop and restart without `--fallback`. Existing QA data persists; normal app settings remain unchanged.

## Numbered test cases

Follow the cases in order except where a case explicitly says to use the normal authenticated app or reset the isolated workspace. Every case names its starting state, action/input, expected UI, persistence result, and failure indicators.

### 1. Start the app and dependencies

- **Start:** Fresh checkout; no QA process.
- **Action/input:** Run the setup commands above, then open port 5298. Separately start the normal app with Docker/Qdrant and configured telemetry when testing live dependencies.
- **Expected UI:** QA opens as local-operator; normal app requires sign-in. Sidebar and tabs load.
- **Expected backend/persistence:** QA files stay under .tmp/manual-qa; no live .data changes.
- **Bug indicators:** Port collision, blank workspace, production identity bypass, or writes outside the chosen QA paths.

### 2. Process health versus readiness

- **Start:** Both apps started; QA in a second terminal.
- **Action/input:** Run Invoke-RestMethod "$qaApi/health" and Invoke-RestMethod "$qaApi/ready". Open Config and RAG. For live /ready, use an authenticated browser.
- **Expected UI:** Health reports the process; RAG/provider readiness is shown separately. An unavailable dependency is not a healthy analysis claim.
- **Expected backend/persistence:** GET requests do not create incidents. The readiness response identifies configured dependencies.
- **Bug indicators:** Healthy badge treated as proof of model success, unauthenticated workspace details exposed, or missing dependency reported available.

### 3. Authentication and identity boundaries

- **Start:** Normal app; private browser signed out. Have your admin account and a separate authorized responder account available.
- **Action/input:** Open /#history before login; sign in, sign out, refresh, and reopen the deep link. Repeat with responder and an account without incident access. For deterministic observation run npx.cmd playwright test e2e/authentication.spec.ts --headed --retries=0.
- **Expected UI:** Signed-out and denied users see no workspace. Admin sees administrative controls; responder sees operational controls only. QA's dev identity is intentional and limited to Testing/Development.
- **Expected backend/persistence:** Anonymous API calls return 401; authenticated unauthorized actions return 403. Session expiration closes the workspace. Stable subject IDs are retained.
- **Bug indicators:** Workspace data appears before authorization; responder can publish/delete/manage sources; sign-out leaves usable stale data. Mark the real second-account branch BLOCKED if no such account is provisioned.

### 4. Create a project

- **Start:** QA, admin; Sources open.
- **Action/input:** Expand Add a project workspace. Enter ID qa-alpha, Name QA Alpha, log path C:\Users\Aseel\source\repos\IncidentResponseAgent\.tmp\manual-qa\alpha-logs.json, metric path with alpha-metrics.json; health endpoint blank; error threshold 25, latency 1000. Click Add project. Repeat ID qa-beta with beta paths.
- **Expected UI:** Both workspaces appear in Project and Sources. Creation success is explicit.
- **Expected backend/persistence:** projects.json contains both IDs and distinct paths. Files may not exist until first ingestion.
- **Bug indicators:** Duplicate IDs silently overwrite, creation succeeds despite invalid configuration, or both projects use the same file unexpectedly.

### 5. Switch projects

- **Start:** qa-alpha and qa-beta exist.
- **Action/input:** Choose QA Alpha, then QA Beta, then Global view in Project. Visit Incidents, History, Monitor, Sources between switches.
- **Expected UI:** Labels and scoped results follow the selected project. Global view explicitly shows multiple projects.
- **Expected backend/persistence:** Reads contain projectId where required; selection is stored locally.
- **Bug indicators:** Previous-project rows/counters stay visible as current data, or the selector changes without refreshing views.

### 6. Project isolation

- **Start:** QA Alpha selected; QA Beta empty.
- **Action/input:** Create an incident titled Alpha-only checkout timeout; ingest Qa-Metric 'alpha-api' 'request_error_rate' 45 'qa-alpha'. Switch to Beta and scan; return to Alpha, then Global view.
- **Expected UI:** Alpha data is absent from Beta; Global view includes it with project identification.
- **Expected backend/persistence:** Alpha incident has projectId qa-alpha; alpha-metrics.json changes and beta-metrics.json does not. Project scoping is a workspace filter, not a tenant security boundary.
- **Bug indicators:** Signal ingestion writes to default/Beta; similarity claims use another project's history where scoped retrieval should exclude it.

### 7. Project settings and unsupported editing

- **Start:** Sources shows QA Alpha.
- **Action/input:** Inspect paths/thresholds. There is no project-edit API/UI in this version: verify this limitation rather than looking for a nonexistent Save button. To test different settings, create qa-threshold with different paths and error threshold 50.
- **Expected UI:** Read-only settings match creation input; creating another workspace is available to admin.
- **Expected backend/persistence:** Existing project metadata remains unchanged. A new project persists independently.
- **Bug indicators:** UI implies editing is supported, displays thresholds not returned by API, or overwrites a project while claiming to add one.

### 8. Remove a project

- **Start:** Disposable qa-beta; switch to it first.
- **Action/input:** In Sources click its Remove project control; cancel once, then confirm. Do not delete source files manually.
- **Expected UI:** Cancel changes nothing. Confirm removes the selector option and safely selects an available workspace.
- **Expected backend/persistence:** Registry entry is removed. Underlying log/metric files and existing incident history are not assumed deleted; inspect Global view to confirm retained history where present.
- **Bug indicators:** Removing a registry entry deletes unrelated files/incidents or leaves a dead selected project. Record the returned behavior rather than assuming cascading deletion.

### 9. Manual incident creation

- **Start:** Default project; fresh evidence batch from setup.
- **Action/input:** Enter the common manual incident input and click Create candidate & confirm once.
- **Expected UI:** Candidate confirmation is followed by completed analysis, or an explicit saved-incident failure state. Title, service, environment and SEV-2 stay accurate.
- **Expected backend/persistence:** One confirmed incident with a stable ID, session ID, creation event and analysis result is stored.
- **Bug indicators:** Double creation from one click, silently changed severity, fake logs/metrics, or incident lost on provider failure.

### 10. Every severity

- **Start:** Default project; QA provider working.
- **Action/input:** Repeat common input with titles QA severity 1 through QA severity 5, selecting each SEV level. Open History and each detail.
- **Expected UI:** Clean SEV-1 through SEV-5 text badges, distinct styling; no square/pseudo-icon next to text.
- **Expected backend/persistence:** Each stored severity matches the submitted value; no inference from the title replaces it.
- **Bug indicators:** Model-suggested severity silently overwrites user selection, missing SEV-5, duplicated markers, or unreadable badge text.

### 11. Validation failures

- **Start:** Create Incident form empty; second terminal available.
- **Action/input:** Submit blank title/description/severity; try title over 200 characters. Send Send-Qa '/api/incidents/analyze' @{title='Invalid';description='Validation';severity='critical'}. Post a metric without serviceName.
- **Expected UI:** Required controls show validation; invalid API input returns 400 with understandable details; no success notification.
- **Expected backend/persistence:** No invalid incident/sample is appended. Compare recent count and file size before/after.
- **Bug indicators:** Invalid data persists, buttons stay disabled forever, or raw stack traces appear.

### 12. Manual log ingestion

- **Start:** Default project; Diagnostics open.
- **Action/input:** Under POST LOG SIGNAL enter Service checkout-api, Level Error, Message prod database connection pool timeout QA log; click Post Log. Repeat with a second distinct message.
- **Expected UI:** Accepted feedback identifies the persisted source; rescan remains available.
- **Expected backend/persistence:** logs.json has both records, timestamps, source, level and message. API returns 202 and location.
- **Bug indicators:** Success with no file record, changed message, or insertion into the wrong project.

### 13. Manual metric ingestion

- **Start:** Default project; Diagnostics open.
- **Action/input:** Enter Service checkout-api, Metric name request_error_rate, Value 45, Unit percent; click Post Metric. For API use Qa-Metric 'checkout-api' 'request_error_rate' 45.
- **Expected UI:** Accepted feedback; metric is available to a subsequent scan.
- **Expected backend/persistence:** metrics.json has the matching service/environment series and sample. Unit is not a persisted analysis dimension in the current metric contract; do not assume conversion.
- **Bug indicators:** No sample despite success, service/environment mixed with another series, or percent treated as 0.45 without explicit conversion.

### 14. Healthy signals do not alert

- **Start:** Fresh service healthy-qa; no prior signals for it.
- **Action/input:** Qa-Log 'healthy-qa' 'Information' 'prod checkout request completed successfully'; Qa-Metric 'healthy-qa' 'request_error_rate' 1; Qa-Metric 'healthy-qa' 'p95_latency' 100. Run Manual Refresh.
- **Expected UI:** No candidate for healthy-qa. Other preexisting incidents may remain.
- **Expected backend/persistence:** Healthy samples persist, but no newly detected incident/candidate is created for this service.
- **Bug indicators:** Every ingested signal becomes an incident, or healthy data is discarded merely because it does not alert.

### 15. Logs trigger detection

- **Start:** New service logs-qa in default project.
- **Action/input:** Send two Error logs with messages prod database connection timeout first and prod database connection timeout second, then scan.
- **Expected UI:** A logs-based candidate shows actual matched signals and source.
- **Expected backend/persistence:** Candidate timeline contains scan started, candidate detected and scan completed; no human confirmation yet.
- **Bug indicators:** Candidate invented without matching logs, created before count threshold, or automatically resolved/confirmed.

### 16. Metrics trigger detection

- **Start:** New service metrics-qa.
- **Action/input:** Qa-Metric 'metrics-qa' 'request_error_rate' 45; scan.
- **Expected UI:** Error-rate candidate with the measured value and severity chosen by detection rules.
- **Expected backend/persistence:** Candidate references the metric; recent confirmed history is unchanged until confirmation.
- **Bug indicators:** Candidate cites an unrelated metric, has fabricated timestamps, or immediately creates approved knowledge.

### 17. Threshold boundaries

- **Start:** Separate services for each value; within ten-minute detection window.
- **Action/input:** Send request_error_rate values 24, 25 and 26 to below-qa, equal-qa and above-qa, respectively. Scan. Also test p95_latency 999, 1000, 1001 using new services.
- **Expected UI:** Error rate alerts only above its threshold; inspect the exact greater-than rule and displayed threshold. Latency behaves according to the same configured comparison.
- **Expected backend/persistence:** Values are not averaged across unrelated services; evidence carries the tested value.
- **Bug indicators:** Boundary value unexpectedly alerts, threshold label disagrees with configuration, or old samples invalidate the test. Use fresh service names to avoid contamination.

### 18. Background monitoring

- **Start:** Resume Scanning; interval 10s; recent failing signal.
- **Action/input:** Close the app tab for 25 seconds, reopen Monitor.
- **Expected UI:** Last Scan advances even while no browser is open; candidates are present.
- **Expected backend/persistence:** Server-side scan records persist independently of browser polling.
- **Bug indicators:** Detection requires an open tab or multiple clients cause duplicate independent scan loops.

### 19. Manual refresh

- **Start:** Monitor open; note current Last Scan.
- **Action/input:** Click Manual Refresh once and watch loading/disabled state.
- **Expected UI:** Timestamp advances after completion; scan result reports actual source count, signals, errors and duration.
- **Expected backend/persistence:** One requested scan is recorded, with completed/failed state and corresponding details.
- **Bug indicators:** Success shown before completion, button stays disabled, or last-scan time is generated only by the browser.

### 20. Pause monitoring

- **Start:** Monitor scanning at 10s.
- **Action/input:** Click Pause Scanning; note Last Scan; wait 25 seconds.
- **Expected UI:** Button becomes Resume Scanning; status is Paused; timestamp stops advancing after any already-running scan finishes.
- **Expected backend/persistence:** monitoring.json records disabled. Existing incidents are retained.
- **Bug indicators:** New scheduled scans continue indefinitely after pause, or pause deletes candidates.

### 21. Resume monitoring

- **Start:** Paused from case 20.
- **Action/input:** Click Resume Scanning and wait up to 25 seconds.
- **Expected UI:** Status becomes active/enabled; scans resume; button changes back.
- **Expected backend/persistence:** Enabled setting persists; new scan records appear.
- **Bug indicators:** UI says active but backend remains paused, or interval resets unexpectedly.

### 22. Polling/settings persistence

- **Start:** Set slider to 20s and pause.
- **Action/input:** Refresh page, then stop/restart only the QA script. Reopen Monitor.
- **Expected UI:** 20s interval and paused state are restored.
- **Expected backend/persistence:** monitoring.json preserves both settings across process restart.
- **Bug indicators:** Browser defaults overwrite saved server settings or corrupt settings silently appear normal. A corrupt file must report that defaults are in use.

### 23. Last-scan timestamps

- **Start:** At least one completed scan.
- **Action/input:** Note time, refresh page, switch projects, inspect their Last Scan, then manually refresh one project.
- **Expected UI:** Displayed time belongs to the stored relevant scan; no scan is shown as Not run until one actually runs.
- **Expected backend/persistence:** Scan history preserves timestamp and project associations.
- **Bug indicators:** Refreshing fabricates a new scan time or shows another project's scan as current.

### 24. Candidate creation

- **Start:** Fresh metrics service candidate-qa.
- **Action/input:** Send request_error_rate 45, then Manual Refresh and open Incidents → Candidates.
- **Expected UI:** Candidate has title, SEV, source, detected time and evidence preview; operational decision buttons are available.
- **Expected backend/persistence:** Candidate record exists separately from confirmed incident history.
- **Bug indicators:** Candidate disappears on reload or looks like a completed model investigation before confirmation.

### 25. Confirm candidate

- **Start:** Unconfirmed candidate from 24.
- **Action/input:** Click Confirm once; wait for analysis.
- **Expected UI:** Confirmed incident opens/appears; analysis state is completed or explicitly failed. Candidate is no longer an open candidate.
- **Expected backend/persistence:** Confirmation and analysis events are stored; same candidate is not repeatedly converted to new incidents.
- **Bug indicators:** Confirmation loses incident on model failure or repeated clicks create duplicates.

### 26. Ignore candidate

- **Start:** Fresh service ignore-qa with failing metric.
- **Action/input:** Scan; find candidate; click Ignore. Refresh page and scan unchanged evidence.
- **Expected UI:** Candidate leaves open triage; ignored state is retained where visible.
- **Expected backend/persistence:** Decision persists; it is not trusted reusable incident knowledge.
- **Bug indicators:** Same unchanged candidate immediately returns as new or ignored data contributes approved knowledge.

### 27. False-positive candidate

- **Start:** Fresh service false-positive-qa with two error logs.
- **Action/input:** Scan; mark candidate False positive. Inspect stored candidate and future related analysis.
- **Expected UI:** Decision is explicit and the open queue clears that candidate.
- **Expected backend/persistence:** False-positive records remain excluded from trusted similarity/learning inputs.
- **Bug indicators:** False positive is treated as a successful mitigation or learned as approved knowledge.

### 28. Duplicate detection

- **Start:** Confirmed incident for duplicate-qa with request_error_rate evidence.
- **Action/input:** Post matching service/environment/signals and scan twice without changing evidence.
- **Expected UI:** Existing candidate/incident relationship is shown; repeated scans do not inflate the queue.
- **Expected backend/persistence:** Stable fingerprint/duplicate reference is retained instead of creating one record per scan.
- **Bug indicators:** Candidate count grows on every scan with unchanged input.

### 29. Duplicate merge

- **Start:** A candidate visibly offers Merge with a linked incident.
- **Action/input:** Click Merge and inspect the target incident's timeline.
- **Expected UI:** Candidate leaves open queue; merge identifies the existing incident rather than inventing a new analysis.
- **Expected backend/persistence:** Merged decision and linked incident ID persist; original incident remains.
- **Bug indicators:** Merge creates an unrelated incident, erases target history or marks the duplicate as trusted standalone knowledge.

### 30. Searching incidents

- **Start:** Several differently titled/services incidents exist.
- **Action/input:** Search QA checkout in Incidents; clear it. Search the same title in History; also search a service and displayed assignee.
- **Expected UI:** Matching rows only; clearing restores results; count/pagination reflect the filter.
- **Expected backend/persistence:** Search is read-only; IDs/statuses unchanged.
- **Bug indicators:** Stale filters hide new mutations, email-only searches are required to find an owner, or clearing fails.

### 31. Every status filter

- **Start:** Have candidate, confirmed, active, mitigated, resolved records; recovered only if produced by a supported recovery path.
- **Action/input:** Click each queue filter; in History select Confirmed, Active, Mitigated, Resolved, Recovered and All statuses.
- **Expected UI:** Each filter matches its label. Recovered may legitimately be empty; do not fabricate it or expect a manual Recover button.
- **Expected backend/persistence:** Stored statuses are unchanged by filtering.
- **Bug indicators:** Resolved appears under Mitigated, filter count contradicts rows, or empty filter looks like loading forever.

### 32. Severity filtering

- **Start:** Cases 10 complete.
- **Action/input:** In History select each SEV-1 through SEV-5, then All severities.
- **Expected UI:** Only matching badges appear; other filters combine rather than reset silently.
- **Expected backend/persistence:** Read-only operation; stored severity unchanged.
- **Bug indicators:** No SEV-5 option, invalid labels, or severity inferred from title instead of contract.

### 33. Pagination

- **Start:** At least 12 saved incidents; generate extra QA page N incidents if needed.
- **Action/input:** Open History; click Next/Previous. Change search/filter while on page two.
- **Expected UI:** Ten rows per page; valid ranges; filter returns to an available page; disabled controls match boundaries.
- **Expected backend/persistence:** No records lost or duplicated; API load is capped at latest 100 and UI states that scope in analytics.
- **Bug indicators:** Empty page after filtering, duplicate rows between pages, or claims of all-time counts beyond the loaded data.

### 34. Incident detail

- **Start:** Known incident from case 9.
- **Action/input:** Open it from queue and History; inspect header, evidence, provider details, ownership, timeline and close.
- **Expected UI:** Title, SEV, lifecycle, owner, project, times, investigation and actions can be located; closing restores focus.
- **Expected backend/persistence:** Opening does not mutate status or acknowledgement.
- **Bug indicators:** Detail shows another row's data, raw object strings, unlabelled controls, or lost keyboard focus.

### 35. Assignment

- **Start:** An unassigned incident; dev user or signed-in real responders registered.
- **Action/input:** Open detail; choose a named responder, Save assignment; then Assign to me; then Unassign.
- **Expected UI:** Selected owner updates everywhere after save; clear returns Unassigned.
- **Expected backend/persistence:** Stable subject ID persists; timeline records each change. Entering an unknown arbitrary assignee through API is rejected.
- **Bug indicators:** Names are stored in place of IDs, unknown identities accepted, or toast says saved while owner remains unchanged.

### 36. Friendly identity display

- **Start:** Normal Auth0 account with an actual name/preferred_username/nickname claim; directory populated by sign-in.
- **Action/input:** Assign and acknowledge an incident. Check account summary, picker, queue, detail and timeline. Also test an account whose only name is its email.
- **Expected UI:** Friendly available claim is preferred. Email-only/opaque identity falls back to Responder; the app does not invent a person's name from an email local part.
- **Expected backend/persistence:** Raw stable ID and historical audit actor remain intact. Display resolution is a presentation concern.
- **Bug indicators:** Raw email/auth0 subject shown as the human label, unauthorized identity changes, or all audit identities overwritten with friendly names.

### 37. Acknowledge

- **Start:** Unacknowledged incident with an existing owner.
- **Action/input:** Change the picker without saving, then click Acknowledge. Refresh and inspect.
- **Expected UI:** Acknowledgement records the viewer and time, while ownership/status stay unchanged.
- **Expected backend/persistence:** First acknowledgement actor/time are preserved; repeated acknowledgement does not overwrite them.
- **Bug indicators:** Acknowledgement silently assigns edited picker value, starts work, resolves incident, or overwrites first actor.

### 38. Start investigation

- **Start:** Confirmed incident.
- **Action/input:** Click Start work.
- **Expected UI:** Lifecycle becomes Active; timeline records work started; evidence remains visible.
- **Expected backend/persistence:** Status active and updated timestamp persist.
- **Bug indicators:** Acknowledgement/owner reset or runbook proposal prematurely published.

### 39. Mitigate

- **Start:** Active incident.
- **Action/input:** Click Mark mitigated.
- **Expected UI:** Mitigated label uses cyan/teal, distinct from green Resolved; incident remains open for follow-up.
- **Expected backend/persistence:** Status mitigated and timeline event persist; no infrastructure command is executed.
- **Bug indicators:** App claims remediation executed, closes investigation as resolved, or generates approved knowledge automatically.

### 40. Compare lifecycle colors everywhere

- **Start:** At least one mitigated and one resolved incident.
- **Action/input:** In both themes inspect counters, queue rows, History, detail badges, action buttons, timeline dots, status filter chips and Current lifecycle chart.
- **Expected UI:** Mitigated is cyan/teal; Resolved green; labels remain present. Confirmed purple and Active amber remain readable.
- **Expected backend/persistence:** Presentation only; lifecycle values stay distinct in API.
- **Bug indicators:** Same computed color for mitigated/resolved, unreadable text, or color alone conveys status.

### 41. Resolve

- **Start:** Active/mitigated incident with no previous proposal.
- **Action/input:** Click Resolve.
- **Expected UI:** Resolved state appears; detail opens and clearly shows Runbook proposal generated · Pending review; queue has a review entry.
- **Expected backend/persistence:** Status resolved, one pending proposal and generation timeline event persist; nothing is automatically approved.
- **Bug indicators:** No proposal, hidden failure, duplicate proposal on one click, or draft trusted before approval.

### 42. Reopen

- **Start:** Resolved incident with a proposal.
- **Action/input:** Click Reopen; refresh.
- **Expected UI:** Active state and reopened timeline entry appear. Existing proposal/review is preserved rather than silently replaced.
- **Expected backend/persistence:** Same incident ID and original proposal ID remain.
- **Bug indicators:** History is reset, proposal disappears, or reopening creates a second incident.

### 43. Timeline accuracy

- **Start:** Complete assignment, acknowledgement, work, mitigate, resolve, reopen sequence.
- **Action/input:** Compare visible timeline with incidents.json or recent API.
- **Expected UI:** Events retain chronological times, accurate labels/actors, semantic dots and actual summaries.
- **Expected backend/persistence:** Each successful mutation adds its intended event; failed mutations add no fake success event.
- **Bug indicators:** Missing events, fabricated current timestamps, repeated acknowledgements overwriting first actor, or unreadable actor IDs.

### 44. AI analysis

- **Start:** Fresh evidence and common manual input; QA provider running.
- **Action/input:** Create and confirm incident; repeat once in the normal app with actual external provider available.
- **Expected UI:** Analysis completes with explicit provider/model, confidence and limitations. QA model is visibly fixture/model-v1.
- **Expected backend/persistence:** Result stores provider, evidence, timings and UsedFallback=false on success.
- **Bug indicators:** Synthetic fixture output is represented as proof of live provider availability, or success appears without a completed state.

### 45. Known facts

- **Start:** Analyzed incident with injected logs/metrics.
- **Action/input:** Read Verified operational facts; compare every fact to the injected data and its evidence references.
- **Expected UI:** Facts cite actual operational evidence. User description alone is explicitly context.
- **Expected backend/persistence:** References resolve to stored evidence, not invented IDs.
- **Bug indicators:** Unsupported fact, unrelated service data, or user claim presented as verified telemetry.

### 46. Hypotheses

- **Start:** Same incident.
- **Action/input:** Read each hypothesis and compare confidence/inference strength to evidence.
- **Expected UI:** Hypotheses are distinguishable from known facts; uncertain causes remain qualified.
- **Expected backend/persistence:** Grounded references persist alongside hypothesis text.
- **Bug indicators:** Hypothesis called confirmed root cause without evidence, or a malformed model response creates facts.

### 47. Unknowns and missing evidence

- **Start:** Create No-evidence QA for service no-evidence-qa without its logs/metrics.
- **Action/input:** Read Unknowns & validation gaps.
- **Expected UI:** Missing logs/metric window is explicit; uncertainty is not disguised with decorative success states.
- **Expected backend/persistence:** No invented operational samples are stored.
- **Bug indicators:** Empty evidence yields high certainty or fabricated log messages.

### 48. Recommendations

- **Start:** Evidence-backed incident.
- **Action/input:** Inspect each recommended action, rationale and evidence/source; compare runbook steps.
- **Expected UI:** Actions are specific to available evidence; generic validation is identified when evidence is missing.
- **Expected backend/persistence:** SupportingSignals reference stored sources. UI does not execute infrastructure actions.
- **Bug indicators:** Ungrounded destructive recommendation shown as performed, or citations point to nonexistent evidence.

### 49. Confidence

- **Start:** Compare full-evidence and no-evidence incidents.
- **Action/input:** Read headline confidence and Analysis quality; inspect provider failure incident.
- **Expected UI:** Low evidence produces cautious confidence; failed analysis does not imply successful high-confidence reasoning.
- **Expected backend/persistence:** Confidence and quality agree with stored result/limitations.
- **Bug indicators:** Always-high confidence or contradictory failure/success labels.

### 50. Evidence mapping

- **Start:** Evidence-backed incident and RAG match.
- **Action/input:** Expand saved evidence and compare every fact/action reference against evidence source IDs; copy evidence once.
- **Expected UI:** Copied text is the actual selected evidence; references are traceable to logs, metrics or runbook section.
- **Expected backend/persistence:** Copying changes clipboard only; record unchanged.
- **Bug indicators:** Missing references, duplicate fabricated sources, or copy takes another record's text.

### 51. Provider/model disclosure

- **Start:** Successful live or QA analysis plus an explicit fallback case.
- **Action/input:** Open Provider & retrieval diagnostics.
- **Expected UI:** Shows actual provider/model and embedding/vector provider. QA says fixture model. Fallback identifies local analysis.
- **Expected backend/persistence:** Stored transparency agrees with actual selected path.
- **Bug indicators:** A failed external provider is labelled the producer of local output, or provider fields use unexplained unknown/? values.

### 52. Execution diagnostics

- **Start:** Successful, failed and historical incident records.
- **Action/input:** Expand Execution diagnostics in each. Inspect timings, fallback stage, reason and timeout source.
- **Expected UI:** Captured timings are formatted in ms, including valid zero; absent data says not captured or Not available; no raw enums/objects/question mark placeholder.
- **Expected backend/persistence:** No synthetic timing values are written merely for display.
- **Bug indicators:** undefined, null, [object Object], unexplained ?, raw .NET class names or fabricated timing.

### 53. Normal structured output

- **Start:** QA default provider and fresh incident title without special failure phrase.
- **Action/input:** Create a normal incident; inspect http://127.0.0.1:5299/__requests in browser or Invoke-RestMethod.
- **Expected UI:** Successful result; no structured retry/fallback notice when neither happened.
- **Expected backend/persistence:** One normal model attempt for this title; valid schema persisted.
- **Bug indicators:** Unnecessary retry, false fallback, or invalid response accepted.

### 54. Provider failure

- **Start:** QA started without --fallback; evidence exists.
- **Action/input:** Create Model unavailable checkout test; inspect saved incident. Restart normal provider state by using a normal title for another incident.
- **Expected UI:** Incident is saved with analysis unavailable; understandable provider error and Retry analysis action. No recommendations fabricated.
- **Expected backend/persistence:** analysisState failed, failure timeline event; provider call failure does not delete incident.
- **Bug indicators:** Raw stack trace, empty success view, or local analysis runs with fallback disabled.

### 55. Timeout

- **Start:** QA without --fallback; 5-second model limit.
- **Action/input:** Create Model timeout checkout test. Wait for deadline; observe UI and API response.
- **Expected UI:** Bounded failure, clear timeout and saved incident. No endless spinner. If testing live, do not deliberately disrupt the external provider.
- **Expected backend/persistence:** Failure event records timeout; no analysis text accepted after cancellation.
- **Bug indicators:** Late provider response overwrites failed state, unbounded wait, or timeout claims local fallback ran when disabled.

### 56. Malformed JSON

- **Start:** QA without --fallback; fresh evidence.
- **Action/input:** Create Malformed always checkout test. Inspect error and provider request list for that title.
- **Expected UI:** Failure after two structured attempts; error explains invalid model JSON without BytePositionInLine or .NET parser exception.
- **Expected backend/persistence:** No malformed content becomes evidence; saved incident has failed analysis and no implicit deterministic replacement.
- **Bug indicators:** Third/unbounded retry, arbitrary text repair, fake evidence, raw parser details, or fallback despite disabled setting.

### 57. Bounded retry and recovery

- **Start:** QA without --fallback; fresh evidence.
- **Action/input:** Create Malformed once checkout test. Inspect its entries in provider /__requests. For a saved failed incident use Retry analysis; an always-failing title should remain failed truthfully.
- **Expected UI:** Once-malformed case completes and discloses structured-output retry, not local fallback; repeated failure remains understandable.
- **Expected backend/persistence:** Two model attempts for the once-malformed request; schema remains requested on both. Repeated user retries are separate explicit actions.
- **Bug indicators:** No retry notice, schema discarded after malformed content, unbounded automatic attempts, or success claims on another failed retry.

### 58. Explicit local fallback

- **Start:** Stop QA script; restart with node e2e/scripts/manual-qa.mjs --fallback. Fresh checkout-api evidence.
- **Action/input:** Create Malformed always fallback test or Model unavailable fallback test.
- **Expected UI:** Local fallback is clearly labelled; actual available evidence is used; unsupported claims remain unknown.
- **Expected backend/persistence:** UsedFallback=true, attempted model, reason, duration and failure stage are persisted. No normal-app settings change.
- **Bug indicators:** Local output labelled model-backed, fallback invents telemetry, or absence of evidence still produces a confident incident analysis.

### 59. Fallback disabled

- **Start:** Stop QA script and restart without --fallback.
- **Action/input:** Repeat Malformed always disabled test and Model unavailable disabled test; compare with case 58.
- **Expected UI:** Incident remains saved but analysis fails; no local output substitutes for the provider.
- **Expected backend/persistence:** UsedFallback is not silently set through an application-layer bypass; no generated deterministic model success is stored.
- **Bug indicators:** Same successful local result appears despite disabled setting or failure loses the incident record.

### 60. Truthful fallback diagnostics

- **Start:** Both successful external-fixture and fallback records exist.
- **Action/input:** Inspect provider disclosure, execution diagnostics, history provider column and saved JSON for each.
- **Expected UI:** The actual result producer and attempted provider are distinguishable. Failure reason does not say local analysis ran when it did not.
- **Expected backend/persistence:** Stored UsedFallback, provider/model and reason agree with UI.
- **Bug indicators:** Contradictory Model completed and local fallback labels, fallback enabled globally by a test, or missing reason.

### 61. Multi-turn investigation

- **Start:** Successful analyzed incident with nonempty session ID.
- **Action/input:** Open detail and click Continue session.
- **Expected UI:** Analysis form opens with that Session ID and cleared editable incident fields; original record remains visible in History.
- **Expected backend/persistence:** No mutation occurs until follow-up submission.
- **Bug indicators:** Continuing silently overwrites the original analysis or creates a new unrelated session before submission.

### 62. Follow-up question/context

- **Start:** Continue-session form from 61.
- **Action/input:** Title QA follow-up checkout recovery; same service/environment; SEV-3; description Error rate is declining after reduced concurrency. What evidence should verify recovery? Submit.
- **Expected UI:** A new linked analysis appears with the follow-up context and current evidence.
- **Expected backend/persistence:** New incident/turn record links to existing session; original title/description preserved.
- **Bug indicators:** Follow-up response uses unrelated session, drops submitted context, or overwrites original severity.

### 63. Session continuity

- **Start:** At least two analyses in the same session.
- **Action/input:** Open each in History; use Session filter Linked incidents and the specific session entry.
- **Expected UI:** Original/follow-up labels and linked count agree; earlier context is available.
- **Expected backend/persistence:** Session turn increases; session store holds continuity, not a shared global conversation across unrelated sessions.
- **Bug indicators:** Other users/projects' unrelated contexts mix in, count wrong, or turn resets unexpectedly.

### 64. Session ID behavior

- **Start:** Detail with a session and a failed-before-analysis incident.
- **Action/input:** Copy session ID; compare pasted value with the full displayed ID. Start another incident with blank Session ID.
- **Expected UI:** Full ID copies without truncation; a new independent investigation receives a different ID. Missing IDs must not be presented as usable sessions.
- **Expected backend/persistence:** Session identity stays stable for follow-ups and distinct for independent incidents.
- **Bug indicators:** Copy yields none/undefined, all incidents share one session, or empty-session follow-up falsely claims continuity.

### 65. Action worked

- **Start:** Successful analysis visible in Analysis tab.
- **Action/input:** In Action outcome tracking enter Reduced checkout concurrency after approval; choose worked; click Log.
- **Expected UI:** Outcome appears with worked status and time; visible notification confirms storage.
- **Expected backend/persistence:** Action outcome persists on incident; it records human input rather than executing that action.
- **Bug indicators:** Action disappears after refresh, fabricated success timestamp, or infrastructure is changed by merely logging an outcome.

### 66. Action partially worked

- **Start:** Same analysis.
- **Action/input:** Enter Restarted one worker; latency improved but errors persist; choose partial; Log.
- **Expected UI:** Partial outcome is distinct from worked/failed and remains in history.
- **Expected backend/persistence:** Stored status partial; later learning can include qualified result.
- **Bug indicators:** Partial is normalized to worked, silently excluded, or treated as full resolution.

### 67. Action failed

- **Start:** Same analysis.
- **Action/input:** Enter Raised connection cap; errors increased; choose failed; Log.
- **Expected UI:** Failed outcome visibly warns; timeline/history retain it.
- **Expected backend/persistence:** Failed action can be surfaced as a caution in trusted future history after appropriate approval.
- **Bug indicators:** Failed action becomes recommended solely because it was attempted, or data is dropped.

### 68. Analysis feedback

- **Start:** Analysis tab with Rate this analysis.
- **Action/input:** Expand it; choose Useful, Correct, reason missing evidence, context QA feedback, then Save feedback. Repeat on other incidents with Partially Useful and Not Useful. Try saving with a missing rating.
- **Expected UI:** Both ratings required. Saved state and notification appear; duplicate submission is prevented for that form.
- **Expected backend/persistence:** Feedback record retains ratings, tags, comments and timestamp.
- **Bug indicators:** Feedback persists without required ratings, submitted text leaks as executable markup, or success shown without storage.

### 69. Recommendation feedback

- **Start:** Another incident analysis; feedback form open.
- **Action/input:** Choose recommendation correctness Wrong or Partially Correct; enter the specific recommendation and explain the issue; save.
- **Expected UI:** Feedback explicitly identifies recommendation and correctness, separately from analysis usefulness.
- **Expected backend/persistence:** Recommendation description and reason tags are stored with the incident.
- **Bug indicators:** Wrong recommendation rating changes incident lifecycle or is confused with action outcome.

### 70. Connect a Markdown source

- **Start:** QA runbook file from setup exists; admin in RAG.
- **Action/input:** Expand Connect a runbook source. Name QA checkout runbooks, Type Local / mounted directory, Directory path C:\Users\Aseel\source\repos\IncidentResponseAgent\.tmp\manual-qa\connected-runbooks; connect.
- **Expected UI:** Source appears with real path, enabled/reachable state and controls. A nonexistent directory gives a real error.
- **Expected backend/persistence:** sources.json stores registration; source file is not copied into fabricated integration storage.
- **Bug indicators:** Success for nonexistent path, claims of hosted Git integration when only a local working tree is supported, or duplicate sources without warning.

### 71. Synchronize/index source

- **Start:** Connected source from 70.
- **Action/input:** Click its Sync; wait for completion. Inspect source error/status and RAG search.
- **Expected UI:** Success only after backend synchronization; errors are visible. Section counts must not substitute for proof of successful retrieval.
- **Expected backend/persistence:** Vector index contains enabled source chunks; last sync updates on successful sync.
- **Bug indicators:** Unavailable embedding provider produces a green reindexed message, stale chunks remain after file change, or source registration disappears.

### 72. Search runbook

- **Start:** Source enabled and synchronized.
- **Action/input:** Search checkout connection pool recovery in RAG.
- **Expected UI:** Matching real sections with source path and relevance score; no fabricated document.
- **Expected backend/persistence:** Search may update index for changed sources; no incident is created.
- **Bug indicators:** Wrong path, ungrounded text, empty successful result despite exact content in a healthy index without explanation.

### 73. Retrieval diagnostics

- **Start:** Successful search plus an unavailable-embedding test.
- **Action/input:** Compare embedding provider/model, vector store, RAG status and degradation reason.
- **Expected UI:** Working path says available; failed path explicitly unavailable/degraded. Metadata is secondary but readable.
- **Expected backend/persistence:** Diagnostics correspond to actual provider/index result, not only configuration presence.
- **Bug indicators:** Configured key treated as proof of healthy service, unexplained ?, or raw filesystem exception shown as a success value.

### 74. Disable source

- **Start:** Custom source enabled; unique phrase searchable.
- **Action/input:** Click Disable, then repeat search.
- **Expected UI:** Source says Disabled; its unique document no longer appears after the next index refresh.
- **Expected backend/persistence:** Source registration retained with enabled=false; physical Markdown remains.
- **Bug indicators:** Disabled source still used as trusted evidence or file deleted without request.

### 75. Re-enable and sync

- **Start:** Disabled source from 74.
- **Action/input:** Click Enable, then Sync and search again.
- **Expected UI:** Enabled state and matching document return after successful synchronization.
- **Expected backend/persistence:** Registration is updated, not duplicated; index refresh includes it.
- **Bug indicators:** No Enable path, duplicate registration, or permanent loss of source after disabling.

### 76. Remove source

- **Start:** Disposable custom source; local file backed up.
- **Action/input:** Click Remove; cancel once, then confirm. Search its unique phrase.
- **Expected UI:** Cancel preserves it. Confirm removes source from list and subsequent retrieval; built-in configured source is not removable.
- **Expected backend/persistence:** Registry/index exclude source after refresh; underlying file remains.
- **Bug indicators:** File deleted by source removal or removed source stays retrievable without being registered elsewhere.

### 77. No runbook match

- **Start:** New query zzquasar-unrelated-qa with no overlap; high relevance threshold if necessary in isolated QA only.
- **Action/input:** Search it and create an unrelated no-evidence incident.
- **Expected UI:** Useful no-match message; absence of matches is not proof no runbook exists; analysis reports limited context.
- **Expected backend/persistence:** RunbookMatches empty; no fake default document is inserted.
- **Bug indicators:** Always returns a canned checkout runbook regardless of relevance, or no matches treated as provider failure.

### 78. Runbook does match

- **Start:** Restore/register known checkout runbook; evidence batch fresh.
- **Action/input:** Create QA checkout connection pool incident and inspect Runbook matches/actions.
- **Expected UI:** Relevant sections appear with citations; recommendations trace to actual text.
- **Expected backend/persistence:** Retrieved section IDs correspond to enabled source chunks.
- **Bug indicators:** Recommendations cite disconnected/rejected sources or unsupported commands.

### 79. Generate knowledge on resolution

- **Start:** New analyzed incident with logged action outcome; no existing proposal.
- **Action/input:** Resolve it.
- **Expected UI:** Clear Pending review state; generation event is visible.
- **Expected backend/persistence:** A pending proposal with title/content/generated time exists; approval/review fields unset.
- **Bug indicators:** No proposal despite successful first resolution, empty silent state, or automatic publication.

### 80. Locate the proposal

- **Start:** Resolved incident from 79.
- **Action/input:** Close detail, go to Incidents, use Pending runbook proposals → Review for that incident. Also locate in History.
- **Expected UI:** Both paths open the same editable proposal; no need to guess where it went.
- **Expected backend/persistence:** Same proposal ID/content in both views.
- **Bug indicators:** Review link opens another incident, banner absent, or only a transient toast exposes the proposal.

### 81. Edit proposal

- **Start:** Pending proposal; admin.
- **Action/input:** Replace/edit text to include: QA approved checkout pool recovery. Verified: two connection timeout logs and request_error_rate 45. Action: reduced concurrency; outcome partially worked. Next: verify error rate below 25 before resolution. Do not claim actions you did not record.
- **Expected UI:** Textarea is editable; content is retained while the review confirmation is open/cancelled. Text is not published until approval.
- **Expected backend/persistence:** Edited content is persisted with review decision, not as autosave before approval.
- **Bug indicators:** Edits disappear on a cancelled confirmation, are silently published, or nonadmin can change saved content.

### 82. Reject proposal

- **Start:** Pending edited draft dedicated to rejection.
- **Action/input:** Click Reject draft, cancel once, then confirm.
- **Expected UI:** Rejected state; no approval controls; clear not-reusable explanation.
- **Expected backend/persistence:** Review status rejected; no trusted approved Markdown remains; similarity excludes rejected incident knowledge.
- **Bug indicators:** Rejected content appears in future RAG or a cancel action persists a rejection.

### 83. Generate another proposal

- **Start:** Rejected proposal retained; create a separate evidence-backed incident QA approved learning checkout.
- **Action/input:** Resolve the new incident.
- **Expected UI:** A new pending proposal for the new incident. Re-resolving an existing incident preserves its existing proposal; it is not a regenerate command.
- **Expected backend/persistence:** New unique proposal ID only for the new incident.
- **Bug indicators:** Rejecting permanently prevents other incidents from learning, or every resolve duplicates an existing draft.

### 84. Approve proposal

- **Start:** Pending proposal from 83; admin.
- **Action/input:** Edit with the verified recovery text, click Approve and publish, inspect confirmation, then confirm once.
- **Expected UI:** Approved state only after publication succeeds; publishing error remains visible and retryable rather than reporting success.
- **Expected backend/persistence:** Approval and published status stored after Markdown publication; review timestamp set.
- **Bug indicators:** Approval saved before failed publication, duplicate publication from double click, or silent write failure.

### 85. Verify Markdown file

- **Start:** Successful approval from 84.
- **Action/input:** Read proposal ID from API; remove hyphens; open .tmp/manual-qa/knowledge/approved-<id>.md.
- **Expected UI:** UI identifies the published filename; file contains approved edited text and provenance.
- **Expected backend/persistence:** Markdown frontmatter includes proposal/incident IDs and approval time; content matches saved review.
- **Bug indicators:** File missing/truncated despite success, content from old draft, or generated draft published before approval.

### 86. Locate published knowledge in RAG

- **Start:** Published proposal and file exist.
- **Action/input:** Click View published runbook in RAG from detail.
- **Expected UI:** RAG opens with proposal title as query; real source/path shown for returned approved sections.
- **Expected backend/persistence:** Configured knowledge folder is included as a source; no fictitious new integration added.
- **Bug indicators:** Navigation only changes tab without querying, document path is invented, or generic old runbook mistaken for the approved file.

### 87. Verify index, including failure

- **Start:** Published file; RAG open.
- **Action/input:** Search distinctive approved text; inspect matching approved filename/section. For failure, use deterministic embedding outage query rag unavailable, then return to normal query.
- **Expected UI:** Successful retrieval verifies indexed knowledge. Outage reports unavailable/degraded; publication and index availability remain distinct.
- **Expected backend/persistence:** File remains published during transient provider outage. A later healthy query can rebuild/recover index without local fallback.
- **Bug indicators:** UI says Indexed solely because file exists, outage deletes approved knowledge, or recovery requires manual process restart unnecessarily.

### 88. Later related incident

- **Start:** Approved file successfully retrieved; fresh checkout-api telemetry.
- **Action/input:** Create QA recurring checkout connection pool timeout with matching tags/service/environment.
- **Expected UI:** Later analysis includes relevant runbook and/or approved similar incident, with evidence links.
- **Expected backend/persistence:** New analysis is separate; approved history and actions may be reused, rejected/deleted history excluded.
- **Bug indicators:** No relationship despite matching healthy retrieval, or prior incident overwritten.

### 89. Verify approved knowledge retrieval

- **Start:** Later incident from 88.
- **Action/input:** Inspect its stored runbookMatches/evidence and RAG source; compare approved file content.
- **Expected UI:** At least a relevant approved section is traceable when its query/relevance qualifies; not merely any checkout runbook.
- **Expected backend/persistence:** Evidence refers to the published approved document/section. If threshold filters it, inspect score and query before declaring a bug.
- **Bug indicators:** Claims of learned knowledge without matching source evidence, or unrelated old runbook passed off as the new approved document.

### 90. Exclude pending and rejected knowledge

- **Start:** Have separate pending, rejected and approved proposals with distinguishable content.
- **Action/input:** Search unique pending/rejected phrases; run a later incident referencing them. Inspect RAG paths and trusted similar incidents.
- **Expected UI:** Pending/rejected drafts do not appear as reusable runbooks; approved content may appear.
- **Expected backend/persistence:** Only approved publication is indexed. Human draft records remain in incident storage.
- **Bug indicators:** Pending/rejected text gets published/indexed or used as trusted similar incident guidance.

### 91. Delete/history removal

- **Start:** Disposable incident with known ID; preserve other incidents.
- **Action/input:** Open detail → Delete incident. Cancel once; then confirm deletion. Refresh History.
- **Expected UI:** Cancel preserves it; confirm removes it with explicit feedback and closes detail.
- **Expected backend/persistence:** Record is removed; associated approved knowledge is removed where applicable; other incidents in same session survive.
- **Bug indicators:** Deletion affects unrelated records/files, deleted row returns after refresh, or UI claims deletion after API failure.

### 92. Deleted history excluded from reuse

- **Start:** Deleted approved incident from 91; another related incident retained.
- **Action/input:** Run a new related analysis and search for deleted approved filename/unique content.
- **Expected UI:** Deleted incident and its published document are absent from future similarity/retrieval after index refresh.
- **Expected backend/persistence:** Store search and Markdown source exclude deleted record; no resurrected cached match.
- **Bug indicators:** Deleted content stays trusted because vector cache was not refreshed.

### 93. Evaluation scenarios

- **Start:** QA app; Evaluation tab.
- **Action/input:** Compare listed scenarios to GET /api/evaluation/scenarios. Click Run for a chosen scenario; inspect submitted severity/service/environment/tags.
- **Expected UI:** Only API-provided scenarios appear; exact scenario inputs populate/run analysis. Empty catalog shows an empty state.
- **Expected backend/persistence:** Creates a real test analysis using supplied catalog fields; no guessed defaults or invented scenario IDs.
- **Bug indicators:** UI substitutes service based on title text, always uses SEV-2, or presents nonexistent scenarios.

### 94. Analytics empty state

- **Start:** Fresh isolated QA directory or a newly created empty project.
- **Action/input:** Open Incidents → Incident overview.
- **Expected UI:** No saved incidents message; no fake chart bars/dates; candidate count can differ because charts exclude candidates.
- **Expected backend/persistence:** GET only; counters/charts reflect scoped saved records.
- **Bug indicators:** Example/demo data inserted to fill charts or zero falsely labelled unavailable.

### 95. Analytics populated

- **Start:** At least three incidents with different severities/statuses/creation dates.
- **Action/input:** Inspect creation-date, severity and lifecycle distributions; compare counts to recent API for selected project.
- **Expected UI:** Labels and numerical counts present; compact bars readable; UTC date basis and latest-100 limit disclosed.
- **Expected backend/persistence:** Counts derive from saved incidents, not candidates or failed HTTP requests; missing timestamps excluded with notice.
- **Bug indicators:** Wrong totals, implicit all-time claims, or charts invent acknowledgement/resolution averages unsupported by timestamps.

### 96. Analytics after lifecycle mutation

- **Start:** Chart has one active incident identified.
- **Action/input:** Mark it mitigated, then resolved; return to overview after each.
- **Expected UI:** Current lifecycle counts move between categories; creation-date/severity counts remain stable.
- **Expected backend/persistence:** Same record, new status; no additional incident created by status change.
- **Bug indicators:** Totals increase on mutation or resolved and mitigated remain counted simultaneously for one record.

### 97. Analytics after project switch

- **Start:** QA Alpha has data; another project empty.
- **Action/input:** Switch between projects and Global view.
- **Expected UI:** Charts and project caption refresh together; empty project has no inherited bars.
- **Expected backend/persistence:** API scoping respected; selector persists.
- **Bug indicators:** Previous project's chart is displayed under another project's name, or mixed-project counts without Global view label.

### 98. Light theme

- **Start:** Populated queue/detail and RAG diagnostics.
- **Action/input:** Use Toggle theme to select light; inspect all tabs and colored controls, severity, lifecycle, charts, errors and disabled buttons.
- **Expected UI:** Readable foreground/background, distinct surfaces, visible focus/separators; no decorative SEV marker.
- **Expected backend/persistence:** Theme saved under incidentops.theme; data unchanged.
- **Bug indicators:** Light theme retains dark-only surfaces, faint text, same mitigated/resolved color or inaccessible hover states.

### 99. Dark theme

- **Start:** Same populated app.
- **Action/input:** Toggle dark and repeat every tab, modal, form, proposal and chart; inspect native selects and scrollbar.
- **Expected UI:** Intentional dark surfaces, readable semantic text, coherent navbar and chart colors.
- **Expected backend/persistence:** Theme persisted; no data mutation.
- **Bug indicators:** Simple inversion causes unreadable pills, native controls stay white unexpectedly, or focus ring disappears.

### 100. Narrow/responsive view

- **Start:** Browser developer tools at 390×844, then 768px and desktop width.
- **Action/input:** Navigate queue/history, open detail/proposal, enter form data and scroll modal.
- **Expected UI:** No page-wide horizontal overflow; controls remain reachable; tables may use intentional local scrolling; modal close remains available.
- **Expected backend/persistence:** Resizing does not submit/reset forms or mutate incidents.
- **Bug indicators:** Clipped action buttons, inaccessible offscreen modal controls, labels overlapping values or chart forcing page overflow.

### 101. Keyboard-only navigation

- **Start:** Mouse unused; incident queue loaded.
- **Action/input:** Tab through navigation/forms; activate with Enter/Space; open detail, cycle Tab/Shift+Tab, close with Escape; open confirmation and cancel.
- **Expected UI:** Visible focus, meaningful order, focus contained within active modal and restored to opener. Skip link reaches workspace.
- **Expected backend/persistence:** No accidental mutations from focus; confirmation required for destructive action.
- **Bug indicators:** Focus escapes behind modal, lost focus after closure, button inaccessible or Escape confirms instead of cancelling.

### 102. Loading states

- **Start:** Developer tools Network throttling Slow 3G on isolated QA.
- **Action/input:** Refresh; submit analysis; run scan; navigate to RAG. Restore normal network after observing.
- **Expected UI:** Connecting/loading states are meaningful; actions cannot be accidentally repeated during pending work; no fake complete labels.
- **Expected backend/persistence:** One intended mutation per submission; final stored state agrees with UI.
- **Bug indicators:** Blank permanent pane, indefinite disabled button, duplicate operation or success before response.

### 103. Empty states

- **Start:** Fresh/empty project; no proposal/no outcomes/no runbook matches.
- **Action/input:** Visit all tabs and incident sections with no data.
- **Expected UI:** Each area explains what's absent and an appropriate next action; diagnostics say not captured where relevant.
- **Expected backend/persistence:** No placeholder records persisted to avoid empty UI.
- **Bug indicators:** undefined/null/? labels, fabricated metrics, or empty section resembling a broken render.

### 104. Backend error state

- **Start:** Isolated QA; use invalid source directory or invalid enum via API.
- **Action/input:** Attempt connection to a nonexistent directory; submit invalid status from API; inspect notifications and retry ability.
- **Expected UI:** Understandable failure, no success toast, form remains usable; legitimate server detail retained without stack trace.
- **Expected backend/persistence:** Failed mutation leaves existing valid data unchanged.
- **Bug indicators:** Error swallowed, draft lost, optimistic success not rolled back, or repeated silent failure.

### 105. Network/API failure

- **Start:** QA app loaded; DevTools Request blocking available.
- **Action/input:** Block *127.0.0.1:5298/api/* or temporarily stop only QA process. Navigate/read and try a non-destructive action. Unblock/restart and retry.
- **Expected UI:** Connection error shown; no stale success claim; app recovers on retry/reload. Authentication failure stays closed.
- **Expected backend/persistence:** No mutation assumed saved without response; inspect persistence before retrying uncertain writes.
- **Bug indicators:** UI reports Saved offline, shows stale data as refreshed, or cannot recover after service returns.

### 106. Notifications

- **Start:** Perform one successful assignment and one intentional validation/provider failure.
- **Action/input:** Read message wording, tone and placement; verify status/alert accessibility semantics.
- **Expected UI:** Success and failure clearly differ, explain what happened and what to do; do not obscure primary controls.
- **Expected backend/persistence:** Notification itself does not modify incident state.
- **Bug indicators:** Generic success for a failed action, repetitive contradictory toasts or raw exception text.

### 107. Dismiss/expiry of notifications

- **Start:** Toast visible from case 106.
- **Action/input:** Click its Dismiss button; trigger another and wait about 4.2 seconds.
- **Expected UI:** Clicked toast disappears immediately; another expires automatically; no invisible blocking overlay remains.
- **Expected backend/persistence:** No backend mutation; notification DOM removed.
- **Bug indicators:** Toast cannot be dismissed, remains indefinitely, traps focus or duplicates on every poll.

### 108. Refresh/persistence

- **Start:** Have assignment, acknowledgement, lifecycle, feedback, outcomes, review and project selection saved.
- **Action/input:** Refresh browser; stop/restart QA server; revisit all records.
- **Expected UI:** Saved data remains consistent; transient loading state clears; unsaved edits are not falsely represented as saved.
- **Expected backend/persistence:** Incident JSON, SQLite sessions and registries retain committed state.
- **Bug indicators:** Mutations exist only in browser memory or reload restores old/contradictory status.

### 109. Theme/settings persistence

- **Start:** Dark theme; QA Alpha selected; interval 20s; paused monitoring.
- **Action/input:** Refresh and restart; reopen app in same browser, then a separate fresh profile.
- **Expected UI:** Same-browser theme/project restored; server monitoring settings shared. Fresh profile can have theme defaults without altering server state.
- **Expected backend/persistence:** Browser preferences and persisted server settings keep distinct ownership.
- **Bug indicators:** One browser resets another's server settings or profile-specific theme treated as a server-wide preference.

### 110. Console errors

- **Start:** Clean browser Console before exercise.
- **Action/input:** Run normal navigation, form submit, modal review, theme toggle, charts and project switch. Filter for Error and React warnings.
- **Expected UI:** No uncaught errors or React controlled/uncontrolled warnings during normal operation.
- **Expected backend/persistence:** No hidden background exceptions while UI appears successful.
- **Bug indicators:** Uncaught promises, React DOM errors after repeated modal opening, or JSON parsing errors emitted by routine API responses.

### 111. Normal network requests

- **Start:** DevTools Network open, Preserve log; provider healthy.
- **Action/input:** Perform normal workflow from creation through review/search. Inspect red requests and status codes.
- **Expected UI:** Normal calls succeed; expected auth redirects and deliberately tested failures are distinguished from regressions.
- **Expected backend/persistence:** Request payloads match selected project/incident; successful UI action has corresponding accepted API result.
- **Bug indicators:** Silent 4xx/5xx loops, CSRF/auth failures during authorized actions, duplicate mutation requests or requests to stale endpoints.

### 112. No stale/contradictory state after mutations

- **Start:** A saved incident with chart/queue/history representations.
- **Action/input:** Assign, acknowledge, mitigate, resolve, edit/review proposal, reopen, switch tabs and refresh. Compare every representation with recent API.
- **Expected UI:** Counts, badges, detail, timeline and proposal state agree. Review success does not imply provider indexing succeeded until verified.
- **Expected backend/persistence:** Exactly intended changes persist; original metadata and evidence remain intact.
- **Bug indicators:** Queue says Active while detail says Resolved, charts lag indefinitely, approved label despite missing file, or failed mutation leaves optimistic success.

## Supplemental failure and security checks

- **Publication failure:** use the deterministic backend test `dotnet test IncidentResponseAgent.Tests --filter PublicationFailureKeepsProposalPendingAndCanBeRetried`. It injects a disk-write failure without damaging a real folder. Expected: pending review, publication failed, no approved knowledge, then successful retry. To inspect manually, use a disposable QA source on an unwritable test volume; never change permissions on the live knowledge folder. If you cannot safely create that condition, mark the interactive disk-failure branch BLOCKED, not PASS.
- **Unauthorized admin requests:** use a real responder Auth0 session for UI checks and the signed-token backend test suite for every administrative endpoint. Hidden controls alone are not authorization. Do not put real bearer tokens into the guide or screenshots.
- **Global runbook scope:** source registration and the configured knowledge folder are shared by this application instance. Project history/log/metric scoping does not make runbooks private per project. Do not use separate projects as an organizational security boundary.
- **Learning revision behavior:** resolving an incident a second time preserves its existing proposal; there is no separate regenerate-draft control. Create a second incident to test another proposal. Historical approved records without captured publication metadata are labelled publication not verified.
- **Data deletion:** deleting an approved incident removes its generated knowledge document. Source removal only disconnects the source and does not delete original Markdown. Project removal is registry removal, not a full data purge.
- **Provider quota:** the live free embedding/model route is quota-limited. A quota failure should be visible and recover on a later request; do not enable fallback or increase spending to make a test look successful.

## Manual QA sign-off checklist

Enter PASS / FAIL / BLOCKED and an incident ID, screenshot, or observation for each row. Do not substitute automated coverage for your own manual execution.

| Workflow | Cases | Result | Evidence / notes |
| --- | --- | --- | --- |
| Startup, health and dependencies | 1–2 | | |
| Real authentication, roles and session expiry | 3 | | |
| Projects, switching, isolation, settings and removal | 4–8 | | |
| Manual incidents, all severities and validation | 9–11 | | |
| Ingestion, healthy signals and thresholds | 12–17 | | |
| Server monitoring, pause/resume and persistence | 18–23 | | |
| Candidate decisions, deduplication and merge | 24–29 | | |
| Search, filters, pagination and details | 30–34 | | |
| Assignment, friendly identities and acknowledgement | 35–37 | | |
| Lifecycle, semantic colors and timeline | 38–43 | | |
| Analysis, facts, hypotheses, unknowns and citations | 44–50 | | |
| Provider/diagnostic honesty and failures | 51–60 | | |
| Investigation session and follow-up | 61–64 | | |
| Action outcomes and both feedback ratings | 65–69 | | |
| Runbook connection, sync, search and removal | 70–78 | | |
| Proposal generation, discovery and editing | 79–81 | | |
| Reject/approve, Markdown publication and index verification | 82–87 | | |
| Future retrieval and excluded knowledge | 88–90 | | |
| Deletion and future similarity exclusion | 91–92 | | |
| Evaluation and real-data analytics | 93–97 | | |
| Themes, contrast, responsive layout and keyboard | 98–101 | | |
| Loading/empty/error/offline states and notifications | 102–107 | | |
| Persistence, console/network health and consistency | 108–112 | | |
| Publication failure and recovery (supplemental) | Supplemental | | |

Tester: __________  Date: __________  Commit/working-tree snapshot: __________
Browser/version: __________  Environment (QA/live): __________
Open bugs / blocked prerequisites: ________________________________________
