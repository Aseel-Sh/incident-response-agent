# Deployment-readiness and security-hardening report

Repository: Aseel-Sh/incident-response-agent. Base commit: `a2d50b4deadab12315eaa7b33795a63e702ff9df`.

Implemented and tested in the isolated checkout, then applied to `C:\Users\Aseel\source\repos\IncidentResponseAgent` at the user's request. All 36 changed files matched the tested source archive before this location note and a Dockerfile trailing-blank-line cleanup. Tests listed below ran in the isolated checkout; they were not rerun for this identical transfer. No commit/push, deployment, cloud resource creation, external identity/provider setting change or real credential use. The source archive and patch contain the changes; the patch applies to the base commit. Review [deployment.md](deployment.md) before preparing an environment.

## Findings and fixes

- Added a multi-stage React/.NET 10 image, restricted build context, non-root API runtime, read-only app container, internal Qdrant network, and four named persistent volumes. Credentials are supplied only at runtime; optional key-per-file secrets are supported.
- Added distinct ASP.NET budgets for expensive analysis/retrieval/scans, signal ingestion, ordinary requests and readiness. Per-subject/IP and shared-instance budgets return 429 with Retry-After; expensive concurrency is capped at two. Body/header/request deadlines and runbook query bounds are explicit.
- Added explicit trusted-proxy configuration, restrictive default host filtering, HSTS/security headers, durable data-protection configuration, fixed cookie expiry, and safer API/provider error logging. HF response bodies no longer flow into exceptions; failure status remains visible. No fallback was enabled to hide a provider failure.
- Preserved JWT/OIDC, admin checks, browser CSRF and architecture. Expanded negative object-route tests, shared-workspace behavior tests, rate tests and provider-redaction tests.
- Object-level audit: projects are filters inside one authorized responder workspace, not tenant security boundaries. Responders share incidents/sessions/runbooks; assignment grants no access. Existing admin boundaries remain enforced. No intended per-project security boundary was found, so no multitenancy was invented.
- `/health` remains process liveness. Protected `/ready` performs a bounded 2-second Qdrant check and persistence-directory write probes. It labels configuration separately from reachability, omits raw monitoring error details, and returns 503 while external execution remains unverified. No paid inference or external login occurs in probes.
- Added read-only CI with pinned action commits, build/test/audit/container/Compose checks, and version-tag archive preparation. Removed write permissions from advisory React Doctor. No deployment workflow or registry push was added.
- Documented runtime configuration, deployment-time Auth0 callbacks/logout/roles, providers, paths, volumes, backup limits, one-instance operation, and demo telemetry provenance.

## Verification results

Windows host: .NET SDK 10.0.401/runtime 10.0.12; Node 24.13.1; Playwright 1.58.2/Chromium 145. Tests used only synthetic identities/provider fixtures.

For .NET commands the process-local `APPDATA`, `LOCALAPPDATA`, `DOTNET_CLI_HOME`, and `NUGET_PACKAGES` pointed to `.tmp/appdata`, `.tmp/localappdata`, `.tmp/dotnet`, `.tmp/nuget`. Playwright used `PLAYWRIGHT_BROWSERS_PATH=.tmp/browsers`. These scratch settings are not shipped.

| Command/check | Exact final result |
| --- | --- |
| `dotnet restore IncidentResponseAgent.slnx --configfile .tmp/NuGet.Config` | Exit 0; all six projects restored; all-transitive NuGet auditing enabled and audit errors/vulnerabilities configured as errors. |
| `dotnet build IncidentResponseAgent.slnx -c Release --no-restore` | Exit 0; 0 warnings, 0 errors. |
| `dotnet test IncidentResponseAgent.slnx -c Release --no-restore --logger 'trx;LogFileName=hardening.trx'` | Exit 0; **143 passed, 0 failed, 0 skipped**, final duration 36 seconds. Original suite had 123 tests; 20 cases added. |
| `npm ci --cache .tmp/npm-cache` | Exit 0; 209 packages installed; install audit reported 0 vulnerabilities. |
| `npm run build` | Exit 0; Vite 8.2.1, 36 modules transformed; production UI generated. |
| `npm audit --audit-level=low --json` | Exit 0; **0 vulnerabilities** at all severities. |
| `dotnet list IncidentResponseAgent.slnx package --vulnerable --include-transitive --no-restore --config .tmp/NuGet.Config --format json` | Exit 0; six projects, no reported vulnerable packages or audit problems. |
| `npx playwright install chromium` | Exit 0; Chromium, headless shell, FFmpeg and Windows support downloaded. |
| `npm run test:e2e -- --retries=0` | Exit 0; **27 passed**, 0 failed/skipped, 1.0 minute; includes authentication, core CRUD, monitoring, accessibility and mobile flows. |
| `dotnet publish IncidentResponseAgent.Api/IncidentResponseAgent.Api.csproj -c Release --no-restore -o .tmp/production-smoke` | Exit 0. |
| `node .tmp/production-smoke.mjs` | Exit 0; real loopback Kestrel in Production with invalid placeholder issuer and empty keys: `/health` 200, `/ready` 401, `/api/projects` 401, `/` 200. Process stopped afterward. Authenticated readiness failure/reachability are covered by backend tests. |
| `docker --config .tmp/docker compose config --quiet` | Exit 0; Compose configuration validates. |
| `docker --config .tmp/docker build -t incident-response-agent:local .` | Exit 1, **blocked**: named pipe `//./pipe/docker_engine` does not exist; no Docker daemon available. Image not built locally. |
| `docker --config .tmp/docker compose up -d --no-build` | Exit 1, **blocked** by the same absent daemon. No containers/resources created. Persistence across container restart and image vulnerability scan remain unverified. |
| `trivy 0.74.0 fs --scanners secret --exit-code 1 --skip-dirs .tmp,node_modules,.git,IncidentResponseAgent.Tests/bin,IncidentResponseAgent.Tests/obj --format json --output .tmp/secret-scan.json .` | Exit 0; **0 secret findings** in the working tree. Not a complete Git history scan. Release archive matched its published SHA256. |
| `trivy --cache-dir .tmp/trivy-cache --timeout 45s config --disable-telemetry --exit-code 1 --severity HIGH,CRITICAL --skip-dirs .tmp,node_modules,.git --format json --output .tmp/config-scan.json .` | Exit 0; Dockerfile detected, **20 successful checks, 0 failures** at requested severities. Trivy did not identify Compose as a supported config type; Compose was validated separately. |
| Microsoft container manifest checks | `dotnet/sdk:10.0` and `dotnet/aspnet:10.0` returned HTTP 200 from MCR. This is not an image-build result. |
| `git diff --check` | Exit 0; no whitespace errors after removing generated asset churn. |

