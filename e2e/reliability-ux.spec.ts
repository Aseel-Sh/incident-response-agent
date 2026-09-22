import { test, expect } from '@playwright/test';
import { createIncident, openIncident } from './support/incident-fixtures';

test('lifecycle colors remain distinct and severity has no decorative marker in both themes', async ({ page, request }) => {
  const title = `Lifecycle palette regression ${Date.now()}`;
  const record = await createIncident(request, { title });
  await request.put(`/api/incidents/${record.incidentId}/status`, { data: { status: 'mitigated' } });
  await page.goto('/');
  await openIncident(page, title, record.incidentId);
  for (const theme of ['light', 'dark']) {
    await page.evaluate(theme => { document.documentElement.dataset.theme = theme; }, theme);
    const colors = await page.evaluate(() => {
      const root = getComputedStyle(document.documentElement);
      const badge = document.querySelector('#historyDetail .severity')!;
      return { mitigated: root.getPropertyValue('--mitigated'), resolved: root.getPropertyValue('--green'), marker: getComputedStyle(badge, '::before').content };
    });
    expect(colors.mitigated).not.toBe(colors.resolved);
    expect(['none', 'normal']).toContain(colors.marker);
    const counterColors = await page.evaluate(() => ({
      mitigated: getComputedStyle(document.querySelector('#mitigatedCount')!).color,
      resolved: getComputedStyle(document.querySelector('#resolvedCount')!).color
    }));
    expect(counterColors.mitigated).not.toBe(counterColors.resolved);
  }
  await page.getByRole('button', { name: 'Resolve', exact: true }).click();
  await expect(page.getByRole('region', { name: 'Runbook proposal', exact: true })).toContainText('Runbook proposal generated · Pending review');
  await page.getByRole('button', { name: 'Close history detail' }).click();
  await page.getByRole('button', { name: 'Incidents', exact: true }).click();
  await expect(page.getByRole('region', { name: 'Pending runbook proposals' })).toContainText(title);
  await page.getByRole('button', { name: `Review ${title}`, exact: true }).click();
  await expect(page.getByRole('button', { name: 'Approve and publish', exact: true })).toBeVisible();
});

test('dashboard empty state and populated charts use only scoped saved data', async ({ page }) => {
  await page.route('**/api/incidents/recent?**', route => route.fulfill({ json: [] }));
  await page.goto('/');
  await expect(page.getByText('No saved incidents yet. Confirm a candidate to see incident trends.')).toBeVisible();
  await page.unroute('**/api/incidents/recent?**');
  await page.route('**/api/incidents/recent?**', route => route.fulfill({ json: [{
    incidentId: '12345678-1234-1234-1234-123456789abc', title: 'Scoped chart fixture', incidentSummary: 'Scoped chart fixture', severity: 'SEV-4', status: 'mitigated', projectId: 'default', createdAtUtc: '2026-09-15T12:00:00Z', tags: [], evidence: [], timeline: []
  }] }));
  await page.reload();
  await expect(page.getByRole('list', { name: 'Severity distribution' })).toHaveText('SEV-41');
  await expect(page.getByRole('list', { name: 'Current lifecycle' })).toHaveText('Mitigated1');
  await expect(page.getByRole('list', { name: 'Incidents by creation date' })).toHaveText('2026-09-151');
});

test('evaluation uses API scenario fields rather than invented defaults', async ({ page }) => {
  await page.route('**/api/evaluation/scenarios', route => route.fulfill({ json: [] }));
  await page.goto('/');
  await page.getByRole('button', { name: 'Evaluation', exact: true }).click();
  await expect(page.locator('[data-evaluation-run]')).toHaveCount(0);
  await expect(page.locator('#evaluationOutput')).toContainText('No evaluation scenarios');
});

test('runbook source text is escaped and unsuccessful sync stays visible', async ({ page }) => {
  const source = { id: 'source-ui-test', name: '<img src=x onerror=alert(1)>', type: 'markdown_directory', path: 'fixture/path', enabled: true, reachable: true, removable: true, documentCount: 1, sectionCount: 2 };
  await page.route('**/api/runbooks/sources', route => route.fulfill({ json: [source] }));
  await page.route('**/api/runbooks/sources/source-ui-test/synchronize', route => route.fulfill({ json: { ...source, reachable: false, lastError: 'The source cannot be read.' } }));
  await page.goto('/#rag');
  const card = page.locator('.runbook-source-card').filter({ hasText: source.name });
  await expect(card).toBeVisible();
  await expect(card.locator('img')).toHaveCount(0);
  await card.getByRole('button', { name: 'Sync', exact: true }).click();
  await expect(card.getByRole('alert')).toHaveText('The source cannot be read.');
  await expect(card.getByRole('button', { name: 'Sync', exact: true })).toBeEnabled();
});

test('stale pending incidents stay honest and offer recovery without a fake session', async ({ page }) => {
  let analysisState = 'pending';
  await page.route('**/api/incidents/recent?**', route => route.fulfill({ json: [{
    incidentId: '12345678-1234-1234-1234-123456789def', incidentTitle: 'Interrupted analysis', incidentSummary: 'Interrupted analysis',
    severity: 'SEV-3', status: 'new', projectId: 'default', createdAtUtc: '2026-01-01T12:00:00Z', analysisState,
    sessionId: '', tags: [], evidence: [], timeline: []
  }] }));
  await page.route('**/api/incidents/*/analysis/retry', route => {
    analysisState = 'failed';
    return route.fulfill({ status: 503, json: { analysisState, notes: 'The provider returned no completion choices. Local fallback is disabled.' } });
  });
  await page.goto('/#history');
  const row = page.getByRole('row', { name: 'Open Interrupted analysis', exact: true });
  await expect(row).toContainText('pending');
  await row.getByRole('button', { name: 'Interrupted analysis', exact: true }).click();
  await expect(page.getByRole('button', { name: 'Retry analysis', exact: true })).toBeVisible();
  await expect(page.getByRole('button', { name: 'Continue session', exact: true })).toHaveCount(0);
  await expect(page.getByRole('dialog')).toContainText('No investigation session yet');
  await expect(page.getByRole('dialog')).toContainText('Confidence not assessed');
  await page.getByRole('button', { name: 'Retry analysis', exact: true }).click();
  await expect(page.getByRole('dialog')).toContainText('Analysis: failed');
  await expect(page.getByRole('alert')).toContainText('The provider returned no completion choices');
});
