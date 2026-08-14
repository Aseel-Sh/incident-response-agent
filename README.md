# Incident Response Agent

Incident Response Agent is a .NET 10 incident investigation and workflow assistant. It detects or accepts incident candidates, lets an operator confirm or dismiss them, gathers operational evidence and runbooks, runs a Microsoft Agent Framework agent through OpenRouter, and persists the response and learning lifecycle.

The project is a modular monolith intended for single-instance use. External model analysis and hosted embeddings are the normal product path. Local prompt analysis, hashing embeddings, deterministic telemetry, and SQLite vector fallback are offline/demo escape hatches and are disabled by default; the application reports an unavailable provider instead of silently degrading.

It also includes a React frontend built with Vite and served by the API, plus optional Qdrant vector database support.

## What It Does

- Accepts incident submissions over HTTP.
- Validates incident title, description, severity, session id, service, environment, timestamp, and tags.
- Retrieves relevant Markdown runbook chunks using hybrid RAG.
- Stores runbook documents, chunks, and embedding vectors in SQLite.
- Uses Qdrant as the primary vector database when it is running locally.
- Can use SQLite vector search only when selected directly or explicitly enabled as a Qdrant fallback.
- Can use local hashing embeddings only when explicitly enabled for an offline demo.
- Searches local JSON-backed log samples.
- Queries local JSON-backed metric samples.
- Scans operational logs and metrics for likely incident candidates.
- Persists incident analysis records.
- Persists multi-turn investigation session state in SQLite.
- Uses a real Microsoft Agent Framework `ChatClientAgent` when an OpenRouter key is configured.
- Registers framework `AIFunction` tools for logs, metrics, runbooks, trusted prior incidents, prior action outcomes, similarity, and proposed knowledge drafts.
- Fails model analysis closed by default when OpenRouter is unavailable; local prompt fallback requires explicit opt-in.
- Supports project-scoped monitoring, sources, thresholds, incidents, and history.
- Persists responder assignment and acknowledgement with actor/timestamp timeline events.
- Authenticates API users with externally issued OIDC access tokens and derives audit actors from validated identity claims rather than request data.
- Supports responder/admin roles and a service catalog with ownership, on-call, escalation, dependencies, and runbook links.
- Preserves confirmed incidents when external analysis is unavailable and exposes an explicit retry workflow.
- Queries all symptom-relevant metric families in parallel rather than selecting only one signal.
- Separates process liveness (`/health`) from dependency readiness (`/ready`).
- Returns structured evidence, hypotheses, recommended actions, confidence, notes, and session context.
- Exposes RAG diagnostics so retrieval quality can be inspected directly.
- Exposes detected incident candidates so the frontend can self-report likely incidents.
- Includes rubric-based evaluation scaffolding and built-in evaluation scenarios.
- Returns structured `ProblemDetails` for invalid requests and provider failures.

## Architecture

The solution follows clean architecture boundaries:

- `IncidentResponseAgent.Api`
  HTTP controllers, request/response contracts, validation, configuration binding, OpenAPI, health, and error handling.

- `IncidentResponseAgent.Application`
  Use cases, orchestration, tool interfaces, persistence interfaces, runbook retrieval contracts, structured analysis parsing, and evaluation contracts.

- `IncidentResponseAgent.Domain`
  Framework-light business models such as `Incident`, `IncidentSeverity`, and `RunbookDocument`.

- `IncidentResponseAgent.Infrastructure`
  SQLite storage, Markdown runbook indexing, embedding providers, local log/metric providers, session persistence, and incident record persistence.

- `IncidentResponseAgent.Agent`
  Microsoft Agent Framework agent construction, OpenRouter/OpenAI-compatible transport, framework tools, structured-output validation, and local prompt fallback.

Dependency direction:

```text
Api -> Application, Infrastructure, Agent
Application -> Domain
Infrastructure -> Application, Domain
Agent -> Application, Domain
Domain -> none
```

## RAG Pipeline

Runbooks are authored as Markdown in:

```text
IncidentResponseAgent.Infrastructure/Runbooks/KnowledgeBase
```

At runtime the RAG service:

