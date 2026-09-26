# Single-instance deployment preparation

This repository is prepared for a later portfolio deployment, not deployed. Read the [pre-change gap analysis](deployment-gap-analysis.md). Do not treat a successful build or `/health` as proof that Auth0, OpenRouter, Hugging Face or telemetry is working.

## Access and trust model

There is **one shared authorized responder workspace**. Project IDs group/filter telemetry and incidents; they are not security boundaries. Responders can read and operate on incidents in every project, reuse investigation sessions, ingest signals and control monitoring. Assignment is coordination, not ownership or an access grant. Runbooks are workspace-wide. Admin role is additionally required for project/source management, incident deletion and knowledge publication. This matches the controllers, stores and existing manual QA semantics. No tenant isolation is implemented or implied.

The identity provider must grant only intended responders the configured role or delegated scope. Admins are trusted operators: they can select local source paths and telemetry health URLs, so they can reach files/network locations accessible to the process. Do not give admin to untrusted users. Keep secrets outside source/knowledge directories and restrict container mounts and outbound networking. Arbitrary-source sandboxing, per-project ACLs, immutable audit identity and multitenancy are deliberately outside this pass.

**Bundled/sample operational telemetry is demo data unless a real provider is connected.** A configured JSON path or successful health request does not establish telemetry authenticity, freshness or completeness. The Compose image starts with empty telemetry paths and monitoring disabled; it does not seed synthetic logs as production evidence. Bundled runbooks are copied to the persistent knowledge directory on the first new volume initialization.

## Build and local production-mode smoke test

Prerequisites: Docker Engine with Linux containers and Compose v2. For source tests: .NET 10 SDK, Node 24 and Playwright Chromium.

```sh
cp .env.example .env
# Leave all credential fields empty for the local smoke test.
docker compose config --quiet
docker compose build
docker compose up -d
curl --fail http://127.0.0.1:8080/health
# Expect 401 with no token; readiness details are protected.
curl -i http://127.0.0.1:8080/ready
curl -i http://127.0.0.1:8080/api/projects
docker compose exec app id
docker compose logs --tail=50 app
docker compose down
```

The placeholder `.invalid` authority permits credential-free process startup, but cannot authenticate anyone. Expect `/health` 200, protected endpoints 401, and a nonzero app UID. No login or inference success is expected. Compose publishes only to host loopback, keeps Qdrant ports off the host, drops app capabilities, uses a read-only app filesystem and a bounded writable `/tmp`. The final image contains the compiled React UI and ASP.NET API, not Node/SDK tooling. Qdrant retains its upstream runtime user; enforcing a different UID requires volume-permission validation on the target Docker host.

For a real deployment, expose the app only through a TLS reverse proxy. Configure its exact host name, turn on HTTPS redirection, and supply the **immediate proxy IP** in `Security__KnownProxies__0` (additional entries numbered 1, 2, ...). Add these environment entries in a deployment-only Compose override. The proxy must replace incoming forwarding headers, support a request deadline consistent with the app, and enforce connection/body limits. The app accepts one forwarded hop and never trusts forwarded host. With no explicit proxy list, forwarding is disabled. Never use `ASPNETCORE_FORWARDEDHEADERS_ENABLED=true` as a shortcut.

## Configuration

ASP.NET hierarchical keys use `__` in environment variables. Compose aliases are listed where present; other settings can be supplied in an override file. None belong in Docker build arguments or frontend variables.

