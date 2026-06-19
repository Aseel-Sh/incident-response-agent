import { expect, test, type APIRequestContext, type Page } from '@playwright/test';
import { goldenFixtures, type GoldenAnalysisFixture } from './fixtures/ai/golden-fixtures';

const fixtureTime = new Date('2026-06-19T12:30:00Z');

async function analyzeThroughUi(page: Page, fixture: GoldenAnalysisFixture) {
  await page.clock.install({ time: fixtureTime });
  await page.goto('/');
  await page.getByRole('button', { name: 'Create Incident' }).click();
  await page.locator('#incidentForm [name="title"]').fill(fixture.title);
  await page.locator('#incidentForm [name="description"]').fill(fixture.description);
  await page.locator('#incidentForm [name="severity"]').selectOption(fixture.severity);
  await page.locator('#incidentForm [name="serviceName"]').fill(fixture.serviceName);
  await page.locator('#incidentForm [name="environment"]').fill(fixture.environment);
  await page.locator('#incidentForm [name="tags"]').fill(fixture.tags.join(', '));
  const confirmation = page.waitForResponse(response =>
    response.request().method() === 'POST' && /\/api\/incidents\/candidates\/[^/]+\/confirm/.test(new URL(response.url()).pathname));
  await page.getByRole('button', { name: 'Create candidate & confirm' }).click();
  const response = await confirmation;
  return { response, body: await response.json().catch(() => null) };
}

function assertGrounded(result: any, fixture: GoldenAnalysisFixture) {
  expect(result.providerTransparency.modelProvider).toBe(result.analysisProvider);
  expect(result.providerTransparency.model).toBe(result.analysisModel);
  expect(result.incidentSummary).toBeTruthy();
  expect(result.knownFacts.length).toBeGreaterThan(0);
  expect(result.unknowns.length).toBeGreaterThan(0);
  expect(result.retrievedEvidence.length).toBeGreaterThan(0);
  expect(result.rootCauseHypotheses.length).toBeGreaterThan(0);
  expect(result.recommendedActions.length).toBeGreaterThan(0);
  expect(result.confidence).toMatch(/^(High|Medium|Low)$/);
  expect(result.quality).toMatchObject({
    evidenceCoverage: expect.stringMatching(/^(High|Medium|Low)$/),
    runbookMatchQuality: expect.stringMatching(/^(High|Medium|Low)$/),
    recommendationSpecificity: expect.stringMatching(/^(High|Medium|Low)$/)
  });

  const evidenceSources = result.retrievedEvidence.map((item: any) => item.source);
  const allowed = new Set(evidenceSources);
  for (const required of fixture.expected.requiredEvidence) {
    expect(evidenceSources.some((source: string) => source.startsWith(required)), `missing evidence ${required}`).toBeTruthy();
  }
  for (const fact of result.knownFacts) {
    expect(fact.evidenceReferences.length, `unsupported fact: ${fact.claim}`).toBeGreaterThan(0);
    expect(fact.evidenceReferences.every((source: string) => allowed.has(source))).toBeTruthy();
  }
  for (const hypothesis of result.rootCauseHypotheses) {
    expect(hypothesis.evidenceReferences.length, `unsupported hypothesis: ${hypothesis.description}`).toBeGreaterThan(0);
    expect(hypothesis.evidenceReferences.every((source: string) => allowed.has(source))).toBeTruthy();
  }
  for (const action of result.recommendedActions) {
    expect(action.supportingSignals.length, `unsupported action: ${action.description}`).toBeGreaterThan(0);
    expect(action.supportingSignals.every((source: string) => allowed.has(source))).toBeTruthy();
  }

  const hypothesisText = result.rootCauseHypotheses.map((item: any) => item.description).join(' ').toLowerCase();
  const unknownText = result.unknowns.join(' ').toLowerCase();
  const actionText = result.recommendedActions.map((item: any) => item.description).join(' ').toLowerCase();
  fixture.expected.expectedHypothesisTerms.forEach(term => expect(hypothesisText).toContain(term.toLowerCase()));
  fixture.expected.requiredUnknownTerms.forEach(term => expect(unknownText).toContain(term.toLowerCase()));
  fixture.expected.usefulActionTerms.forEach(term => expect(actionText).toContain(term.toLowerCase()));
  fixture.expected.forbiddenActionTerms.forEach(term => expect(actionText).not.toContain(term.toLowerCase()));
  expect(JSON.stringify(result)).not.toMatch(/fake-log|fake-metric|invented-runbook/i);
}