1. Loads Markdown runbooks.
2. Parses title, summary, metadata, and tags.
3. Chunks runbooks by headings and numbered steps.
4. Generates an embedding for each chunk.
5. Stores document metadata and an index copy in SQLite.
6. Upserts vectors and payloads to Qdrant when Qdrant is selected and available.
7. Reindexes when Markdown content, approved incident knowledge, or the embedding provider/model changes.
8. Retrieves chunks from the selected vector store; cross-provider fallback requires explicit opt-in.
9. Reranks with hybrid semantic and lexical scoring.
10. Returns top matches to the agent and API response.

Default RAG database:

```text
%LOCALAPPDATA%\IncidentResponseAgent\runbook-rag.sqlite
```

## Free Local Vector Database

Qdrant is the configured primary vector database. It is free and runs locally through Docker.

Start Qdrant:

```powershell
docker compose up -d qdrant
```

Qdrant endpoints:

```text
REST API: http://localhost:6333
Dashboard: http://localhost:6333/dashboard
gRPC: http://localhost:6334
```

The project uses:

```json
{
  "Runbooks": {
    "SemanticRetrieval": {
      "VectorStoreProvider": "Qdrant",
      "QdrantEndpoint": "http://localhost:6333",
      "QdrantCollectionName": "incident_runbook_chunks"
    }
  }
}
```

If Docker Desktop is not running or Qdrant is unavailable, RAG reports unavailable. Set `AllowLocalVectorStoreFallback=true` only when an intentional SQLite fallback is acceptable.

Qdrant docs:

- https://qdrant.tech/documentation/quickstart/
- https://api.qdrant.tech/api-reference/collections/create-collection
- https://api.qdrant.tech/api-reference/points/upsert-points
- https://api.qdrant.tech/api-reference/search/query-points

## Embeddings

The project supports two embedding paths:

- Local fallback: `local-hashing-384`
- Hosted Hugging Face feature-extraction model: `BAAI/bge-small-en-v1.5`

Hugging Face is the normal embedding path. If it is unreachable, rate-limited, or misconfigured, RAG reports `unavailable` by default. Set `Runbooks__SemanticRetrieval__AllowLocalEmbeddingFallback=true` only for an intentional offline/demo run. Likewise, Qdrant does not silently fall back to SQLite unless `AllowLocalVectorStoreFallback` is explicitly enabled; selecting `VectorStoreProvider=SQLite` directly remains supported for a local deployment.

Get a Hugging Face token:

- https://huggingface.co/settings/tokens
- https://huggingface.co/docs/hub/security-tokens

Set it with environment variables:

```powershell
$env:HF_TOKEN = "your-hugging-face-token"
$env:HF_EMBEDDING_MODEL = "BAAI/bge-small-en-v1.5"
```

Or in `IncidentResponseAgent.Api/appsettings.Development.json`:

```json
{
  "Runbooks": {
    "SemanticRetrieval": {
      "ApiKey": "your-hugging-face-token",
      "Model": "BAAI/bge-small-en-v1.5"
    }
  }
}
```

ASP.NET Core loads this file automatically when `ASPNETCORE_ENVIRONMENT=Development` (the checked-in development launch profile already sets it). Environment variables still take precedence, so `HF_TOKEN` and `HF_EMBEDDING_MODEL` can override the local development file without editing it.

Do not commit real keys.

## Microsoft Agent Framework and OpenRouter

The model-backed path is `MicrosoftAgentFrameworkIncidentAnalysisAgent`. It creates a Microsoft Agent Framework `ChatClientAgent` with `OpenAI.Chat.ChatClient.AsAIAgent(...)`, a framework `AgentSession`, `ChatClientAgentRunOptions`, structured JSON response format, and seven registered `AIFunction` tools:

- search logs;
- query metrics;
- retrieve runbook sections;
- retrieve human-approved prior incidents;
- retrieve prior action outcomes;
- check similar incidents;
- draft a proposed runbook/postmortem update.

Business logic stays behind application interfaces so it remains independently testable. The use case pre-gathers deterministic evidence for reliability, while the framework agent may call the same registered tools for additional context. Agent instructions explicitly prohibit invented logs, metrics, runbooks, prior incidents, outcomes, and evidence references.

OpenRouter supplies the model through its OpenAI-compatible API. No key is stored in source, logs, runtime configuration responses, or tests.

Get an OpenRouter key:

- https://openrouter.ai/settings/keys
- https://openrouter.ai/docs/api-reference/authentication

Set it with environment variables:

