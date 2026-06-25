import fs from 'node:fs/promises';
import path from 'node:path';

const baseUrl = (process.argv[2] || process.env.INCIDENT_AGENT_URL || 'http://127.0.0.1:5155').replace(/\/$/, '');
const outputRoot = path.resolve('evaluation/results');
const runAt = new Date();
const runId = runAt.toISOString().replaceAll(':', '-').replaceAll('.', '-');

async function json(url, options) {
  const response = await fetch(`${baseUrl}${url}`, options);
  const body = await response.text();
  if (!response.ok) throw new Error(`${response.status} ${response.statusText}: ${body}`);
  return body ? JSON.parse(body) : null;
}

const scenarios = await json('/api/evaluation/scenarios');
const results = [];
for (const scenario of scenarios) {
  const started = performance.now();
  try {
    const analysis = await json('/api/incidents/analyze', {
      method: 'POST', headers: { 'content-type': 'application/json' },
      body: JSON.stringify({
        title: scenario.title, description: scenario.description,
        severity: String(scenario.severity).toLowerCase(), serviceName: scenario.serviceName,
        environment: scenario.environment, tags: scenario.tags
      })
    });
    const evidenceSources = (analysis.retrievedEvidence || []).map(item => String(item.source || ''));
    const required = scenario.expectedEvidenceSignals || [];
    const evidenceHits = required.filter(expected => evidenceSources.some(source => source.includes(expected))).length;
    const claimCount = (analysis.knownFacts || []).length;
    const unsupportedClaims = (analysis.knownFacts || []).filter(claim => !(claim.evidenceReferences || []).every(reference => evidenceSources.includes(reference))).length;
    const recommendations = analysis.recommendedActions || [];
    const groundedRecommendations = recommendations.filter(action => (action.supportingSignals || []).length > 0 && action.supportingSignals.every(signal => evidenceSources.includes(signal))).length;
    const provider = analysis.providerTransparency || {};
    results.push({
      scenario: scenario.name, passed: true,
      expectedSeverity: scenario.severity.toUpperCase().replace('SEV', 'SEV-'), actualSeverity: analysis.severity,
      severityCorrect: analysis.severity === scenario.severity.toUpperCase().replace('SEV', 'SEV-'),
      evidenceCoverage: required.length ? evidenceHits / required.length : 1,
      retrievalRelevant: required.some(item => item.startsWith('rag.runbook')) ? (analysis.runbookMatches || []).length > 0 : null,
      unsupportedClaimRate: claimCount ? unsupportedClaims / claimCount : 0,
      groundedRecommendationRate: recommendations.length ? groundedRecommendations / recommendations.length : 0,
      providerReportingCorrect: Boolean(provider.modelProvider && provider.model && provider.embeddingProvider && provider.vectorStore),
      fallbackCorrect: analysis.usedFallbackAnalysis ? Boolean(analysis.fallbackReason) : !analysis.fallbackReason,
      ragDegradedCorrect: provider.isDegraded ? Boolean(provider.degradedReason) : true,
      provider: provider.modelProvider || analysis.analysisProvider, model: provider.model || analysis.analysisModel,
      fallbackUsed: Boolean(analysis.usedFallbackAnalysis), fallbackReason: analysis.fallbackReason || null,
      ragStatus: provider.ragStatus, ragDegraded: Boolean(provider.isDegraded),
      latencyMs: Math.round(performance.now() - started), modelLatencyMs: provider.modelDurationMilliseconds ?? null,
      retrievalLatencyMs: provider.ragDurationMilliseconds ?? null, toolLatencyMs: provider.toolDurationMilliseconds ?? null,
      evidenceSources, runbookMatches: (analysis.runbookMatches || []).map(item => item.title)
    });
  } catch (error) {
    results.push({ scenario: scenario.name, passed: false, error: error.message, latencyMs: Math.round(performance.now() - started) });
  }
}

