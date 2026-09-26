# Deployment gap analysis (before implementation)

Inspected Program.cs, all API controllers, authentication integration tests, provider wrappers, persistence options and project/source documentation at main.

- Deployment: no Dockerfile/.dockerignore; Compose only starts Qdrant and exposes both ports. Frontend builds into API wwwroot. All projects target .NET 10.
- Security already present: JWT issuer/audience/signature/lifetime checks, secure HttpOnly browser cookie, OIDC code + PKCE, responder fallback policy, admin mutations, cookie antiforgery, development-identity production guard. No CORS policy: same-origin UI is intended.
- Missing: request rate/concurrency limits, explicit body limits, HSTS/security headers, explicit trusted proxy configuration, durable data protection keys. AllowedHosts is wildcard. Exception messages/logging can contain provider diagnostics.
- Authorization: project IDs are filters in a shared responder workspace, not tenant boundaries. Verified controller/store access and docs/manual-qa.md and docs/reliability-2026-09-16.md. Assignment does not confer access. Keep this model; expand negative endpoint tests.
- Readiness: process health is separate, but readiness returns success from keys/endpoints/paths alone. It does not contact Qdrant or prove provider availability. Bundled telemetry is synthetic.
- Persistence: JSON files, SQLite databases, project/source registries, monitoring state, responder directory and knowledge publications need durable writable paths. File locks are process-local: one instance only.
- CI: only advisory React Doctor workflow. Add deterministic build/test, dependency auditing, container build/scan and version-tag artifact preparation without deployment.
- Bounds: trusted admins can configure filesystem sources and telemetry health URLs. No tenant ACLs, distributed storage, provider administration or cloud deployment will be introduced. Docker CLI exists locally but daemon is unavailable at inspection time.