```powershell
$env:OPENROUTER_API_KEY = "your-openrouter-key"
$env:OPENROUTER_MODEL = "nvidia/nemotron-3-super-120b-a12b:free"
$env:OPENROUTER_TIMEOUT_SECONDS = "75"
$env:OPENROUTER_BASE_URL = "https://openrouter.ai/api/v1"
$env:OPENROUTER_SITE_URL = "https://your-app.example" # optional
$env:OPENROUTER_APP_NAME = "Incident Response Agent"  # optional
```

Or in `IncidentResponseAgent.Api/appsettings.Development.json`:

```json
{
  "Agent": {
    "IncidentAnalysis": {
      "Provider": "OpenRouter",
      "Model": "nvidia/nemotron-3-super-120b-a12b:free",
      "Endpoint": "https://openrouter.ai/api/v1",
      "ApiKey": "your-openrouter-key"
    }
  }
}
```

The older `IRA_AGENT_API_KEY`, `IRA_AGENT_MODEL`, and `IRA_AGENT_ENDPOINT` names remain compatible, but the `OPENROUTER_*` variables above are preferred and override default model/base-URL settings.

If no key is configured, or the OpenRouter call and its relaxed structured-output retry both fail, the API returns a provider error; it does not silently replace the model with local analysis. RAG failure remains independently visible and does not select a local model. Free OpenRouter models can queue or time out, so the timeout remains bounded:

```json
{
  "Agent": {
    "IncidentAnalysis": {
      "AnalysisTimeoutSeconds": 75
    }
  }
}
```

For an intentional offline demo only, set `Agent__IncidentAnalysis__AllowLocalAnalysisFallback=true`. The fallback still refuses to invent an analysis when there is no log, metric, runbook, or approved prior-incident evidence.

Only approved proposed knowledge updates are written into the Markdown knowledge corpus and reindexed for later retrieval. Rejected proposals and deleted incidents remove their generated knowledge document. In the UI, proposed runbook/postmortem updates appear from History after an incident is resolved. After approval, the approved Markdown file is visible from the Runbooks/RAG source view and becomes searchable through runbook retrieval.

Verification commands:

```powershell
dotnet test IncidentResponseAgent.Tests/IncidentResponseAgent.Tests.csproj
npm.cmd run test:e2e:ai
npm.cmd run test:e2e:learning
```

Run the 12-scenario evaluator against a running API:

```powershell
node evaluation/run-evaluation.mjs http://127.0.0.1:5155
```

For offline/local validation without real model or telemetry credentials, run the API with `Tools__OperationalData__UseDeterministicFallbacks=true`. The evaluator reports candidate-classification and prior-outcome reuse as `not measured` unless those campaigns are exercised separately; it does not fabricate those metrics.

During live testing on June 20, 2026, `nvidia/nemotron-3-super-120b-a12b:free` returned valid structured JSON in about 1.2 seconds. The previous `nex-agi/nex-n2-pro:free` route took about 31.5 seconds for a minimal request and repeatedly exceeded the incident-analysis timeout. Free model availability changes over time, so the model id remains configurable.

## Local Operational Data

The project includes sample operational signals. These are not logs from your PC and they are not coming from a live app yet. They are bundled JSON fixtures that let the product demonstrate monitoring, detection, RAG, and analysis without needing Azure, Datadog, Splunk, Prometheus, or another paid service.

```text
IncidentResponseAgent.Infrastructure/Tools/SampleData/logs.json
IncidentResponseAgent.Infrastructure/Tools/SampleData/metrics.json
```

These files are copied to the API output folder and used by:

- `LocalJsonLogSearchProvider`
- `LocalJsonMetricsProvider`
- `LocalOperationalSignalMonitor`

If no local data matches a query, the app now returns an empty log or metric result by default so it does not invent operational evidence. You can enable deterministic fallback log and metric samples for demos:

```json
{
  "Tools": {
    "OperationalData": {
      "UseDeterministicFallbacks": true
    }
  }
}
```

When the API runs, those files are copied into the API output folder and read from there by default:

```text
IncidentResponseAgent.Api/bin/Debug/net10.0/Tools/SampleData/logs.json
IncidentResponseAgent.Api/bin/Debug/net10.0/Tools/SampleData/metrics.json
```

The current detector is therefore in sample mode. It detects incidents from those files because no real application log stream or metric backend has been connected yet.

## Automatic Incident Detection

People usually learn about incidents from alerts, dashboards, log errors, metric threshold breaches, customer reports, support tickets, or manual escalation from another team. This app now supports both paths:

