import { expect, test } from '@playwright/test';
import { readFile, rm, writeFile } from 'node:fs/promises';
import { resolve } from 'node:path';
import { createIncident, recentIncidents } from './support/incident-fixtures';

const logsPath = resolve('.tmp/e2e-data/logs.json');
const metricsPath = resolve('.tmp/e2e-data/metrics.json');
const recordsPath = resolve('.tmp/e2e-data/incident-records.json');
const workflowPath = resolve('.tmp/e2e-data/incident-records-workflow.json');
let signalTimestamp = '';

const fixtures = {
  errorRate: { metricName: 'request_error_rate', serviceName: 'error-api', environment: 'production', value: 45 },
  latency: { metricName: 'p95_latency', serviceName: 'latency-api', environment: 'production', value: 2200 },
  healthCheck: { metricName: 'health_check_failures', serviceName: 'health-api', environment: 'production', value: 5 },
  duplicate: { metricName: 'request_error_rate', serviceName: 'duplicate-api', environment: 'production', value: 31 }
};

async function ingestMetric(request: any, fixture: Record<string, unknown>) {
  const response = await request.post('/api/signals/metrics', {
    data: { ...fixture, timestamp: signalTimestamp }
  });
  expect(response.status()).toBe(202);
}

async function ingestLog(request: any, data: Record<string, unknown>) {
  const response = await request.post('/api/signals/logs', { data });
  expect(response.status()).toBe(202);
}

