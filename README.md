# Incident Response Agent

Incident Response Agent is a .NET 10 modular monolith for AI-assisted incident investigation. It accepts incident submissions, retrieves operational runbook context, queries log and metric tools, and returns structured guidance with evidence, hypotheses, recommended actions, confidence, and session context.

## Projects

- `IncidentResponseAgent.Api`: HTTP endpoints, contracts, validation, and composition root.
- `IncidentResponseAgent.Application`: use cases, orchestration, tool abstractions, persistence abstractions, and evaluation contracts.
- `IncidentResponseAgent.Domain`: framework-light incident and runbook domain models.
- `IncidentResponseAgent.Infrastructure`: concrete log/metric providers, incident storage, session storage, and SQLite-backed runbook RAG.
- `IncidentResponseAgent.Agent`: Microsoft Agent Framework integration and prompt-based local fallback.

## RAG Storage

Runbooks are authored as Markdown in `IncidentResponseAgent.Infrastructure/Runbooks/KnowledgeBase`.

At runtime, the infrastructure layer:

1. Loads Markdown runbooks.
2. Chunks them by headings and numbered steps.
3. Generates embeddings for each chunk.
4. Stores documents, chunks, and vectors in SQLite.
5. Retrieves top matching chunks with hybrid semantic and lexical scoring.

The default database path is:

```text
%LOCALAPPDATA%\IncidentResponseAgent\runbook-rag.sqlite
```

You can override it with:

```json
"Runbooks": {
  "SemanticRetrieval": {
    "DatabasePath": "C:\\data\\incident-response-agent\\runbook-rag.sqlite",
    "KnowledgeBasePath": "C:\\path\\to\\runbooks"
  }
}
```

## Free Embedding Defaults

The project is configured for free/low-cost development paths:

- Without keys, it uses `local-hashing-384`, a deterministic local embedding fallback.
- With `HF_TOKEN`, it uses the Hugging Face feature-extraction endpoint.
- Default hosted embedding model: `thenlper/gte-large`.

Set these when you are ready:

```powershell
$env:HF_TOKEN = "your-hugging-face-token"
$env:HF_EMBEDDING_MODEL = "thenlper/gte-large"
```

## Agent Model Configuration

If `Agent:IncidentAnalysis:ApiKey` or `IRA_AGENT_API_KEY` is empty, the app uses the prompt-based local agent fallback so the API can still run during development.

When you add a free/OpenRouter-compatible model key later:

```powershell
$env:IRA_AGENT_API_KEY = "your-model-api-key"
$env:IRA_AGENT_ENDPOINT = "https://openrouter.ai/api/v1"
$env:IRA_AGENT_MODEL = "your-free-model"
```

## Run

```powershell
dotnet run --project IncidentResponseAgent.Api
```

Health check:

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

## Evaluation

`IncidentResponseAgent.Application/Evaluation` contains local rubric-based evaluation hooks. They are intentionally model-independent so scenario tests can score evidence coverage, hypotheses, actions, and expected themes without calling an LLM.
