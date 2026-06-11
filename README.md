# Incident Response Agent

Incident Response Agent is a .NET 10 backend for AI-assisted incident investigation. It accepts an incident report, retrieves relevant operational runbooks through a local RAG pipeline, queries log and metric tools, uses an OpenAI-compatible agent when configured, and returns structured response guidance.

The project is built as a portfolio-quality modular monolith. It is useful without paid services because it includes local embeddings, sample operational data, SQLite persistence, and a local prompt-based agent fallback. When API keys are added, it can use OpenRouter-compatible chat models and Hugging Face-hosted embeddings.

It also includes a static frontend served by the API and optional Qdrant vector database support for local, free vector search.

## What It Does

- Accepts incident submissions over HTTP.
- Validates incident title, description, severity, session id, service, environment, timestamp, and tags.
- Retrieves relevant Markdown runbook chunks using hybrid RAG.
- Stores runbook documents, chunks, and embedding vectors in SQLite.
- Uses Qdrant as the primary vector database when it is running locally.
- Falls back to SQLite vector search when Qdrant is unavailable.
- Falls back to local embeddings if Hugging Face is unavailable.
- Searches local JSON-backed log samples.
- Queries local JSON-backed metric samples.
- Scans operational logs and metrics for likely incident candidates.
- Persists incident analysis records.
- Persists multi-turn investigation session state in SQLite.
- Uses an incident analysis agent when a model key is configured.
- The agent receives deterministic runbook, log, and metric evidence gathered by the application, then returns strict JSON analysis.
- Falls back to a local prompt-based agent when no model key is configured.
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
  Incident analysis agent instructions, OpenAI-compatible agent execution, and local prompt fallback.

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
5. Stores document metadata and fallback vectors in SQLite.
6. Upserts vectors and payloads to Qdrant when Qdrant is available.
7. Reindexes when Markdown content or embedding provider/model changes.
8. Retrieves chunks from Qdrant first, then falls back to SQLite vector search.
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

If Docker Desktop is not running or Qdrant is unavailable, the app logs the failure and falls back to SQLite search.

Qdrant docs:

- https://qdrant.tech/documentation/quickstart/
- https://api.qdrant.tech/api-reference/collections/create-collection
- https://api.qdrant.tech/api-reference/points/upsert-points
- https://api.qdrant.tech/api-reference/search/query-points

## Embeddings

The project supports two embedding paths:

- Local fallback: `local-hashing-384`
- Hosted Hugging Face model: `thenlper/gte-large`

If a Hugging Face token is configured, the app tries Hugging Face first. If Hugging Face is unreachable, rate-limited, misconfigured, or otherwise fails, the app logs the failure and falls back to local embeddings for the current process.

Get a Hugging Face token:

- https://huggingface.co/settings/tokens
- https://huggingface.co/docs/hub/security-tokens

Set it with environment variables:

```powershell
$env:HF_TOKEN = "your-hugging-face-token"
$env:HF_EMBEDDING_MODEL = "thenlper/gte-large"
```

Or in `IncidentResponseAgent.Api/appsettings.Development.json`:

```json
{
  "Runbooks": {
    "SemanticRetrieval": {
      "ApiKey": "your-hugging-face-token",
      "Model": "thenlper/gte-large"
    }
  }
}
```

Do not commit real keys.

## Agent Model

The agent uses an OpenAI-compatible endpoint. OpenRouter is the easiest drop-in provider for this project.

The current model-backed path is an agent, but it deliberately avoids relying on free-model tool-calling loops. The application gathers runbooks, logs, and metrics first, then sends that evidence to `OpenAIIncidentAnalysisAgent`, which asks the model for strict JSON reasoning. This is more reliable with free OpenRouter models than letting the model drive every tool call itself.

Get an OpenRouter key:

- https://openrouter.ai/settings/keys
- https://openrouter.ai/docs/api-reference/authentication

Set it with environment variables:

```powershell
$env:IRA_AGENT_API_KEY = "your-openrouter-key"
$env:IRA_AGENT_ENDPOINT = "https://openrouter.ai/api/v1"
$env:IRA_AGENT_MODEL = "nex-agi/nex-n2-pro:free"
```

Or in `IncidentResponseAgent.Api/appsettings.Development.json`:

```json
{
  "Agent": {
    "IncidentAnalysis": {
      "Provider": "OpenAI-compatible provider",
      "Model": "nex-agi/nex-n2-pro:free",
      "Endpoint": "https://openrouter.ai/api/v1",
      "ApiKey": "your-openrouter-key"
    }
  }
}
```

If no key is configured, the app uses the local prompt-based fallback so development and tests still work.

If the configured model provider is slow, unavailable, or rejects the request, the app falls back to the local analyzer instead of leaving the request stuck indefinitely. Free OpenRouter models can queue or time out, so the default model timeout is intentionally bounded:

```json
{
  "Agent": {
    "IncidentAnalysis": {
      "AnalysisTimeoutSeconds": 75
    }
  }
}
```

The API always gathers deterministic runbook, log, and metric evidence before asking the model. Treat the structured evidence list as the source of truth if a free model returns vague or inconsistent prose.

During local testing, `nex-agi/nex-n2-pro:free` responded faster and followed strict JSON better than the previous Nemotron free model. Free model availability changes over time, so you can swap the model id without changing application code.

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

## Frontend

The API serves a static frontend at:

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

No npm install or frontend build step is required.

OpenAPI is exposed in Development through:

```text
http://localhost:5155/openapi/v1.json
```

## Endpoints

Health:

```http
GET http://localhost:5155/health
```

Analyze an incident:

```http
POST http://localhost:5155/api/incidents/analyze
Content-Type: application/json

{
  "title": "Checkout 5xx spike",
  "description": "Customers are seeing intermittent 500 responses during checkout.",
  "severity": "High",
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
  "severity": "High",
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
- resilient embedding fallback
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

The app logs the hosted embedding failure and falls back to `local-hashing-384`.

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
