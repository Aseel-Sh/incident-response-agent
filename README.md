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
- Persists incident analysis records.
- Persists multi-turn investigation session state in SQLite.
- Uses a Microsoft Agent Framework `AIAgent` with tool registration when a model key is configured.
- Falls back to a local prompt-based agent when no model key is configured.
- Returns structured evidence, hypotheses, recommended actions, confidence, notes, and session context.
- Exposes RAG diagnostics so retrieval quality can be inspected directly.
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
  Microsoft Agent Framework integration, agent instructions, tool registration, OpenAI-compatible agent execution, and local prompt fallback.

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

Get an OpenRouter key:

- https://openrouter.ai/settings/keys
- https://openrouter.ai/docs/api-reference/authentication

Set it with environment variables:

```powershell
$env:IRA_AGENT_API_KEY = "your-openrouter-key"
$env:IRA_AGENT_ENDPOINT = "https://openrouter.ai/api/v1"
$env:IRA_AGENT_MODEL = "nvidia/nemotron-3-super-120b-a12b:free"
```

Or in `IncidentResponseAgent.Api/appsettings.Development.json`:

```json
{
  "Agent": {
    "IncidentAnalysis": {
      "Provider": "OpenAI-compatible provider",
      "Model": "nvidia/nemotron-3-super-120b-a12b:free",
      "Endpoint": "https://openrouter.ai/api/v1",
      "ApiKey": "your-openrouter-key"
    }
  }
}
```

If no key is configured, the app uses the local prompt-based fallback so development and tests still work.

## Local Operational Data

The project includes sample operational signals:

```text
IncidentResponseAgent.Infrastructure/Tools/SampleData/logs.json
IncidentResponseAgent.Infrastructure/Tools/SampleData/metrics.json
```

These files are copied to the API output folder and used by:

- `LocalJsonLogSearchProvider`
- `LocalJsonMetricsProvider`

If no local data matches a query, deterministic fallback signals are returned so the incident workflow remains usable.

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

- submit an incident
- reuse a session id for follow-up turns
- inspect structured analysis output
- run RAG searches directly
- view recent persisted analyses
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
- Add a frontend for incident submission and investigation sessions.
- Add more evaluation fixtures and regression tests.
- Add a dedicated vector database adapter if you want Qdrant, Chroma, LanceDB, or pgvector.

## Security Notes

- Do not commit real API keys.
- Prefer environment variables or `appsettings.Development.json` for local secrets.
- `appsettings.Development.json` is ignored by git.
- Treat model output as untrusted.
- Keep tool behavior bounded and read-only unless deliberately adding controlled write actions.