- automatic detection from logs and metrics
- manual incident submission when a human spots something the monitor missed

The local monitor scans the configured free operational data sources and turns suspicious signals into incident candidates. Today it detects:

- high `request_error_rate` for `checkout-api`
- elevated `request_error_rate` for `auth-api`
- high `queue_depth` for `orders-worker`
- error/warning log patterns such as `500`, `timeout`, `latency`, `backlog`, and `failure`

Detection is exposed through:

```http
GET http://localhost:5155/api/incidents/detected
```

The frontend calls this endpoint on load and refreshes it every 30 seconds. Each detected card can be copied into the manual incident form or analyzed immediately.

Configure detection thresholds in `appsettings.Development.json`:

```json
{
  "Tools": {
    "OperationalData": {
      "HighErrorRateThreshold": 25,
      "CriticalErrorRateThreshold": 40,
      "QueueDepthWarningThreshold": 700,
      "MaxDetectedIncidents": 10
    }
  }
}
```

Override paths in `appsettings.Development.json`:

```json
{
  "Tools": {
    "OperationalData": {
      "LogEntriesPath": "C:\\data\\incident-response-agent\\logs.json",
      "MetricSamplesPath": "C:\\data\\incident-response-agent\\metrics.json"
    }
  }
}
```

To connect your own local data without adding a new provider, create JSON files with the same shape as the sample files and point `LogEntriesPath` / `MetricSamplesPath` at them. To monitor a real service directly, add an implementation of `ILogSearchProvider` and `IMetricsProvider` for the system you use, such as Prometheus, Elasticsearch, OpenSearch, Splunk, or plain files written by your app.

You can also push signals into the app over HTTP. This is the simplest free live-monitoring path for another local service:

```http
POST http://localhost:5155/api/signals/logs
Content-Type: application/json

{
  "source": "checkout-api",
  "level": "Error",
  "message": "HTTP 500 during checkout",
  "correlationId": "corr-123"
}
```

```http
POST http://localhost:5155/api/signals/metrics
Content-Type: application/json

{
  "metricName": "request_error_rate",
  "serviceName": "checkout-api",
  "environment": "production",
  "value": 42.6
}
```

The Monitor tab scans those files and self-reports candidates when thresholds or log patterns match.

## Persistence

Incident records are stored as JSON by default:

```text
%LOCALAPPDATA%\IncidentResponseAgent\incident-records.json
```

Session state is stored in SQLite:

```text
%LOCALAPPDATA%\IncidentResponseAgent\incident-sessions.sqlite
```

Runbook RAG data is stored in SQLite:

```text
%LOCALAPPDATA%\IncidentResponseAgent\runbook-rag.sqlite
```

You can override session and RAG database paths:

```json
{
  "Runbooks": {
    "SemanticRetrieval": {
      "DatabasePath": "C:\\data\\incident-response-agent\\runbook-rag.sqlite",
      "KnowledgeBasePath": "C:\\data\\incident-response-agent\\runbooks"
    }
  },
  "Storage": {
    "Incidents": {
      "SessionDatabasePath": "C:\\data\\incident-response-agent\\incident-sessions.sqlite"
    }
  }
}
```

## Run The API

From the repository root:

```powershell
dotnet run --project IncidentResponseAgent.Api --launch-profile http
```

Default local URL:

```text
http://localhost:5155
```

## Authentication and responder identity

> Deferred setup: the authentication implementation remains in the API, but live Auth0 tenant configuration and end-to-end provider validation are intentionally paused until after the frontend migration. Development/test identity remains the supported local workflow in the meantime.

Production authentication is configured for the Auth0 Free hosted tier. Create one Auth0 **Regular Web Application** and one Auth0 API whose identifier is `https://incident-response-agent-api`. The repository already supplies the audience, scopes, claim names, PKCE flow, secure cookie, and CSRF handling; only the account-specific Auth0 values remain external:

Store the three Auth0-generated values with .NET User Secrets instead of editing `appsettings.json`:

```powershell
dotnet user-secrets set "Authentication:Authority" "https://YOUR_TENANT.us.auth0.com/" --project IncidentResponseAgent.Api
dotnet user-secrets set "Authentication:BrowserClientId" "YOUR_AUTH0_REGULAR_WEB_APP_CLIENT_ID" --project IncidentResponseAgent.Api
dotnet user-secrets set "Authentication:BrowserClientSecret" "YOUR_AUTH0_REGULAR_WEB_APP_CLIENT_SECRET" --project IncidentResponseAgent.Api
dotnet run --project IncidentResponseAgent.Api --launch-profile https
```

