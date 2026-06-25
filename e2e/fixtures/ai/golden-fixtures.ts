export type GoldenAnalysisFixture = {
  title: string;
  description: string;
  severity: 'sev1' | 'sev2' | 'sev3' | 'sev4' | 'sev5';
  serviceName: string;
  environment: string;
  tags: string[];
  expected: {
    severityLabel: string;
    requiredEvidence: string[];
    expectedHypothesisTerms: string[];
    requiredUnknownTerms: string[];
    usefulActionTerms: string[];
    forbiddenActionTerms: string[];
    shouldMatchRunbook: boolean;
  };
};

const common = { environment: 'production', tags: ['database', 'latency'] };

export const goldenFixtures: Record<string, GoldenAnalysisFixture> = {
  clearDatabaseLatency: {
    ...common,
    title: 'Clear database latency incident',
    description: 'database-api requests are timing out while waiting for the checkout query connection pool.',
    severity: 'sev2', serviceName: 'database-api',
    expected: {
      severityLabel: 'SEV-2', requiredEvidence: ['tool.logs', 'tool.metrics', 'rag.runbook.'],
      expectedHypothesisTerms: ['pool saturation'], requiredUnknownTerms: ['root cause'],
      usefulActionTerms: ['connection-pool utilization', 'p95 query latency'],
      forbiddenActionTerms: ['restart the database blindly', 'delete data'], shouldMatchRunbook: true
    }
  },
  metricsOnly: {
    environment: 'production', tags: ['telemetry', 'impact-gauge'],
    title: 'Telemetry impact with absent event records',
    description: 'blackhole-metrics-api has a measured customer-impact gauge increase without matching event records.',
    severity: 'sev1', serviceName: 'blackhole-metrics-api',
    expected: {
      severityLabel: 'SEV-1', requiredEvidence: ['tool.metrics'],
      expectedHypothesisTerms: ['measured impact'], requiredUnknownTerms: ['logs were available', 'root cause'],
      usefulActionTerms: ['error-rate metric'], forbiddenActionTerms: ['log proves'], shouldMatchRunbook: false
    }
  },
  conflictingEvidence: {
    ...common,
    title: 'Conflicting evidence database incident',
    description: 'conflicting-api has one timeout log while the request error-rate metric remains low.',
    severity: 'sev3', serviceName: 'conflicting-api',
    expected: {
      severityLabel: 'SEV-3', requiredEvidence: ['tool.logs', 'tool.metrics'],
      expectedHypothesisTerms: ['conflicting'], requiredUnknownTerms: ['root cause'],
      usefulActionTerms: ['correlate'], forbiddenActionTerms: ['confirmed outage'], shouldMatchRunbook: true
    }
  },
  emptyModelRetry: {
    ...common,
    title: 'Empty model output retry database latency',
    description: 'database-api connection pool latency requires a structured-output retry.',
    severity: 'sev2', serviceName: 'database-api',
    expected: {
      severityLabel: 'SEV-2', requiredEvidence: ['tool.logs', 'tool.metrics'],
      expectedHypothesisTerms: ['pool saturation'], requiredUnknownTerms: ['root cause'],
      usefulActionTerms: ['connection-pool utilization'], forbiddenActionTerms: ['delete data'], shouldMatchRunbook: true
    }
  },
  modelUnavailable: {
    ...common,
    title: 'Model unavailable database incident',
    description: 'model-unavailable-api database connection timeouts require evidence-based local handling.',
    severity: 'sev2', serviceName: 'model-unavailable-api',
    expected: {
      severityLabel: 'SEV-2', requiredEvidence: ['tool.logs', 'tool.metrics'],
      expectedHypothesisTerms: ['database'], requiredUnknownTerms: ['root cause'],
      usefulActionTerms: ['connection'], forbiddenActionTerms: ['model confirmed'], shouldMatchRunbook: true
    }
  },
  bothUnavailable: {
    ...common,
    title: 'Both unavailable blackhole incident',
    description: 'blackhole-service reports an unexplained condition with no operational evidence.',
    severity: 'sev4', serviceName: 'blackhole-service', tags: ['blackhole'],
    expected: {
      severityLabel: 'SEV-4', requiredEvidence: [], expectedHypothesisTerms: [],
      requiredUnknownTerms: ['operational evidence'], usefulActionTerms: [], forbiddenActionTerms: ['confirmed'], shouldMatchRunbook: false
    }
  },
  ragUnavailable: {
    environment: 'production', tags: ['rag-unavailable', 'metrics'],
    title: 'RAG unavailable but model available measured impact',
    description: 'rag-unavailable-api has a measured request error-rate impact while embedding retrieval is unavailable.',
    severity: 'sev2', serviceName: 'rag-unavailable-api',
    expected: {
      severityLabel: 'SEV-2', requiredEvidence: ['tool.metrics', 'rag.runbook.'],
      expectedHypothesisTerms: ['pool saturation'], requiredUnknownTerms: ['root cause'],
      usefulActionTerms: ['connection-pool utilization'], forbiddenActionTerms: ['RAG confirmed'], shouldMatchRunbook: true
    }
  }
};
