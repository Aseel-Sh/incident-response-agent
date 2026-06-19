import { createServer } from 'node:http';
import { spawn } from 'node:child_process';

const providerPort = 5199;
const requests = [];

function json(response, status, body) {
  response.writeHead(status, { 'content-type': 'application/json' });
  response.end(JSON.stringify(body));
}

function embeddingFor(text) {
  const value = String(text || '').toLowerCase();
  const vector = new Array(10).fill(0);
  if (value.includes('metrics-only-api')) return vector.map((_, index) => index === 6 ? 1 : 0);
  if (value.includes('rag-unavailable-api')) return vector.map((_, index) => index === 7 ? 1 : 0);
  if (/database|connection|pool|checkout|query/.test(value)) vector[0] = 1;
  if (/latency|slow|timeout/.test(value)) vector[1] = 1;
  if (/cache|static|asset/.test(value)) vector[2] = 1;
  if (/error.rate|request failure|impact/.test(value)) vector[3] = 1;
  if (/conflict/.test(value)) vector[4] = 1;
  if (/blackhole|unexplained/.test(value)) vector[5] = 1;
  if (vector.every(item => item === 0)) vector[9] = 1;
  const magnitude = Math.sqrt(vector.reduce((sum, item) => sum + item * item, 0));
  return vector.map(item => item / magnitude);
}

function responseFor(context) {
  const incident = context.incident;
  const evidence = context.evidence || {};
  const refs = ['incident.description'];
  const items = [{
    summary: incident.description,
    source: 'incident.description',
    details: `User-provided details for ${incident.serviceName}.`
  }];

  const runbook = evidence.runbooks?.[0];
  if (runbook) {
    refs.push(`rag.runbook.${runbook.id}`);
    items.push({ summary: runbook.summary, source: `rag.runbook.${runbook.id}`, details: runbook.title });
  }
  if (evidence.logs?.length) {
    refs.push('tool.logs');
    items.push({ summary: evidence.logs[0].message, source: 'tool.logs', details: evidence.logs[0].correlationId || 'observed log entry' });
  }
  if (evidence.metrics?.samples?.length) {
    refs.push('tool.metrics');
    items.push({ summary: `request_error_rate=${evidence.metrics.samples[0].value}`, source: 'tool.metrics', details: 'Observed metric sample.' });
  }

  const title = String(incident.title).toLowerCase();
  const conflicting = title.includes('conflicting');
  const poolEvidence = refs.some(ref => ref.startsWith('rag.runbook.')) || String(incident.description).toLowerCase().includes('pool');
  const hypothesis = conflicting
    ? 'Conflicting log and metric evidence may indicate a localized request-path problem rather than a broad outage.'
    : poolEvidence
      ? 'Database connection-pool saturation is a hypothesis supported by the timeout and latency evidence.'
      : 'Measured impact is present, but the root cause remains unconfirmed.';
  const actions = conflicting
    ? [{ description: 'Correlate the timeout correlation ID with the low error-rate interval.', priority: 'High', rationale: 'Resolve the conflict before changing production.', supportingSignals: refs.filter(ref => ref === 'tool.logs' || ref === 'tool.metrics') }]
    : poolEvidence
      ? [
          { description: 'Inspect connection-pool utilization and checkout wait time for the affected service.', priority: 'High', rationale: 'The supplied timeout evidence names pool checkout.', supportingSignals: refs },
          { description: 'Compare p95 query latency with request error rate over the incident window.', priority: 'High', rationale: 'This validates whether query latency tracks impact.', supportingSignals: refs.filter(ref => ref === 'tool.metrics' || ref === 'incident.description') }
        ]
      : [{ description: 'Inspect the request error-rate metric by endpoint and deployment version.', priority: 'High', rationale: 'The metric is the only observed operational signal.', supportingSignals: refs.filter(ref => ref === 'tool.metrics' || ref === 'incident.description') }];

  return {
    summary: `${incident.serviceName} incident analyzed from ${items.length} supplied evidence source(s).`,
    severity: incident.severity,
    evidence: items,
    hypotheses: [{ description: hypothesis, inferenceStrength: conflicting ? 'Weak' : 'Medium', confidence: conflicting ? 'Low' : 'Medium', supportingEvidence: items.map(item => item.summary), evidenceReferences: refs }],
    recommendedActions: actions,
    confidence: conflicting || (!evidence.logs?.length && !runbook) ? 'Low' : 'Medium',
    notes: evidence.logs?.length ? 'Root cause is not confirmed; validate the hypothesis.' : 'No matching log evidence was supplied; root cause remains unknown.'
  };
}