test.describe.serial('@monitoring source ingestion and automatic detection', () => {
  let duplicateIncidentId = '';
  let candidates: any[] = [];
  let confirmedIncidentId = '';
  let falsePositiveCandidateId = '';
  let ignoredCandidateId = '';
  let mergedCandidateId = '';

  test.beforeAll(async () => {
    signalTimestamp = new Date().toISOString();
    await Promise.all([
      rm(recordsPath, { force: true }),
      rm(workflowPath, { force: true }),
      writeFile(logsPath, '[]\n'),
      writeFile(metricsPath, '[]\n')
    ]);
  });

  test('configured sources and monitoring controls report verified, persisted state honestly', async ({ page, request }) => {
    await page.goto('/');
    await page.getByRole('button', { name: 'Monitor', exact: true }).click();

    const pause = page.getByRole('button', { name: 'Pause Scanning' });
    await pause.click();
    await expect(page.getByRole('button', { name: 'Resume Scanning' })).toBeVisible();
    await expect(page.locator('#lastScan')).toContainText('Paused');
    await page.reload();
    await page.getByRole('button', { name: 'Monitor', exact: true }).click();
    await expect(page.getByRole('button', { name: 'Resume Scanning' })).toBeVisible();
    await expect(page.locator('#lastScan')).toContainText('Paused');

    const before = Date.now();
    const scanResponsePromise = page.waitForResponse(response =>
      response.request().method() === 'POST' && new URL(response.url()).pathname === '/api/monitoring/scan');
    await page.getByRole('button', { name: 'Manual Refresh' }).click();
    const scanResponse = await scanResponsePromise;
    expect(scanResponse.ok()).toBeTruthy();
    const scanPayload = await scanResponse.json();
    const completedAt = new Date(scanPayload.lastScan.completedAtUtc).getTime();
    expect(completedAt).toBeGreaterThanOrEqual(before);
    expect(completedAt).toBeLessThanOrEqual(Date.now());
    expect(scanPayload.lastScan.scannedSourceCount).toBe(2);
    expect(scanPayload.lastScan.errorCount).toBe(0);
    expect(scanPayload.lastScan.durationMilliseconds).toBeGreaterThanOrEqual(0);
    await expect(page.locator('#lastScan')).toContainText('Scanned 2 sources');
    await expect(page.locator('#lastScan')).toContainText(`${scanPayload.lastScan.candidateCount} signals`);
    await expect(page.locator('#lastScan')).toContainText('0 errors');
    await expect(page.locator('#lastScan')).toContainText(`${(scanPayload.lastScan.durationMilliseconds / 1000).toFixed(1)}s`);

    const storedScan = await request.get('/api/monitoring/state');
    expect(storedScan.ok()).toBeTruthy();
    expect((await storedScan.json()).lastScan.id).toBe(scanPayload.lastScan.id);
    const localScan = await page.evaluate(() => JSON.parse(localStorage.getItem('incidentops.lastScan') || 'null'));
    expect(new Date(localScan.scannedAt).getTime()).toBe(completedAt);

    await page.reload();
    await page.getByRole('button', { name: 'Monitor', exact: true }).click();
    await expect(page.getByRole('button', { name: 'Resume Scanning' })).toBeVisible();
    const reloadedScan = await page.evaluate(() => JSON.parse(localStorage.getItem('incidentops.lastScan') || 'null'));
    expect(new Date(reloadedScan.scannedAt).getTime()).toBe(completedAt);
    await page.getByRole('button', { name: 'Resume Scanning' }).click();
    await expect(page.locator('#lastScan')).toContainText('Active');
    const timestampBeforeActiveReload = await page.evaluate(() => JSON.parse(localStorage.getItem('incidentops.lastScan') || 'null')?.scannedAt);
    await page.reload();
    await page.getByRole('button', { name: 'Monitor', exact: true }).click();
    await expect(page.getByRole('button', { name: 'Pause Scanning' })).toBeVisible();
    await expect(page.locator('#lastScan')).toContainText('Active');
    const timestampAfterActiveReload = await page.evaluate(() => JSON.parse(localStorage.getItem('incidentops.lastScan') || 'null')?.scannedAt);
    expect(timestampAfterActiveReload).toBe(timestampBeforeActiveReload);

    await page.getByRole('button', { name: 'Sources', exact: true }).click();
    const sourcesResponse = await request.get('/api/operations/sources');
    const sources = await sourcesResponse.json();
    await expect(page.locator('#sourcesOutput .source-card')).toHaveCount(sources.length);
    expect(sources.find((source: any) => source.name === 'Logs').status).toBe('connected');
    expect(sources.find((source: any) => source.name === 'Metrics').status).toBe('connected');
    expect(sources.filter((source: any) => ['pending', 'missing'].includes(source.status)).every((source: any) => source.status !== 'connected')).toBeTruthy();
    const serializedSources = JSON.stringify(sources);
    expect(serializedSources).not.toMatch(/sk-[a-z0-9]|hf_[a-z0-9]|apiKey|authorization/i);
    await expect(page.locator('#sourcesOutput')).not.toContainText(/sk-[a-z0-9]|hf_[a-z0-9]/i);

    await rm(metricsPath, { force: true });
    const unavailable = await (await request.get('/api/operations/sources')).json();
    expect(unavailable.find((source: any) => source.name === 'Metrics').status).toBe('missing');
    await page.getByRole('button', { name: 'Sources', exact: true }).click();
    await page.getByRole('button', { name: 'Dashboard', exact: true }).click();
    await page.getByRole('button', { name: 'Sources', exact: true }).click();
    const metricCard = page.locator('#sourcesOutput .source-card').filter({ hasText: 'Metrics' });
    await expect(metricCard).toContainText('missing');
    await expect(metricCard).not.toContainText('connected');
    await page.getByRole('button', { name: 'Monitor', exact: true }).click();
    const degradedScanPromise = page.waitForResponse(response =>
      response.request().method() === 'POST' && new URL(response.url()).pathname === '/api/monitoring/scan');
    await page.getByRole('button', { name: 'Manual Refresh' }).click();
    const degradedScan = await (await degradedScanPromise).json();
    expect(degradedScan.lastScan.errorCount).toBe(1);
    await expect(page.locator('#lastScan')).toContainText('1 errors');
    await writeFile(metricsPath, '[]\n');
  });

  test('UI/API ingestion persists deterministic signals and malformed payloads return structured errors', async ({ page, request }) => {
    const duplicate = await createIncident(request, {
      title: 'duplicate-api request error rate threshold',
      description: 'duplicate-api production request error rate threshold breach',
      severity: 'sev2',
      serviceName: 'duplicate-api',
      environment: 'production',
      tags: ['request_error_rate', 'duplicate']
    });
    duplicateIncidentId = duplicate.incidentId;

    await page.goto('/');
    await page.getByRole('button', { name: 'Diagnostics', exact: true }).click();
    const logForm = page.locator('#logSignalForm');
    await logForm.locator('[name="source"]').fill('warning-api');
    await logForm.locator('[name="level"]').selectOption('Warning');
    await logForm.locator('[name="message"]').fill('production latency warning threshold observed');
    const logAccepted = page.waitForResponse(response => response.request().method() === 'POST' && new URL(response.url()).pathname === '/api/signals/logs');
    await logForm.getByRole('button', { name: 'Post Signal' }).click();
    expect((await logAccepted).status()).toBe(202);
    await expect(page.locator('#ingestionFeedback')).toContainText('Log accepted');
    await ingestLog(request, {
      timestamp: signalTimestamp, source: 'warning-api', level: 'Warning',
      message: 'production latency warning repeated for the same service'
    });

    const metricForm = page.locator('#metricSignalForm');
    await metricForm.locator('[name="serviceName"]').fill('ui-metric-api');
    await metricForm.locator('[name="metricName"]').fill('request_error_rate');
    await metricForm.locator('[name="value"]').fill('45');
    const metricAccepted = page.waitForResponse(response => response.request().method() === 'POST' && new URL(response.url()).pathname === '/api/signals/metrics');
    await metricForm.getByRole('button', { name: 'Post Metric' }).click();
    expect((await metricAccepted).status()).toBe(202);
    await expect(page.locator('#ingestionFeedback')).toContainText('Metric accepted');

    await ingestMetric(request, fixtures.errorRate);
    await ingestMetric(request, fixtures.latency);
    await ingestMetric(request, fixtures.healthCheck);
    await ingestMetric(request, fixtures.duplicate);

    const storedLogs = JSON.parse(await readFile(logsPath, 'utf8'));
    expect(storedLogs.filter((entry: any) => entry.source === 'warning-api')).toHaveLength(2);
    const storedMetrics = JSON.parse(await readFile(metricsPath, 'utf8'));
    expect(storedMetrics.some((series: any) => series.metricName === 'request_error_rate' && series.serviceName === 'ui-metric-api')).toBeTruthy();
    expect(storedMetrics.some((series: any) => series.metricName === 'health_check_failures' && series.serviceName === 'health-api')).toBeTruthy();

    const malformedLog = await request.post('/api/signals/logs', { data: { source: ' ', level: 'NotALevel', message: ' ' } });
    expect(malformedLog.status()).toBe(400);
    expect(malformedLog.headers()['content-type']).toContain('application/problem+json');
    expect((await malformedLog.json()).errors).toBeTruthy();
    const malformedMetric = await request.post('/api/signals/metrics', {
      data: { metricName: '', serviceName: '', environment: '', value: 'not-a-number' }
    });
    expect(malformedMetric.status()).toBe(400);
    expect(malformedMetric.headers()['content-type']).toContain('application/problem+json');

    await metricForm.locator('[name="metricName"]').fill('');
    await metricForm.getByRole('button', { name: 'Post Metric' }).click();
    expect(await metricForm.locator('[name="metricName"]').evaluate((element: HTMLInputElement) => element.validity.valid)).toBeFalsy();
  });

  test('observable log/metric thresholds create evidence-bearing candidates without auto-confirmation', async ({ page, request }) => {
    const incidentsBefore = await recentIncidents(request);
    await page.goto('/');
    await page.getByRole('button', { name: 'Monitor', exact: true }).click();
    const scanPromise = page.waitForResponse(response => response.request().method() === 'POST' && new URL(response.url()).pathname === '/api/monitoring/scan');
    await page.getByRole('button', { name: 'Manual Refresh' }).click();
    const scanPayload = await (await scanPromise).json();
    expect(scanPayload.lastScan.status).toBe('completed');
    candidates = await (await request.get('/api/incidents/detected')).json();

    const errorRate = candidates.find((item: any) => item.serviceName === 'error-api');
    const latency = candidates.find((item: any) => item.serviceName === 'latency-api');
    const health = candidates.find((item: any) => item.serviceName === 'health-api');
    const warning = candidates.find((item: any) => item.serviceName === 'warning-api');
    const duplicate = candidates.find((item: any) => item.serviceName === 'duplicate-api');
    for (const candidate of [errorRate, latency, health, warning, duplicate]) {
      expect(candidate).toBeTruthy();
      expect(candidate.status).toBe('candidate');
      expect(candidate.severity).toMatch(/^sev[1-5]$/);
      expect(candidate.signals.length).toBeGreaterThan(0);
      expect(candidate.timeline.map((event: any) => event.type)).toEqual(expect.arrayContaining(['scan started', 'candidate detected', 'scan completed']));
      expect(candidate.timeline.find((event: any) => event.type === 'candidate detected').evidenceReference).toBeTruthy();
    }
    expect(errorRate.signals).toContain('request_error_rate=45');
    expect(latency.signals).toContain('p95_latency=2200');
    expect(health.signals).toContain('health_check_failures=5');
    expect(warning.source).toContain('logs');
    expect(duplicate.duplicateIncidentId).toBe(duplicateIncidentId);
    expect(duplicate.similarIncidents.some((item: any) => item.incidentId === duplicateIncidentId)).toBeTruthy();
    expect(await recentIncidents(request)).toHaveLength(incidentsBefore.length);

    await page.getByRole('button', { name: 'Dashboard', exact: true }).click();
    for (const candidate of [errorRate, latency, health, warning, duplicate]) {
      const row = page.locator(`[data-row-id="${candidate.id}"]`);
      await expect(row).toBeVisible();
      await expect(row.locator('.severity')).toHaveText(/^SEV-[1-5]$/);
      await expect(row).toContainText('rule');
      await expect(row.locator('.candidate-evidence')).toContainText(candidate.signals[0]);
    }
  });

  test('confirm, false-positive, ignore, and merge decisions persist UI/backend timelines safely', async ({ page, request }) => {
    const errorRate = candidates.find(item => item.serviceName === 'error-api');
    const warning = candidates.find(item => item.serviceName === 'warning-api');
    const health = candidates.find(item => item.serviceName === 'health-api');
    const duplicate = candidates.find(item => item.serviceName === 'duplicate-api');
    falsePositiveCandidateId = warning.id;
    ignoredCandidateId = health.id;
    mergedCandidateId = duplicate.id;
    const incidentCountBefore = (await recentIncidents(request)).length;

    await page.goto('/');
    const confirmResponse = page.waitForResponse(response => response.request().method() === 'POST' && new URL(response.url()).pathname.endsWith(`/candidates/${errorRate.id}/confirm`));
    await page.locator(`[data-row-id="${errorRate.id}"]`).getByRole('button', { name: 'Confirm' }).click();
    confirmedIncidentId = (await (await confirmResponse).json()).incidentId;

    await page.getByRole('button', { name: 'Dashboard', exact: true }).click();
    for (const [candidate, action] of [[warning, 'False positive'], [health, 'Ignore'], [duplicate, 'Merge duplicate']] as const) {
      await page.locator(`[data-row-id="${candidate.id}"]`).getByRole('button', { name: action }).click();
      const confirmation = page.getByRole('alertdialog');
      await expect(confirmation).toBeVisible();
      await confirmation.getByRole('button', { name: /Mark false positive|Ignore candidate|Merge candidate/ }).click();
      await expect(page.locator(`[data-row-id="${candidate.id}"]`)).toHaveCount(0);
    }

    const storedCandidates = await (await request.get('/api/incidents/detected')).json();
    const assertDecision = (id: string, status: string, timelineType: string) => {
      const candidate = storedCandidates.find((item: any) => item.id === id);
      expect(candidate.status).toBe(status);
      expect(candidate.timeline.map((event: any) => event.type)).toContain(timelineType);
    };
    assertDecision(errorRate.id, 'confirmed', 'incident confirmed');
    assertDecision(falsePositiveCandidateId, 'false_positive', 'false positive');
    assertDecision(ignoredCandidateId, 'ignored', 'ignored');
    assertDecision(mergedCandidateId, 'merged', 'merged');

    const records = await recentIncidents(request);
    expect(records).toHaveLength(incidentCountBefore + 1);
    expect(records.some(item => item.incidentId === confirmedIncidentId)).toBeTruthy();
    expect(records.some(item => item.incidentTitle === warning.title || item.incidentTitle === health.title)).toBeFalsy();
    const mergeTarget = records.find(item => item.incidentId === duplicateIncidentId);
    expect(mergeTarget.timeline.map((event: any) => event.type)).toContain('merged');
    expect(records.filter(item => item.serviceName === 'duplicate-api')).toHaveLength(1);
  });

  test('approved similar history is scored/comparable while deleted and candidate exclusions remain untrusted', async ({ page, request }) => {
    const previous = await createIncident(request, {
      title: 'Recurring checkout database latency',
      description: 'checkout-db production database latency and connection timeout during payment queries',
      severity: 'sev2', serviceName: 'checkout-db', environment: 'production', tags: ['database', 'latency', 'timeout']
    });
    expect((await request.put(`/api/incidents/${previous.incidentId}/status`, { data: { status: 'resolved' } })).ok()).toBeTruthy();
    expect((await request.post(`/api/incidents/${previous.incidentId}/knowledge-review`, {
      data: { decision: 'approved', notes: 'Deterministic E2E approval' }
    })).ok()).toBeTruthy();

    await page.goto('/');
    await page.getByRole('button', { name: 'Create Incident' }).click();
    await page.locator('#incidentForm [name="title"]').fill('Recurring checkout database latency follow-up');
    await page.locator('#incidentForm [name="description"]').fill('checkout-db production database latency and connection timeout during payment queries');
    await page.locator('#incidentForm [name="severity"]').selectOption('sev2');
    await page.locator('#incidentForm [name="serviceName"]').fill('checkout-db');
    await page.locator('#incidentForm [name="environment"]').fill('production');
    await page.locator('#incidentForm [name="tags"]').fill('database, latency, timeout');
    const analysisResponse = page.waitForResponse(response => response.request().method() === 'POST' && /\/confirm$/.test(new URL(response.url()).pathname));
    await page.getByRole('button', { name: 'Create candidate & confirm' }).click();
    const analysis = await (await analysisResponse).json();
    expect(analysis.similarIncidents.some((item: any) => item.incidentId === previous.incidentId && item.score > 0)).toBeTruthy();
    expect(analysis.similarIncidents.some((item: any) => [falsePositiveCandidateId, ignoredCandidateId, mergedCandidateId].includes(item.incidentId))).toBeFalsy();

    const similar = page.locator(`[data-similar-id="${previous.incidentId}"]`);
    await expect(similar).toBeVisible();
    await expect(similar.locator('.similar-score')).toHaveClass(/score-(green|yellow|red)/);
    await similar.getByRole('button', { name: 'Compare' }).click();
    await expect(page.locator('#comparePanel')).toBeVisible();
    await expect(page.locator('#comparePanel')).toContainText('Previous');
    await similar.getByRole('button', { name: 'Compare' }).click();
    await expect(page.locator('#comparePanel')).toBeHidden();

    expect((await request.delete(`/api/incidents/${previous.incidentId}`)).status()).toBe(204);
    const probe = await createIncident(request, {
      title: 'Recurring checkout database latency probe',
      description: 'checkout-db production database latency and connection timeout during payment queries',
      severity: 'sev2', serviceName: 'checkout-db', environment: 'production', tags: ['database', 'latency', 'timeout']
    });
    expect((probe.similarIncidents as any[]).some(item => item.incidentId === previous.incidentId)).toBeFalsy();
  });
});