const completed = results.filter(item => item.passed);
const average = (key) => completed.length ? completed.reduce((sum, item) => sum + Number(item[key] || 0), 0) / completed.length : 0;
const report = {
  runId, generatedAtUtc: new Date().toISOString(), baseUrl,
  configuration: { scenarioCount: scenarios.length, note: 'Provider names, degraded state, fallback state, and timings are copied from actual API responses.' },
  thresholds: { severityAccuracy: 0.8, evidenceCoverage: 0.7, unsupportedClaimRateMax: 0, groundedRecommendationRate: 0.8, providerReportingAccuracy: 1 },
  aggregate: {
    completedScenarios: completed.length, failedScenarios: results.length - completed.length,
    severityAccuracy: average('severityCorrect'), evidenceCoverage: average('evidenceCoverage'),
    unsupportedClaimRate: average('unsupportedClaimRate'), groundedRecommendationRate: average('groundedRecommendationRate'),
    providerReportingAccuracy: average('providerReportingCorrect'), fallbackCorrectness: average('fallbackCorrect'),
    ragDegradedModeCorrectness: average('ragDegradedCorrect'), averageEndToEndLatencyMs: average('latencyMs'),
    candidateClassificationAccuracy: null, priorOutcomeReuseAccuracy: null,
    note: 'Candidate classification and prior-outcome reuse require the live monitoring/learning campaign and are intentionally not fabricated here.'
  },
  scenarios: results
};
report.passed = report.aggregate.completedScenarios === scenarios.length && report.aggregate.severityAccuracy >= report.thresholds.severityAccuracy && report.aggregate.evidenceCoverage >= report.thresholds.evidenceCoverage && report.aggregate.unsupportedClaimRate <= report.thresholds.unsupportedClaimRateMax && report.aggregate.groundedRecommendationRate >= report.thresholds.groundedRecommendationRate && report.aggregate.providerReportingAccuracy >= report.thresholds.providerReportingAccuracy;

await fs.mkdir(outputRoot, { recursive: true });
const jsonPath = path.join(outputRoot, `${runId}.json`);
const markdownPath = path.join(outputRoot, `${runId}.md`);
await fs.writeFile(jsonPath, JSON.stringify(report, null, 2));
const percent = value => value == null ? 'not measured' : `${(value * 100).toFixed(1)}%`;
const markdown = `# Incident agent evaluation ${runId}\n\n- Generated: ${report.generatedAtUtc}\n- API: ${baseUrl}\n- Result: **${report.passed ? 'PASS' : 'FAIL'}**\n- Completed: ${report.aggregate.completedScenarios}/${scenarios.length}\n\n## Aggregate metrics\n\n| Metric | Actual | Threshold |\n|---|---:|---:|\n| Severity accuracy | ${percent(report.aggregate.severityAccuracy)} | ${percent(report.thresholds.severityAccuracy)} |\n| Evidence coverage | ${percent(report.aggregate.evidenceCoverage)} | ${percent(report.thresholds.evidenceCoverage)} |\n| Unsupported claim rate | ${percent(report.aggregate.unsupportedClaimRate)} | ${percent(report.thresholds.unsupportedClaimRateMax)} max |\n| Grounded recommendation rate | ${percent(report.aggregate.groundedRecommendationRate)} | ${percent(report.thresholds.groundedRecommendationRate)} |\n| Provider reporting accuracy | ${percent(report.aggregate.providerReportingAccuracy)} | ${percent(report.thresholds.providerReportingAccuracy)} |\n| Fallback correctness | ${percent(report.aggregate.fallbackCorrectness)} | reported |\n| RAG degraded-mode correctness | ${percent(report.aggregate.ragDegradedModeCorrectness)} | reported |\n| Mean end-to-end latency | ${report.aggregate.averageEndToEndLatencyMs.toFixed(0)} ms | observed |\n\n## Scenarios\n\n${results.map(item => `- **${item.scenario}**: ${item.passed ? `SEV ${item.actualSeverity}; evidence ${percent(item.evidenceCoverage)}; fallback ${item.fallbackUsed ? 'used' : 'not used'}; ${item.latencyMs} ms` : `ERROR: ${item.error}`}`).join('\n')}\n\n## Honest omissions\n\n${report.aggregate.note}\n`;
await fs.writeFile(markdownPath, markdown);
console.log(JSON.stringify({ passed: report.passed, jsonPath, markdownPath, aggregate: report.aggregate }, null, 2));