const provider = createServer((request, response) => {
  if (request.method === 'GET' && request.url === '/__requests') return json(response, 200, requests);
  if (request.method === 'POST' && request.url === '/__shutdown') {
    json(response, 200, { status: 'stopping' });
    setTimeout(stop, 25);
    return;
  }

  let body = '';
  request.on('data', chunk => { body += chunk; });
  request.on('end', () => {
    let payload;
    try { payload = body ? JSON.parse(body) : {}; }
    catch { return json(response, 400, { error: 'invalid fixture request JSON' }); }

    if (request.url === '/embeddings/fixture-embedding') {
      if (String(payload.inputs || '').toLowerCase().includes('rag unavailable')) {
        return json(response, 503, { error: 'deterministic embedding outage' });
      }
      const embedding = embeddingFor(payload.inputs);
      requests.push({ kind: 'embedding', preview: String(payload.inputs || '').slice(0, 120), activeDimensions: embedding.map((value, index) => value ? index : -1).filter(index => index >= 0) });
      return json(response, 200, embedding);
    }

    if (request.url === '/v1/chat/completions') {
      let context = {};
      try { context = JSON.parse(payload.messages?.find(item => item.role === 'user')?.content || '{}'); }
      catch { return json(response, 400, { error: 'invalid model context' }); }
      const strict = payload.response_format?.type === 'json_schema';
      const title = String(context.incident?.title || '');
      requests.push({ model: payload.model, strict, title, authorizationPresent: Boolean(request.headers.authorization) });
      if (/model unavailable|both unavailable/i.test(title)) return json(response, 503, { error: 'deterministic model outage' });
      if (/empty model output/i.test(title) && strict) {
        return json(response, 200, { model: 'fixture/model-v1', choices: [{ message: { content: '' } }] });
      }
      const content = JSON.stringify(responseFor(context));
      return json(response, 200, { model: 'fixture/model-v1', choices: [{ message: { content } }] });
    }

    return json(response, 404, { error: 'fixture route not found' });
  });
});

provider.listen(providerPort, '127.0.0.1');
const defaults = {
  ASPNETCORE_URLS: 'http://127.0.0.1:5198',
  ASPNETCORE_ENVIRONMENT: 'Production',
  'Agent__IncidentAnalysis__Provider': 'OpenRouter',
  'Agent__IncidentAnalysis__Model': 'fixture/model-v1',
  'Agent__IncidentAnalysis__Endpoint': 'http://127.0.0.1:5199/v1',
  'Agent__IncidentAnalysis__ApiKey': 'fixture-key-not-a-real-secret',
  'Runbooks__SemanticRetrieval__ApiKey': 'fixture-key-not-a-real-secret',
  'Runbooks__SemanticRetrieval__Endpoint': 'http://127.0.0.1:5199/embeddings',
  'Runbooks__SemanticRetrieval__Model': 'fixture-embedding',
  'Runbooks__SemanticRetrieval__KnowledgeBasePath': '.tmp/e2e-ai/knowledge',
  'Runbooks__SemanticRetrieval__VectorStoreProvider': 'SQLite',
  'Runbooks__SemanticRetrieval__DatabasePath': '.tmp/e2e-ai/rag.sqlite',
  'Runbooks__SemanticRetrieval__MinimumRelevanceScore': '0.95',
  'Storage__Incidents__SessionDatabasePath': '.tmp/e2e-ai/sessions.sqlite',
  'Storage__Incidents__IncidentRecordsPath': '.tmp/e2e-ai/incidents.json',
  'Tools__OperationalData__LogEntriesPath': '.tmp/e2e-ai/logs.json',
  'Tools__OperationalData__MetricSamplesPath': '.tmp/e2e-ai/metrics.json'
};
const app = spawn('dotnet', ['.tmp/e2e-app/IncidentResponseAgent.Api.dll', '--contentRoot', '.'], {
  stdio: 'ignore', windowsHide: true, env: { ...defaults, ...process.env }
});

let stopping = false;
function stop() {
  if (stopping) return;
  stopping = true;
  provider.close();
  provider.closeAllConnections?.();
  if (app.exitCode === null) app.kill();
  setTimeout(() => process.exit(0), 750);
}
process.on('SIGINT', stop);
process.on('SIGTERM', stop);
app.on('exit', code => process.exit(stopping ? 0 : (code ?? 1)));
