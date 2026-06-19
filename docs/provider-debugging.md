# Model provider debugging report

## Request path

`Create candidate & confirm` -> `POST /api/incidents/candidates/manual` -> `POST /api/incidents/candidates/{id}/confirm` -> `IncidentsController` -> `AnalyzeIncidentUseCase` -> isolated RAG/log/metric/prior-incident gathering -> `ResilientIncidentAnalysisAgent` -> `MicrosoftAgentFrameworkIncidentAnalysisAgent` -> Microsoft Agent Framework `ChatClientAgent.RunAsync` -> OpenRouter chat completions -> structured validation -> persistence -> `IncidentAnalysisResponse` -> `renderAnalysis`.

RAG exceptions are converted to a degraded empty `RunbookRetrievalResult` before model execution. Hugging Face is only an embedding provider. Qdrant/SQLite are only vector stores. Neither subsystem selects the local analysis fallback.

## Effective model configuration

- Provider: OpenRouter, invoked through Microsoft Agent Framework's OpenAI integration.
- Model: `nex-agi/nex-n2-pro:free`.
- Base URL: `https://openrouter.ai/api/v1`.
- API: `POST /chat/completions` with `choices[].message.content` response parsing.
- `appsettings.json` contains no API key. The verified development environment supplied `OPENROUTER_API_KEY`. Launch profiles do not define it and the API project has no `UserSecretsId`, so launches that do not inherit that environment variable fall back before sending a request.

## Sanitized request shape

```json
{
  "model": "nex-agi/nex-n2-pro:free",
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