async function expectAnalysisUi(page: Page, fixture: GoldenAnalysisFixture) {
  const output = page.locator('#analysisOutput');
  for (const label of [fixture.expected.severityLabel, fixture.title, fixture.description, fixture.serviceName,
    'Known facts', 'Hypotheses', 'Unknowns', 'Evidence', 'Runbook matches', 'Confidence',
    'Analysis quality', 'Recommended actions', 'Provider information']) {
    await expect(output).toContainText(label);
  }
}

async function storedAnalysis(request: APIRequestContext, incidentId: string) {
  const response = await request.get('/api/incidents/recent');
  expect(response.ok()).toBeTruthy();
  const body = await response.json();
  return body.find((item: any) => item.incidentId === incidentId);
}

test.describe.serial('@ai evidence-grounded analysis, RAG, and fallback honesty', () => {
  test('model + RAG path is evidence-grounded, specific, and honestly displayed', async ({ page, request }) => {
    const fixture = goldenFixtures.clearDatabaseLatency;
    const { response, body } = await analyzeThroughUi(page, fixture);
    expect(response.ok()).toBeTruthy();
    expect(body.providerTransparency).toMatchObject({
      modelProvider: 'OpenRouter', model: 'fixture/model-v1',
      vectorStore: 'sqlite', usedModelFallback: false, isDegraded: false
    });
    expect(body.providerTransparency.embeddingProvider).toMatch(/^huggingface/);
    assertGrounded(body, fixture);
    expect(body.runbookMatches.some((item: any) => /database/i.test(item.title))).toBeTruthy();
    const irrelevantMatches = body.runbookMatches.filter((item: any) => /cache/i.test(item.title));
    expect(irrelevantMatches.length).toBeLessThanOrEqual(1);
    expect(body.rootCauseHypotheses.concat(body.recommendedActions).some((item: any) =>
      JSON.stringify(item).includes('irrelevant-cache'))).toBeFalsy();
    await expectAnalysisUi(page, fixture);
    await expect(page.locator('#analysisOutput')).toContainText('Model fallbackNo');
    await expect(page.locator('#analysisOutput')).not.toContainText('Fallback:');
    expect((await storedAnalysis(request, body.incidentId)).providerTransparency).toMatchObject({
      modelProvider: 'OpenRouter', model: 'fixture/model-v1', usedModelFallback: false
    });
  });

  test('metrics-only analysis discloses missing logs and empty runbook matches', async ({ page }) => {
    const fixture = goldenFixtures.metricsOnly;
    const { response, body } = await analyzeThroughUi(page, fixture);
    expect(response.ok()).toBeTruthy();
    assertGrounded(body, fixture);
    expect(body.retrievedEvidence.some((item: any) => item.source === 'tool.logs')).toBeFalsy();
    expect(body.runbookMatches).toHaveLength(0);
    await expect(page.locator('#analysisOutput')).toContainText('No runbook matched. This is not evidence that no runbook exists.');
    await expect(page.locator('#analysisOutput')).toContainText(/no matching log/i);
  });

  test('conflicting evidence remains a hypothesis instead of becoming a claimed outage', async ({ page }) => {
    const fixture = goldenFixtures.conflictingEvidence;
    const { response, body } = await analyzeThroughUi(page, fixture);
    expect(response.ok()).toBeTruthy();
    assertGrounded(body, fixture);
    expect(body.confidence).toBe('Low');
    await expect(page.locator('#analysisOutput')).toContainText(/conflicting log and metric evidence/i);
    await expect(page.locator('#analysisOutput')).not.toContainText('confirmed outage');
  });

  test('empty strict-model output retries prompt-only JSON and persists the retry disclosure', async ({ page, request }) => {
    const fixture = goldenFixtures.emptyModelRetry;
    const { response, body } = await analyzeThroughUi(page, fixture);
    expect(response.ok()).toBeTruthy();
    assertGrounded(body, fixture);
    expect(body.providerTransparency).toMatchObject({ usedStructuredOutputRetry: true, usedModelFallback: false });
    expect(body.providerTransparency.structuredOutputRetryReason).toContain('empty content');
    await expect(page.locator('#analysisOutput')).toContainText('Structured retrySucceeded');
    await expect(page.locator('#analysisOutput')).toContainText('Structured-output retry:');
    const calls = await (await request.get('http://127.0.0.1:5199/__requests')).json();
    expect(calls.filter((item: any) => item.title === fixture.title).map((item: any) => item.strict)).toEqual([true, false]);
    expect(calls.find((item: any) => item.title === fixture.title)).toEqual(expect.objectContaining({
      model: 'fixture/model-v1', authorizationPresent: true
    }));
    expect((await storedAnalysis(request, body.incidentId)).providerTransparency.usedStructuredOutputRetry).toBe(true);
  });

  test('model outage uses local fallback only with evidence and shows the persisted reason', async ({ page, request }) => {
    const fixture = goldenFixtures.modelUnavailable;
    const { response, body } = await analyzeThroughUi(page, fixture);
    expect(response.ok()).toBeTruthy();
    expect(body.usedFallbackAnalysis).toBe(true);
    expect(body.providerTransparency).toMatchObject({ modelProvider: 'local-prompt', model: 'local', usedModelFallback: true });
    expect(body.fallbackReason).toMatch(/503|model outage|service unavailable/i);
    await expect(page.locator('#analysisOutput')).toContainText('Model fallbackYes');
    await expect(page.locator('#analysisOutput')).toContainText('Fallback: OpenRouter model analysis failed');
    const stored = await storedAnalysis(request, body.incidentId);
    expect(stored.providerTransparency.usedModelFallback).toBe(true);
    expect(stored.providerTransparency.fallbackReason).toBe(body.providerTransparency.fallbackReason);
  });

  test('model and local fallback failure returns a structured error without fabricated analysis', async ({ page }) => {
    const fixture = goldenFixtures.bothUnavailable;
    const { response, body } = await analyzeThroughUi(page, fixture);
    expect(response.ok()).toBeFalsy();
    expect(response.status()).toBeGreaterThanOrEqual(500);
    expect(JSON.stringify(body)).toMatch(/insufficient operational evidence|analysis unavailable/i);
    await expect(page.locator('#analysisStatus')).toHaveText('Analysis failed.');
    await expect(page.locator('#analysisOutput')).toContainText(/insufficient operational evidence|analysis unavailable/i);
    await expect(page.locator('#analysisOutput')).not.toContainText('Recommended actions');
  });

  test('embedding outage degrades RAG but still uses and reports the configured model', async ({ page, request }) => {
    const fixture = goldenFixtures.ragUnavailable;
    const { response, body } = await analyzeThroughUi(page, fixture);
    expect(response.ok()).toBeTruthy();
    assertGrounded(body, fixture);
    expect(body.providerTransparency).toMatchObject({
      modelProvider: 'OpenRouter', model: 'fixture/model-v1', usedModelFallback: false, isDegraded: true
    });
    expect(body.providerTransparency.degradedReason).toBeTruthy();
    await expect(page.locator('#analysisStatus')).toContainText('RAG degraded');
    await expect(page.locator('#analysisOutput')).toContainText('RAG degraded:');
    await expect(page.locator('#analysisOutput')).toContainText('Model fallbackNo');
    expect((await storedAnalysis(request, body.incidentId)).providerTransparency).toMatchObject({
      modelProvider: 'OpenRouter', usedModelFallback: false, isDegraded: true
    });
  });
});