Initial restore attempts failed on sandbox-denied user NuGet configuration and Windows Schannel credentials. A temporary loopback Node relay fetched only public api.nuget.org resources over certificate-validated HTTPS; NuGet signature metadata was preserved. No TLS validation or NuGet auditing was disabled in the successful restore. The relay/config are scratch-only, excluded from the patch/image. CI uses normal HTTPS NuGet directly.

The first browser run produced 19 passes, 1 failure and 7 serial dependents not run because it expected the old readiness vocabulary. The assertion was updated to require the new unavailable state **and** false execution-verification fields; the complete rerun passed. A later backend run caught an exception-type name in the new safe retrieval message; the message was corrected without changing that test. The final 143-test run passed. Existing tests were not removed or weakened. Browser-suite request quotas are increased only in its Testing fixture because it shares one process; production quotas have separate integration tests.

## Deliberately not changed and deployment blockers

- No per-project ACL/multitenancy: conflicts with the verified shared responder model. Untrusted users must not receive responder/admin access.
- Admin-configured filesystem sources and outbound telemetry health URLs remain trusted operator capabilities; no new source sandbox or network policy system was introduced.
- JSON/SQLite persistence, process-local locks, unbounded telemetry retention and local rate limits remain single-instance limitations. No distributed storage, queue, retention engine or horizontal scaling.
- Data-protection keys require protected/encrypted volumes/backups; application-level key encryption, immutable auditing and retroactive historical-log scrubbing are not implemented. CSP retains inline styles required by the current UI.
- Qdrant retains its upstream runtime user. Changing its UID requires target-volume permission validation.
- Readiness intentionally cannot return a verified all-dependencies-ready 200 based on settings alone. A later operational gate needs fresh successful provider execution evidence; this pass does not fake that evidence or spend provider quota.
- CI is authored but **not run remotely**. Container build, non-root volume permissions, Compose startup/restart, and image scan must pass on a Linux Docker runner before deployment approval. Base/scanner images use explicit major/version tags; select reviewed digests at release time.
- Later environment work: TLS/DNS/proxy and host settings; protected runtime secrets; Auth0 callbacks/logout/scope/roles and denied-account checks; real model/embedding/Qdrant access; authentic telemetry; monitoring enablement; backup/restore test; disk/log retention and resource-budget alerts. Bundled/sample telemetry is demo data until a real provider is connected.

## Files changed

- `.dockerignore`
- `.env.example`
- `.github/dependabot.yml`
- `.github/workflows/react-doctor.yml`
- `.github/workflows/verify.yml`
- `.gitignore`
- `Directory.Build.props`
- `Dockerfile`
- `IncidentResponseAgent.Agent/Incidents/MicrosoftAgentFrameworkIncidentAnalysisAgent.cs`
- `IncidentResponseAgent.Agent/Incidents/ResilientIncidentAnalysisAgent.cs`
- `IncidentResponseAgent.Api/Controllers/IncidentsController.cs`
- `IncidentResponseAgent.Api/Controllers/MonitoringController.cs`
- `IncidentResponseAgent.Api/Controllers/ReadinessController.cs`
- `IncidentResponseAgent.Api/Controllers/RunbooksController.cs`
- `IncidentResponseAgent.Api/Controllers/SignalsController.cs`
- `IncidentResponseAgent.Api/Program.cs`
- `IncidentResponseAgent.Api/Security/DeploymentSecurity.cs`
- `IncidentResponseAgent.Api/Services/DependencyReadinessProbe.cs`
- `IncidentResponseAgent.Api/Services/ServerIncidentMonitoringService.cs`
- `IncidentResponseAgent.Api/appsettings.json`
- `IncidentResponseAgent.Infrastructure/Incidents/LocalOperationalSignalMonitor.cs`
- `IncidentResponseAgent.Infrastructure/Runbooks/HuggingFaceRunbookEmbeddingProvider.cs`
- `IncidentResponseAgent.Infrastructure/Runbooks/QdrantRunbookVectorStore.cs`
- `IncidentResponseAgent.Infrastructure/Runbooks/ResilientRunbookEmbeddingProvider.cs`
- `IncidentResponseAgent.Infrastructure/Runbooks/SemanticRunbookRetrievalService.cs`
- `IncidentResponseAgent.Infrastructure/Tools/HttpOperationalSourceHealthProbe.cs`
- `IncidentResponseAgent.Tests/DeploymentSecurityTests.cs`
- `IncidentResponseAgent.Tests/HuggingFaceRunbookEmbeddingProviderTests.cs`
- `README.md`
- `docker-compose.yml`
- `docs/deployment-gap-analysis.md`
- `docs/deployment.md`
- `docs/hardening-report.md`
- `e2e/core-flows.spec.ts`
- `package.json`
- `playwright.config.ts`
