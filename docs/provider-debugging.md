# Model provider debugging report

## Request path

`Create candidate & confirm` -> `POST /api/incidents/candidates/manual` -> `POST /api/incidents/candidates/{id}/confirm` -> `IncidentsController` -> `AnalyzeIncidentUseCase` -> isolated RAG/log/metric/prior-incident gathering -> `ResilientIncidentAnalysisAgent` -> `MicrosoftAgentFrameworkIncidentAnalysisAgent` -> Microsoft Agent Framework `ChatClientAgent.RunAsync` -> OpenRouter chat completions -> structured validation -> persistence -> `IncidentAnalysisResponse` -> `renderAnalysis`.

RAG exceptions are converted to a degraded empty `RunbookRetrievalResult` before model execution. Hugging Face is only an embedding provider. Qdrant/SQLite are only vector stores. Neither subsystem selects the local analysis fallback.

## Effective model configuration

- Provider: OpenRouter, invoked through Microsoft Agent Framework's OpenAI integration.
- Model: `nvidia/nemotron-3-super-120b-a12b:free` by default.
- Base URL: `https://openrouter.ai/api/v1`.
- API: `POST /chat/completions` with `choices[].message.content` response parsing.
- `appsettings.json` contains no API key. The verified development environment supplied `OPENROUTER_API_KEY`. Launch profiles do not define it and the API project has no `UserSecretsId`, so launches that do not inherit that environment variable fall back before sending a request.

## Sanitized request shape

```json
{
  "model": "nvidia/nemotron-3-super-120b-a12b:free",
  "messages": [
    { "role": "system", "content": "incident analysis and JSON schema instructions" },
    { "role": "user", "content": "JSON containing incident, session, runbooks, logs, metrics, and approved similar incidents" }
  ],
  "response_format": {
    "type": "json_schema",
    "json_schema": {
      "name": "incident_analysis",
      "strict": true,
      "schema": "object schema including SEV-1 through SEV-5"
    }
  },
  "tools": ["SearchLogs", "QueryMetrics", "RetrieveRunbooks", "RetrievePriorIncidents", "RetrievePriorActionOutcomes", "CheckSimilarIncidents", "DraftProposedKnowledgeUpdate"],
  "max_completion_tokens": 1200,
  "temperature": 0.1
}
```

Headers include `Authorization: Bearer [REDACTED]` and, when configured, `HTTP-Referer` and `X-OpenRouter-Title`. The retry retains the messages, model, and framework tools but omits `response_format` and permits up to twice the configured output tokens.

## Observed failures

A controlled request against the previously running app succeeded through OpenRouter in 8.5 seconds with 2,742 response characters, model `nex-agi/nex-n2-pro:free`, and no fallback. Persisted historical records showed several distinct earlier causes:

- most recent: 45-second model timeouts;
- successful HTTP responses with empty model content;
- one nonempty invalid/unstructured JSON response;
- older processes with no inherited API key;
- older OpenRouter free-model rate limits (`429`).

The implementation defect was incomplete retry coverage. Only an empty successful strict response retried. Strict-schema HTTP rejection and nonempty invalid/schema-invalid output did not retry correctly.

## Corrected behavior

Strict output is validated for JSON shape, required fields, enum fields, and numeric SEV. Empty, invalid JSON, schema-invalid, wrong-SEV, and HTTP 400/422 strict-mode rejection trigger one prompt-only JSON retry. A successful retry remains model analysis and records/displays the retry reason. Failure of both attempts reaches the resilient local fallback. Local fallback refuses to synthesize analysis when no log, metric, runbook, or approved prior incident exists.

## June 20 provider investigation

- The persisted 45-second failures were created by an older build whose `appsettings.json` set `AnalysisTimeoutSeconds` to 45. Current source uses 75 seconds. The cancellation originates in `ResilientIncidentAnalysisAgent.CancelAfter`, after evidence and RAG gathering and while Microsoft Agent Framework is executing the OpenRouter request.
- The previous free model was `nex-agi/nex-n2-pro:free`. A minimal direct request took about 31.5 seconds, so a full structured agent request could exceed 45 seconds even without slow local tools. `nvidia/nemotron-3-super-120b-a12b:free` returned valid structured JSON in about 1.2 seconds with the same key and is now the default. A paid low-latency model could not be verified because the current OpenRouter key reports its total spending limit exceeded.
- Microsoft Agent Framework is on the real path: `MicrosoftAgentFrameworkIncidentAnalysisAgent` creates a `ChatClientAgent`, registers seven `AITool` functions, creates an `AgentSession`, and calls `RunAsync`. Local analysis is invoked only by `ResilientIncidentAnalysisAgent` after missing credentials, provider failure, validation failure, or cancellation.
- RAG was available but degraded, not unavailable. Persisted analyses contained three runbook matches while `IsDegraded=true`: SQLite retrieval succeeded using local hashing embeddings after the primary Hugging Face provider failed.
- The Hugging Face root cause was a retired legacy endpoint: `api-inference.huggingface.co` no longer resolved during diagnosis. The provider now targets `https://router.huggingface.co/hf-inference/models/`, supports both `HF_TOKEN` and `HF_API_TOKEN`, enforces a 15-second embedding timeout, and persists a sanitized exception/status reason when local embeddings take over.
- New provider transparency includes evidence-gathering, RAG, and model durations, fallback stage, and timeout source. Tool calls also log their individual durations.
- Live end-to-end verification with the published app running from its content root completed in 11.3 seconds: Microsoft Agent Framework returned provider `OpenRouter`, model `nvidia/nemotron-3-super-120b-a12b:free`, `UsedFallback=false`, model duration 10.9 seconds, RAG duration 165 ms, three runbook matches, and `RagDegraded=false`.
- Launching the published DLL from a different working directory reproduced an immediate configuration fallback (`Agent:IncidentAnalysis:Model is not configured`). Production must run from the publish directory or set `OPENROUTER_MODEL`, `OPENROUTER_BASE_URL`, and the API key explicitly.
- With no Hugging Face token, the corrected router endpoint returned HTTP 401 in 134 ms. This proves DNS and routing work; set `HF_TOKEN` or `HF_API_TOKEN` to enable Hugging Face. Without a token, local embeddings are the configured active provider and RAG is available rather than degraded.