| Environment key / Compose alias | Purpose |
| --- | --- |
| `AllowedHosts` / `ALLOWED_HOSTS` | Semicolon-separated public host names without schemes or ports; default localhost and 127.0.0.1. Never `*` for deployment. |
| `Authentication__Authority` / `AUTHORITY` | HTTPS Auth0 tenant/custom-domain issuer. |
| `Authentication__Audience` / `AUDIENCE` | API audience identifier; must match issued tokens. |
| `Authentication__BrowserClientId` / `BROWSER_CLIENT_ID` | Confidential web application client ID. |
| `Authentication__BrowserClientSecret` / `BROWSER_CLIENT_SECRET` | Runtime secret for browser OIDC. Both browser fields are required for browser login. |
| `Authentication__RequiredScope` / `REQUIRED_SCOPE` | Default `incident_response`; do not set empty unless every authenticated account should have responder access. |
| `Authentication__RoleClaimType`, `NameClaimType`, `BrowserScope` | Defaults in appsettings.json. Roles default to `https://incidentresponseagent/roles`; scopes `openid profile email`. |
| `Authentication__AllowDevelopmentIdentity` | Must remain false in production; startup rejects true outside Development/Testing. |
| `Agent__IncidentAnalysis__ApiKey` / `OPENROUTER_API_KEY` | Runtime OpenRouter key. Legacy aliases `IRA_AGENT_API_KEY` and `OPENROUTER_API_KEY` also work outside Compose. |
| `Agent__IncidentAnalysis__Endpoint`, `Model` / `OPENROUTER_ENDPOINT`, `OPENROUTER_MODEL` | OpenAI-compatible model URL/model. Check current access/quota at deployment time. |
| `Agent__IncidentAnalysis__AnalysisTimeoutSeconds` | Default 75; bounded 5–180 by provider wrapper. |
| `Runbooks__SemanticRetrieval__EmbeddingProvider` / `EMBEDDING_PROVIDER` | `HuggingFace` by default; `OpenRouter` is also implemented. |
| `Runbooks__SemanticRetrieval__ApiKey` / `EMBEDDING_API_KEY` | Embedding-provider runtime key. HF aliases `HF_TOKEN`, `HF_API_TOKEN`; OpenRouter alias `OPENROUTER_API_KEY`. |
| `Runbooks__SemanticRetrieval__Endpoint`, `Model` / `EMBEDDING_ENDPOINT`, `EMBEDDING_MODEL` | HF defaults to router.huggingface.co/hf-inference/models/ and BAAI/bge-small-en-v1.5. For OpenRouter, explicitly set https://openrouter.ai/api/v1 and an accessible embedding model. |
| `Runbooks__SemanticRetrieval__EmbeddingTimeoutSeconds` | Default 30, bounded 3–60. |
| `Runbooks__SemanticRetrieval__QdrantEndpoint`, `QdrantApiKey`, `QdrantCollectionName`, `QdrantTimeoutSeconds` | Compose endpoint http://qdrant:6333, collection incident_runbook_chunks, default operation timeout 5 seconds. If using a managed/external Qdrant, require TLS and runtime authentication. |
| `Security__HttpsRedirection` / `HTTPS_REDIRECTION` | True by default in the app; false in loopback-only Compose smoke configuration. Enable behind TLS proxy. |
| `Security__KnownProxies__0` | Exact trusted immediate proxy IP. Forward limit 1; no implicit trust-all mode. |
| `RateLimits__General`, `Signals`, `Expensive`, `Readiness` | Per-caller requests/minute: 120, 60, 6, 12. Shared instance budgets are five times these, with no queues; expensive concurrency is 2, other categories 32. Positive overrides only. 429 includes Retry-After. |
| `Monitoring__Enabled`, `PollingIntervalSeconds`, `StartupDelaySeconds` | Compose disables monitoring until real telemetry is configured. Defaults outside Compose are true/30/5. |
| `Tools__OperationalData__SourceHealthEndpoint`, `SourceHealthTimeoutSeconds`, `Projects__N__...` | Optional real telemetry producer check and project-specific file/health settings. These are operator-managed targets, not arbitrary public input. |
| `SecretsDirectory` | Optional absolute runtime-mounted directory. Files named e.g. `Authentication__BrowserClientSecret` become configuration values and override other providers. Mount read-only; permit only app UID access. |

Keep local `.env` files untracked. Runtime environment values are visible to Docker administrators; prefer read-only secret file mounts for an actual deployment. Do not print `docker compose config` with secrets; `--quiet` validates without printing values. Never place credentials in URLs. The image context excludes `.env*`, certificates, keys, local settings, databases, logs, test output and caches. This pass uses no real credentials. Defaults keep analysis/embedding/vector fallback disabled; enabling fallbacks would produce explicitly degraded/demo results, not fix a provider outage.

## Auth0 changes required later (not performed)

Configure a Regular Web Application with the deployment origin. Set allowed callback URL to `https://HOST/signin-oidc`, post-logout callback to `https://HOST/signout-callback-oidc` and allowed logout return URL to `https://HOST/`. Set the appropriate web origin to `https://HOST` if required by the tenant. Authorize the API audience and delegated scope for this application. Emit the configured responder/admin role claim into ID/access tokens (the existing README contains the action example). Confirm access with both a responder and an account with no grants; then verify admin restrictions. Remove obsolete development callbacks as appropriate in that separate administrative task.

Browser authentication uses authorization code + PKCE, HttpOnly Secure SameSite=Lax cookies and no sliding expiration (8-hour local cookie ticket; OIDC token lifetime can shorten it). Mutations using browser cookies require `X-CSRF-TOKEN` obtained from `/auth/session`. Bearer requests do not use cookie CSRF. Test logout, expiration, callback failures and denied users against the real issuer. The UI/API are same-origin; no cross-origin CORS allowance is configured.

## Persistence and backups

| Named volume | Paths/content |
| --- | --- |
| `app_state` at `/state` | `incidents.json`, `responders.json` (derived beside incident records), `sessions.sqlite`, `runbooks.sqlite`, `projects.json`, `sources.json`, `monitoring.json`, `knowledge/` and publication history, `telemetry/logs.json`, `telemetry/metrics.json`. |
| `protection_keys` at `/keys` | ASP.NET data-protection key ring for browser cookies/antiforgery. Losing it invalidates existing cookies. Keys are not encrypted at rest by the application; protect/encrypt the volume and backups. |
| `qdrant_storage` | Qdrant collection storage at /qdrant/storage. |
| `qdrant_snapshots` | Qdrant snapshots at /qdrant/snapshots. |

