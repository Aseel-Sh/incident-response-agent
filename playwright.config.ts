import { defineConfig, devices } from '@playwright/test';

const port = 5198;
const baseURL = `http://127.0.0.1:${port}`;

export default defineConfig({
  testDir: './e2e',
  globalTeardown: './e2e/scripts/teardown-core.mjs',
  fullyParallel: false,
  workers: 1,
  retries: 1,
  timeout: 45_000,
  expect: { timeout: 10_000 },
  outputDir: 'test-results',
  reporter: [
    ['list'],
    ['html', { outputFolder: 'playwright-report', open: 'never' }]
  ],
  use: {
    baseURL,
    screenshot: { mode: 'only-on-failure', fullPage: true },
    trace: 'retain-on-failure',
    video: 'retain-on-failure'
  },
  webServer: {
    command: 'node e2e/scripts/serve.mjs',
    url: `${baseURL}/health`,
    reuseExistingServer: process.env.PLAYWRIGHT_REUSE_SERVER === '1',
    timeout: 120_000,
    stdout: 'ignore',
    stderr: 'ignore',
    env: {
      ASPNETCORE_URLS: baseURL,
      ASPNETCORE_ENVIRONMENT: 'Production',
      'Logging__EventLog__LogLevel__Default': 'None',
      'Agent__IncidentAnalysis__ApiKey': '',
      'Agent__IncidentAnalysis__AnalysisTimeoutSeconds': '2',
      OPENROUTER_API_KEY: '',
      IRA_AGENT_API_KEY: '',
      'Runbooks__SemanticRetrieval__ApiKey': '',
      HF_API_TOKEN: '',
      'Runbooks__SemanticRetrieval__VectorStoreProvider': 'SQLite',
      'Runbooks__SemanticRetrieval__DatabasePath': '.tmp/e2e-data/runbook-rag.sqlite',
      'Storage__Incidents__SessionDatabasePath': '.tmp/e2e-data/incident-sessions.sqlite',
      'Storage__Incidents__IncidentRecordsPath': '.tmp/e2e-data/incident-records.json',
      'Tools__OperationalData__LogEntriesPath': '.tmp/e2e-data/logs.json',
      'Tools__OperationalData__MetricSamplesPath': '.tmp/e2e-data/metrics.json',
      'Tools__OperationalData__LogPatternCountThreshold': '2',
      'Tools__OperationalData__HighErrorRateThreshold': '25',
      'Tools__OperationalData__CriticalErrorRateThreshold': '40',
      'Tools__OperationalData__LatencyWarningThresholdMs': '1000',
      'Tools__OperationalData__LatencyCriticalThresholdMs': '3000',
      'Tools__OperationalData__HealthCheckFailureThreshold': '3',
      'Tools__OperationalData__HealthCheckCriticalFailureThreshold': '10'
    }
  },
  projects: [
    {
      name: 'chromium-core',
      testIgnore: [/mobile\.spec\.ts/, /ai-analysis-rag\.spec\.ts/, /learning-loop\.spec\.ts/],
      use: { ...devices['Desktop Chrome'] }
    },
    {
      name: 'chromium-mobile',
      testMatch: /mobile\.spec\.ts/,
      use: { ...devices['Pixel 7'] }
    }
  ]
});
