import { defineConfig, devices } from '@playwright/test';

const baseURL = 'http://127.0.0.1:5198';

export default defineConfig({
  testDir: './e2e',
  testMatch: /ai-analysis-rag\.spec\.ts/,
  globalTeardown: './e2e/scripts/teardown-ai.mjs',
  fullyParallel: false,
  workers: 1,
  retries: 1,
  timeout: 60_000,
  expect: { timeout: 12_000 },
  outputDir: 'test-results/ai',
  reporter: [['list'], ['html', { outputFolder: 'playwright-report/ai', open: 'never' }]],
  use: {
    ...devices['Desktop Chrome'],
    baseURL,
    screenshot: { mode: 'only-on-failure', fullPage: true },
    trace: 'retain-on-failure',
    video: 'retain-on-failure'
  },
  webServer: process.env.AI_E2E_EXTERNAL === '1' ? undefined : {
    command: 'node e2e/scripts/serve-ai.mjs',
    url: `${baseURL}/health`,
    reuseExistingServer: process.env.PLAYWRIGHT_REUSE_SERVER === '1',
    timeout: 120_000,
    stdout: 'pipe',
    stderr: 'pipe',
    env: {
      ASPNETCORE_URLS: baseURL,
      ASPNETCORE_ENVIRONMENT: 'Production',
      'Authentication__AllowDevelopmentIdentity': 'true',
      'Logging__EventLog__LogLevel__Default': 'None',
      'Agent__IncidentAnalysis__Provider': 'OpenRouter',
      'Agent__IncidentAnalysis__Model': 'fixture/model-v1',
      'Agent__IncidentAnalysis__Endpoint': 'http://127.0.0.1:5199/v1',
      'Agent__IncidentAnalysis__ApiKey': 'fixture-key-not-a-real-secret',
      'Agent__IncidentAnalysis__AllowLocalAnalysisFallback': 'true',
      'Agent__IncidentAnalysis__AnalysisTimeoutSeconds': '10',
      'Runbooks__SemanticRetrieval__ApiKey': 'fixture-key-not-a-real-secret',
      'Runbooks__SemanticRetrieval__Endpoint': 'http://127.0.0.1:5199/embeddings',
      'Runbooks__SemanticRetrieval__Model': 'fixture-embedding',
      'Runbooks__SemanticRetrieval__KnowledgeBasePath': '.tmp/e2e-ai/knowledge',
      'Runbooks__SemanticRetrieval__SourceRegistryPath': '.tmp/e2e-ai/runbook-sources.json',
      'Runbooks__SemanticRetrieval__VectorStoreProvider': 'SQLite',
      'Runbooks__SemanticRetrieval__DatabasePath': '.tmp/e2e-ai/rag.sqlite',
      'Runbooks__SemanticRetrieval__MinimumRelevanceScore': '0.95',
      'Storage__Incidents__SessionDatabasePath': '.tmp/e2e-ai/sessions.sqlite',
      'Storage__Incidents__IncidentRecordsPath': '.tmp/e2e-ai/incidents.json',
      'Tools__OperationalData__LogEntriesPath': '.tmp/e2e-ai/logs.json',
      'Tools__OperationalData__MetricSamplesPath': '.tmp/e2e-ai/metrics.json',
      'Monitoring__Enabled': 'false',
      'Monitoring__StatePath': '.tmp/e2e-ai/monitoring-state.json'
    }
  },
  projects: [{ name: 'chromium-ai', use: { ...devices['Desktop Chrome'] } }]
});