All application paths are environment-configurable: `Storage__Incidents__IncidentRecordsPath`, `SessionDatabasePath`, `Storage__DataProtectionKeysPath`, `Runbooks__SemanticRetrieval__DatabasePath`, `SourceRegistryPath`, `KnowledgeBasePath`, `Tools__OperationalData__ProjectRegistryPath`, `LogEntriesPath`, `MetricSamplesPath`, `Monitoring__StatePath`. Every project-specific telemetry/source path must also be inside a persistent mount. A bind mount replacing a named volume must be writable by the image app UID (normally 1654); do not solve this by running the app as root.

**Exactly one API instance is supported.** JSON stores and process-local locks, local rate limits and SQLite are not a distributed consistency system. Do not scale replicas or run overlapping app versions on the same state volume. No automated retention/rotation or durable ingestion queue exists; telemetry appends rewrite JSON arrays and can eventually consume disk/memory despite rate/body limits.

For a consistent small-deployment backup, stop writers (app and telemetry ingestion), stop Qdrant for a cold volume backup or use its documented snapshot process, and back up all four volumes as one versioned set. Copying live SQLite/JSON files is not guaranteed consistent; include SQLite WAL/SHM when applicable. Encrypt backups, restrict access, and test restoration to disposable volumes. Qdrant snapshots alone omit incidents, sessions, knowledge publications and auth keys. Never run `docker compose down -v` against state you want to keep. Upgrading the image does not update previously initialized knowledge volumes automatically.

## Health/readiness contract

- `/health`: anonymous process liveness, 200 once the HTTP process is serving. No provider calls and exempt from application rate limits. Use this for the local container/process health check only.
- `/ready`: requires responder authorization. Qdrant `/readyz` is checked with a 2-second deadline and redirects disabled; 2xx means `reachable`, not proof of collection integrity. Persistence writes/flushes/deletes a small temporary file in required state parent directories; it does not certify database integrity or backups.
- `configured`: required settings/files exist, but provider execution or telemetry provenance was not tested. No paid model/embedding call or Auth0 login is performed by readiness. `unavailable`: missing settings, failed probe, or inaccessible persistence. `degraded`: incomplete verification or impaired subsystem.
- Readiness returns **503** for unavailable or unverified dependencies, including an otherwise configured setup. This is deliberately conservative; do not attach it as an unauthenticated load-balancer admission check. A future operational-readiness gate needs bounded, fresh evidence of successful authenticated provider work before it can truthfully return 200. Do not simply translate configured to healthy.

## Security controls and remaining gaps

Request bodies are limited to 64 KiB, headers to a 15-second deadline, controller requests to 240 seconds, plus provider-specific deadlines. Expensive analysis, confirm/retry, runbook search/sync/add, knowledge publication and manual scans share strict budgets. Signal ingestion has its own budget. Auth/authorization run before rate limiting so rejected identities never reach workspace operations; the edge must still limit unauthenticated connection floods. Background monitoring is bounded by its schedule rather than HTTP budgets.

Host filtering, trusted forwarding, production HSTS, CSP, nosniff, frame denial, no-referrer and restricted browser capabilities are enabled. CSP permits inline styles for the current UI, HTTPS profile images, and only same-origin scripts/connects. Cookie CSRF and authorization tests cover the request pipeline. API/provider failures retain failure status/category and safe remediation without copying raw provider response bodies or exception stacks into routine logs. Incident content is operationally sensitive and intentionally persisted; it is not a secret vault. Do not ingest credentials, tokens or personal data without a separate retention/redaction policy. Historical data/logs are not retroactively scrubbed.

Remaining deployment work: select/pin reviewed image digests, run the container/Compose and scanner jobs on Linux, configure TLS/DNS and exact proxy/host trust, mount protected secrets, validate real Auth0 role/scope mappings and provider quotas/model access, connect real telemetry, enable monitoring, measure resource limits, configure disk/backup/log-retention alerts and test restore. No high availability, full audit trail, tenant isolation, automatic failover or internet-scale abuse protection is claimed.

CI performs restore/build/tests, frontend build, deterministic Chromium flows, NuGet/npm audits, image build, local Compose smoke and container/secret scans. NuGet audit failures are errors. Version tags `vMAJOR.MINOR.PATCH` prepare a checksummed image archive as a GitHub artifact, with no registry push or deployment. No CD workflow is included. Any future CD must be a separate workflow using a protected GitHub environment with required human reviewers and narrowly scoped credentials; configure that approval gate before enabling it.