Configure these URLs in the Auth0 application:

```text
Allowed Callback URL: https://localhost:7104/signin-oidc
Allowed Logout URL:   https://localhost:7104/
Allowed Web Origin:   https://localhost:7104
```

Create the `incident_response` permission on the Auth0 API and create `responder` and `admin` roles. Add this Auth0 Post Login Action to the Login flow so roles are available to both browser sessions and bearer clients:

```javascript
exports.onExecutePostLogin = async (event, api) => {
  const claim = "https://incidentresponseagent/roles";
  const roles = event.authorization?.roles || [];
  api.idToken.setCustomClaim(claim, roles);
  api.accessToken.setCustomClaim(claim, roles);
};
```

Assign each operator at least `responder`; use `admin` only for operators who may delete incidents, modify projects/runbook sources, and approve knowledge. The browser uses Authorization Code with PKCE and sends Auth0 the configured API audience. The server holds the session in a secure HTTP-only cookie and validates antiforgery tokens for mutations, so tokens are not stored in JavaScript. JWT bearer authentication remains available for API clients. Development identity is disabled in base settings and should only be enabled by an explicit local/test override.

Service ownership is configured under `ServiceCatalog:Services`. A matching incident service automatically receives its on-call target or owning team as the initial assignee:

```json
{
  "ServiceCatalog": {
    "Services": [{
      "ServiceName": "checkout-api",
      "OwningTeam": "checkout-team",
      "OnCallTarget": "checkout-oncall",
      "EscalationPolicy": "page primary, then platform lead after 10 minutes",
      "RunbookUrl": "https://internal.example/runbooks/checkout",
      "Dependencies": ["payments-api"]
    }]
  }
}
```

Rotate any provider credentials that were previously stored in plaintext development configuration and move them to user-secrets, environment variables, or a secret manager.

## Frontend

The incident console is a React 19 application under `frontend/`, built with Vite and served by ASP.NET Core at:

```text
http://localhost:5155
```

The frontend lets you:

- scan logs and metrics for detected incident candidates
- submit an incident
- analyze a detected incident with one click
- switch between Monitor, Analyze, Runbooks, History, and Sources tabs
- filter and sort detected incidents
- use dark mode
- reuse a session id for follow-up turns
- inspect structured analysis output
- run RAG searches directly
- view recent persisted analyses
- see where runbooks, logs, metrics, and vectors are coming from
- see embedding/vector store status

Install and build the frontend before running or publishing the API:

```powershell
npm.cmd install
npm.cmd run build
```

For frontend development with hot reload, run the ASP.NET Core HTTPS profile and Vite in separate terminals:

```powershell
dotnet run --project IncidentResponseAgent.Api --launch-profile https
npm.cmd run dev
```

Vite serves the development UI at `http://localhost:5173` and proxies API, authentication, health, and readiness requests to ASP.NET Core. End-to-end preparation builds React automatically.

## Endpoints

Health:

```http
GET http://localhost:5155/health
```

Provider and dependency readiness (returns `503` with per-component details when required external services or production persistence are not configured):

```http
GET http://localhost:5155/ready
```

Assign or acknowledge an incident:

```http
PUT http://localhost:5155/api/incidents/{incidentId}/coordination
Content-Type: application/json

{ "assignee": "checkout-oncall", "actor": "aseel", "acknowledge": true }
```

Retry an unavailable analysis:

```http
POST http://localhost:5155/api/incidents/{incidentId}/analysis/retry
```

Analyze an incident:

```http
POST http://localhost:5155/api/incidents/analyze
Content-Type: application/json

{
  "title": "Checkout 5xx spike",
  "description": "Customers are seeing intermittent 500 responses during checkout.",
  "severity": "sev2",
  "serviceName": "checkout-api",
  "environment": "production",
  "tags": ["checkout", "5xx", "latency"]
}
```

Continue an investigation session:

```http
POST http://localhost:5155/api/incidents/analyze
Content-Type: application/json

{
  "sessionId": "existing-session-id",
  "title": "Checkout 5xx spike still active",
  "description": "Errors continue after rollback.",
  "severity": "sev2",
  "serviceName": "checkout-api",
  "environment": "production",
  "tags": ["checkout", "rollback"]
}
```

Get recent analyses:

