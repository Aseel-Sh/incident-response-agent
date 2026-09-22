// Local, isolated QA workspace. Never reads production credentials or resets existing data.
import { mkdir, writeFile, cp, access } from 'node:fs/promises';
import { resolve } from 'node:path';

const root = resolve('.tmp/manual-qa');
await mkdir(root, { recursive: true });
for (const file of ['logs.json', 'metrics.json']) {
  try { await access(resolve(root, file)); }
  catch { await writeFile(resolve(root, file), '[]'); }
}
try { await access(resolve(root, 'knowledge')); }
catch { await cp(resolve('e2e/fixtures/ai/knowledge'), resolve(root, 'knowledge'), { recursive: true }); }

Object.assign(process.env, {
  ASPNETCORE_ENVIRONMENT: 'Testing', ASPNETCORE_URLS: 'http://127.0.0.1:5298',
  FIXTURE_PROVIDER_PORT: '5299',
  Authentication__AllowDevelopmentIdentity: 'true',
  Agent__IncidentAnalysis__Endpoint: 'http://127.0.0.1:5299/v1',
  Agent__IncidentAnalysis__ApiKey: 'fixture-key-not-a-real-secret',
  Agent__IncidentAnalysis__Model: 'fixture/model-v1',
  Agent__IncidentAnalysis__AllowLocalAnalysisFallback: process.argv.includes('--fallback') ? 'true' : 'false',
  Agent__IncidentAnalysis__AnalysisTimeoutSeconds: '5',
  Runbooks__SemanticRetrieval__Endpoint: 'http://127.0.0.1:5299/embeddings',
  Runbooks__SemanticRetrieval__EmbeddingProvider: 'HuggingFace',
  Runbooks__SemanticRetrieval__Model: 'fixture-embedding',
  Runbooks__SemanticRetrieval__ApiKey: 'fixture-key-not-a-real-secret',
  Runbooks__SemanticRetrieval__VectorStoreProvider: 'SQLite',
  Runbooks__SemanticRetrieval__AllowLocalEmbeddingFallback: 'false',
  Runbooks__SemanticRetrieval__AllowLocalVectorStoreFallback: 'false',
  Runbooks__SemanticRetrieval__KnowledgeBasePath: resolve(root, 'knowledge'),
  Runbooks__SemanticRetrieval__DatabasePath: resolve(root, 'rag.sqlite'),
  Runbooks__SemanticRetrieval__SourceRegistryPath: resolve(root, 'sources.json'),
  Runbooks__SemanticRetrieval__MinimumRelevanceScore: '0.25',
  Storage__Incidents__IncidentRecordsPath: resolve(root, 'incidents.json'),
  Storage__Incidents__SessionDatabasePath: resolve(root, 'sessions.sqlite'),
  Tools__OperationalData__LogEntriesPath: resolve(root, 'logs.json'),
  Tools__OperationalData__MetricSamplesPath: resolve(root, 'metrics.json'),
  Tools__OperationalData__ProjectRegistryPath: resolve(root, 'projects.json'),
  Tools__OperationalData__SourceHealthEndpoint: '',
  Tools__OperationalData__UseDeterministicFallbacks: 'false',
  Tools__OperationalData__LogPatternCountThreshold: '2',
  Monitoring__StatePath: resolve(root, 'monitoring.json'),
  Monitoring__PollingIntervalSeconds: '10', Monitoring__StartupDelaySeconds: '5',
  OPENROUTER_BASE_URL: '', IRA_AGENT_ENDPOINT: '', OPENROUTER_MODEL: '', IRA_AGENT_MODEL: '',
  OPENROUTER_TIMEOUT_SECONDS: '', IRA_AGENT_ANALYSIS_TIMEOUT_SECONDS: '',
  OPENROUTER_API_KEY: '', IRA_AGENT_API_KEY: '', HF_TOKEN: '', HF_API_TOKEN: ''
});
console.log(`Manual QA: http://127.0.0.1:5298 · Data: ${root} · Synthetic provider; local analysis fallback ${process.env.Agent__IncidentAnalysis__AllowLocalAnalysisFallback}`);
await import('./serve-ai.mjs');