```http
GET http://localhost:5155/api/incidents/recent?maxResults=5
```

Get detected incidents:

```http
GET http://localhost:5155/api/incidents/detected
```

Ingest a log signal:

```http
POST http://localhost:5155/api/signals/logs
Content-Type: application/json

{
  "source": "checkout-api",
  "level": "Error",
  "message": "HTTP 500 during checkout"
}
```

Ingest a metric signal:

```http
POST http://localhost:5155/api/signals/metrics
Content-Type: application/json

{
  "metricName": "request_error_rate",
  "serviceName": "checkout-api",
  "environment": "production",
  "value": 42.6
}
```

Inspect connected sources:

```http
GET http://localhost:5155/api/operations/sources
```

Inspect RAG retrieval:

```http
GET http://localhost:5155/api/runbooks/search?query=checkout%205xx%20latency&serviceName=checkout-api&environment=production&maxResults=5
```

The RAG diagnostics response includes:

- embedding provider
- embedding model
- database path
- knowledge base path
- matched runbook chunks
- section path
- score
- source file
- tags

## Response Shape

Incident analysis returns:

- `sessionId`
- `sessionTurnNumber`
- `sessionContextSummary`
- `incidentSummary`
- `analysisText`
- `retrievedEvidence`
- `rootCauseHypotheses`
- `recommendedActions`
- `confidence`
- `notes`

The model is instructed to return strict JSON. The application parses that JSON into typed response fields and preserves deterministic tool/RAG evidence as supporting context.

## Error Handling

The API returns structured `ProblemDetails` for common failure modes:

- `400` for invalid request arguments.
- `502` for rejected/failed model provider requests.
- `503` for missing configuration or unavailable external services.
- `500` for unexpected server errors.

Each problem response includes a `traceId` extension for log correlation.

## Build And Test

Build:

```powershell
dotnet build IncidentResponseAgent.slnx
```

Test:

```powershell
dotnet test IncidentResponseAgent.slnx --no-build
```

Current tests cover:

- incident request validation
- structured agent JSON parsing
- SQLite-backed RAG indexing and diagnostics
- fail-closed external embeddings plus explicitly opted-in offline fallback
- SQLite session persistence
- rubric evaluation
- built-in evaluation scenarios

## Evaluation

Evaluation lives in:

```text
IncidentResponseAgent.Application/Evaluation
```

The current evaluator is model-independent. It scores whether an analysis includes evidence, hypotheses, recommendations, confidence, expected evidence signals, and expected action themes.

Built-in scenarios include:

- `checkout-5xx-regression`
- `queue-backlog-growth`

## Troubleshooting

### Hugging Face DNS Or Network Failure

Symptom:

```text
api-inference.huggingface.co:443
```

Expected behavior:

The app logs the hosted embedding failure and reports RAG unavailable. For an intentional offline demo, explicitly enable `AllowLocalEmbeddingFallback` to use `local-hashing-384`.

### OpenRouter 401 Unauthorized

Check:

- the API key was copied correctly
- the key is active
- the model is available to your account
- `appsettings.Development.json` is not malformed
- environment variables are set in the same shell that starts the API

### RAG Results Look Stale

Delete the local RAG database and restart the app:

```powershell
Remove-Item "$env:LOCALAPPDATA\\IncidentResponseAgent\\runbook-rag.sqlite"
```

The app will rebuild the index from Markdown runbooks.

### No Real Logs Or Metrics

The project uses local JSON sample data by default. Replace or override the sample files to simulate your own operational signals.

## Extension Points

Good next engineering upgrades:

- Replace local JSON log search with a Splunk, Elasticsearch, Azure Monitor, or Datadog provider.
- Replace local JSON metrics with Prometheus, Azure Monitor, or Datadog metrics.
- Move incident record persistence from JSON file to SQLite or a relational database.
- Add OpenTelemetry traces for agent calls, tool calls, and RAG retrieval.
- Add real-time push updates with SignalR instead of frontend polling.
- Add more evaluation fixtures and regression tests.
- Add a dedicated vector database adapter if you want Qdrant, Chroma, LanceDB, or pgvector.

## Security Notes

- Do not commit real API keys.
- Prefer environment variables or `appsettings.Development.json` for local secrets.
- `appsettings.Development.json` is ignored by git.
- Treat model output as untrusted.
- Keep tool behavior bounded and read-only unless deliberately adding controlled write actions.
