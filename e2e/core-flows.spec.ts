import AxeBuilder from '@axe-core/playwright';
import { expect, test } from '@playwright/test';
import {
  createIncident,
  manualLatencyIncident,
  openHistory,
  openIncident,
  recentIncidents,
  type CreatedIncident
} from './support/incident-fixtures';

test.describe.serial('incident response core flows', () => {
  let primary: CreatedIncident;
  let followUp: CreatedIncident;

  test('app shell exposes honest health, version, runtime config, themes, and useful tabs', async ({ page }) => {
    await page.goto('/');
    await expect(page.locator('#dashboardView')).toHaveClass(/active/);
    await expect(page.locator('#detectedOutput')).toBeVisible();
    await expect(page.locator('#healthStatus')).toHaveText('Healthy');
    await expect(page.locator('#appVersion')).toHaveText(/^v\d+\.\d+\.\d+$/);

    const themeButton = page.getByRole('button', { name: 'Toggle theme' });
    await themeButton.click();
    await expect(page.locator('html')).toHaveAttribute('data-theme', 'dark');
    await themeButton.click();
    await expect(page.locator('html')).toHaveAttribute('data-theme', 'light');

    for (const tab of ['Dashboard', 'Analysis', 'History', 'Sources', 'Ingestion', 'RAG', 'Evaluation', 'Monitor', 'Config']) {
      await page.getByRole('button', { name: tab, exact: true }).click();
      await expect(page.locator(`#${tab.toLowerCase()}View`)).toHaveClass(/active/);
    }

    await expect(page.locator('#configOutput')).toContainText('App Mode');
    await expect(page.locator('#configOutput')).toContainText('Vector Store');
    await expect(page.locator('#configOutput')).not.toContainText(/api.?key|sk-[a-z0-9]|hf_[a-z0-9]/i);
    await page.getByRole('button', { name: 'Help' }).click();
    await expect(page.locator('#toastRegion')).toContainText('Use the sidebar');
  });

  test('manual SEV-2 incident is created through UI and persists with truthful metadata', async ({ page, request }) => {
    await page.goto('/');
    await page.getByRole('button', { name: 'Create Incident' }).click();
    await page.locator('#incidentForm [name="title"]').fill(manualLatencyIncident.title);
    await page.locator('#incidentForm [name="description"]').fill(manualLatencyIncident.description);
    await page.locator('#incidentForm [name="severity"]').selectOption(manualLatencyIncident.severity);
    await page.locator('#incidentForm [name="serviceName"]').fill(manualLatencyIncident.serviceName);
    await page.locator('#incidentForm [name="environment"]').fill(manualLatencyIncident.environment);
    await page.locator('#incidentForm [name="tags"]').fill(manualLatencyIncident.tags.join(', '));

    const confirmation = page.waitForResponse(response =>
      response.request().method() === 'POST' && /\/api\/incidents\/candidates\/[^/]+\/confirm/.test(new URL(response.url()).pathname));
    await page.getByRole('button', { name: 'Create candidate & confirm' }).click();
    const response = await confirmation;
    expect(response.ok()).toBeTruthy();
    primary = await response.json();
    expect(primary.incidentId).toMatch(/^[0-9a-f]{8}-[0-9a-f-]{27}$/i);
    expect(primary.sessionId).toBeTruthy();
    expect(primary.sessionTurnNumber).toBe(1);

    await expect(page.locator('#analysisOutput')).toContainText('SEV-2');
    await expect(page.locator('#analysisOutput')).toContainText(manualLatencyIncident.title);
    await expect(page.locator('#incidentForm input[name="sessionId"]')).toHaveValue(primary.sessionId);

    await page.getByRole('button', { name: 'Dashboard', exact: true }).click();
    await expect(page.locator('#detectedOutput').getByText(manualLatencyIncident.title, { exact: true })).toBeVisible();
    await page.getByRole('searchbox', { name: 'Search incidents' }).fill('checkout-api');
    await expect(page.locator('#detectedOutput').getByText(manualLatencyIncident.title, { exact: true })).toBeVisible();
    await page.getByRole('searchbox', { name: 'Search incidents' }).fill('');
    await page.getByRole('button', { name: 'Active', exact: true }).click();
    await expect(page.locator('#detectedOutput').getByText(manualLatencyIncident.title, { exact: true })).toBeVisible();
    await page.reload();
    await expect(page.locator('#healthStatus')).toHaveText('Healthy');
    await expect(page.locator('#detectedOutput').getByText(manualLatencyIncident.title, { exact: true })).toBeVisible();

    await openIncident(page, manualLatencyIncident.title, primary.incidentId);
    await expect(page.getByRole('dialog')).toContainText('Original analysis');
    await expect(page.getByRole('dialog')).toContainText(manualLatencyIncident.description);
    await expect(page.getByRole('dialog')).toContainText('checkout-api');
    await expect(page.getByRole('dialog')).toContainText('production');
    await expect(page.getByRole('dialog')).toContainText(primary.sessionId);

    const stored = (await recentIncidents(request)).find(item => item.incidentId === primary.incidentId);
    expect(stored).toMatchObject({
      incidentDescription: manualLatencyIncident.description,
      serviceName: manualLatencyIncident.serviceName,
      environment: manualLatencyIncident.environment,
      severity: 'sev2',
      sessionId: primary.sessionId
    });
    expect(stored.tags).toEqual(expect.arrayContaining(manualLatencyIncident.tags));
  });

  test('numeric severity contract rejects invalid values and supports search/filter/color', async ({ page, request }) => {
    const invalid = await request.post('/api/incidents/candidates/manual', {
      data: { ...manualLatencyIncident, severity: 'critical' }
    });
    expect(invalid.status()).toBe(400);
    expect(await invalid.text()).toContain('sev1');

    await page.goto('/#analysis');
    await page.getByRole('button', { name: 'Analysis', exact: true }).click();
    await expect(page.locator('#incidentForm [name="severity"] option')).toHaveText([
      'Select severity', 'SEV-1 — Critical', 'SEV-2 — Major', 'SEV-3 — Partial', 'SEV-4 — Minor', 'SEV-5 — Informational'
    ]);

    await openHistory(page);
    await page.getByRole('button', { name: 'Refresh history' }).click();
    await expect(page.locator('#toastRegion')).toContainText('History refreshed');
    await page.getByLabel('Severity filter').selectOption('sev2');
    const severityBadge = page.locator('#recentOutput .severity-sev2').first();
    await expect(severityBadge).toHaveText('SEV-2');
    const badgeStyle = await severityBadge.evaluate(element => {
      const style = getComputedStyle(element);
      return { color: style.color, background: style.backgroundColor };
    });
    expect(badgeStyle.color).not.toBe(badgeStyle.background);
    await page.getByLabel('Search history').fill('payment authorization');
    await expect(page.locator(`tr[data-history-id="${primary.incidentId}"]`)).toBeVisible();
    await page.getByLabel('Search history').fill('no-such-incident');
    await expect(page.locator('#recentOutput')).toContainText('No incidents match');
  });

  test('lifecycle transitions persist and append timeline events', async ({ page, request }) => {
    await page.goto('/');
    await openIncident(page, manualLatencyIncident.title, primary.incidentId);
    await page.getByRole('button', { name: 'Start work' }).click();
    await expect(page.getByRole('dialog')).toContainText('work started');
    await expect.poll(async () => (await recentIncidents(request)).find(item => item.incidentId === primary.incidentId)?.status).toBe('active');

    await page.getByRole('button', { name: 'Mark mitigated' }).click();
    await expect(page.getByRole('dialog')).toContainText('Mitigated');
    await expect.poll(async () => (await recentIncidents(request)).find(item => item.incidentId === primary.incidentId)?.status).toBe('mitigated');

    await page.getByRole('button', { name: 'Resolve', exact: true }).click();
    await expect(page.getByRole('dialog')).toContainText('Resolved');
    await expect(page.getByRole('dialog')).toContainText('runbook update generated');

    await page.getByRole('button', { name: 'Reopen' }).click();
    await expect(page.getByRole('dialog')).toContainText('Active');
    await page.reload();
    const stored = (await recentIncidents(request)).find(item => item.incidentId === primary.incidentId);
    expect(stored.status).toBe('active');
    expect(stored.timeline.map((event: any) => event.type)).toEqual(expect.arrayContaining([
      'incident created', 'incident confirmed', 'work started', 'analysis started', 'analysis completed', 'mitigated', 'resolved', 'reopened', 'runbook update generated'
    ]));
  });

  test('follow-up creates a linked analysis without overwriting original metadata', async ({ page, request }) => {
    await page.goto('/');
    await openIncident(page, manualLatencyIncident.title, primary.incidentId);
    await page.getByRole('button', { name: 'Continue session' }).click();
    await expect(page.locator('#incidentForm [name="sessionId"]')).toHaveValue(primary.sessionId);
    await page.locator('#incidentForm [name="title"]').fill('Follow-up: checkout API latency after mitigation');
    await page.locator('#incidentForm [name="description"]').fill('Error rate is declining; validate payment authorization latency after mitigation.');
    await page.locator('#incidentForm [name="severity"]').selectOption('sev3');
    await page.locator('#incidentForm [name="serviceName"]').fill('checkout-api');
    await page.locator('#incidentForm [name="environment"]').fill('production');
    await page.locator('#incidentForm [name="tags"]').fill('checkout, follow-up');

    const confirmation = page.waitForResponse(response =>
      response.request().method() === 'POST' && /\/confirm$/.test(new URL(response.url()).pathname));
    await page.getByRole('button', { name: 'Create candidate & confirm' }).click();
    followUp = await (await confirmation).json();
    expect(followUp.incidentId).not.toBe(primary.incidentId);
    expect(followUp.sessionId).toBe(primary.sessionId);
    expect(followUp.sessionTurnNumber).toBe(2);

    await openHistory(page);
    await page.getByLabel('Session filter').selectOption('linked');
    await expect(page.locator('#recentOutput .thread-marker').filter({ hasText: 'Original' })).toBeVisible();
    await expect(page.locator('#recentOutput .thread-marker').filter({ hasText: 'Follow-up 1' })).toBeVisible();
    await openIncident(page, 'Follow-up: checkout API latency after mitigation', followUp.incidentId);
    await expect(page.getByRole('dialog')).toContainText('Follow-up analysis 1');

    const records = await recentIncidents(request);
    expect(records.find(item => item.incidentId === primary.incidentId)).toMatchObject({
      incidentDescription: manualLatencyIncident.description,
      severity: 'sev2'
    });
  });

  test('history filters, pagination, confirmed deletion, and retrieval removal work', async ({ page, request }) => {
    const resolvedPrevious = await createIncident(request, {
      title: 'Resolved previous checkout incident', severity: 'sev3', tags: ['resolved', 'previous']
    });
    const resolvedResponse = await request.put(`/api/incidents/${resolvedPrevious.incidentId}/status`, { data: { status: 'resolved' } });
    expect(resolvedResponse.ok()).toBeTruthy();
    const deleted = await createIncident(request, {
      title: 'Deleted SEV-4 checkout latency fixture', severity: 'sev4', tags: ['deleted', 'latency']
    });
    for (let index = 0; index < 9; index++) {
      await createIncident(request, {
        title: `Pagination fixture ${String(index + 1).padStart(2, '0')}`,
        description: `Checkout API latency pagination fixture ${index + 1}.`,
        severity: index % 2 ? 'sev3' : 'sev2',
        tags: ['pagination']
      });
    }

    await page.goto('/');
    await openHistory(page);
    await expect(page.locator('[data-history-page="2"]')).toBeVisible();
    await page.locator('[data-history-page="2"]').click();
    await expect(page.locator('#recentOutput .pagination-bar')).toContainText('11-12 of 12');

    await page.getByLabel('Service filter').selectOption('checkout-api');
    await page.getByLabel('Status filter').selectOption('resolved');
    await page.getByLabel('Search history').fill('Resolved previous checkout incident');
    await expect(page.locator(`tr[data-history-id="${resolvedPrevious.incidentId}"]`)).toBeVisible();
    await page.getByLabel('Search history').fill('');
    await page.getByLabel('Status filter').selectOption('new');
    await page.getByLabel('Confidence filter').selectOption({ index: 1 });
    await expect(page.locator('#historyResultCount')).not.toHaveText('0 results');

    await page.getByLabel('Search history').fill('Deleted SEV-4 checkout latency fixture');
    await page.locator(`tr[data-history-id="${deleted.incidentId}"] .link-button`).click();
    const deleteButton = page.getByRole('button', { name: 'Delete incident' });
    let sawConfirmation = false;
    page.once('dialog', async dialog => {
      sawConfirmation = true;
      expect(dialog.type()).toBe('confirm');
      expect(dialog.message()).toContain('Permanently delete');
      await dialog.dismiss();
    });
    await deleteButton.click();
    expect(sawConfirmation).toBeTruthy();
    await expect(page.getByRole('dialog')).toBeVisible();

    page.once('dialog', dialog => dialog.accept());
    await deleteButton.click();
    await expect(page.getByRole('dialog')).toBeHidden();
    expect((await recentIncidents(request)).some(item => item.incidentId === deleted.incidentId)).toBeFalsy();

    const probe = await createIncident(request, { title: 'Deleted SEV-4 checkout latency fixture probe', severity: 'sev4' });
    expect((probe.similarIncidents as any[]).some(item => item.incidentId === deleted.incidentId)).toBeFalsy();
    await page.reload();
    await openHistory(page);
    await page.getByLabel('Search history').fill('Deleted SEV-4 checkout latency fixture');
    await expect(page.getByRole('button', { name: 'Deleted SEV-4 checkout latency fixture', exact: true })).toHaveCount(0);
  });

  test('keyboard, labels, focus visibility, modal focus trap/restore, and contrast pass smoke checks', async ({ page, request }) => {
    const accessibleIncident = await createIncident(request, { title: 'Accessible modal focus fixture' });
    await page.goto('/');
    const createButton = page.getByRole('button', { name: 'Create Incident' });
    await createButton.focus();
    const outline = await createButton.evaluate(element => getComputedStyle(element).outlineStyle);
    expect(outline).not.toBe('none');
    await page.keyboard.press('Enter');
    await expect(page.locator('#incidentForm [name="title"]')).toBeFocused();
    for (const name of ['title', 'severity', 'serviceName', 'environment', 'sessionId', 'tags', 'description']) {
      const control = page.locator(`#incidentForm [name="${name}"]`);
      await expect(control).toBeVisible();
      expect(await control.evaluate(element => Boolean(element.closest('label')))).toBeTruthy();
    }

    await page.getByRole('button', { name: 'History', exact: true }).click();
    const opener = page.locator(`tr[data-history-id="${accessibleIncident.incidentId}"] .link-button`);
    await opener.focus();
    await page.keyboard.press('Enter');
    await expect(page.getByRole('button', { name: 'Close history detail' })).toBeFocused();
    await page.getByRole('button', { name: 'Close history detail' }).click();
    await expect(page.getByRole('dialog')).toBeHidden();
    await expect(opener).toBeFocused();
    await opener.click();
    await expect(page.getByRole('button', { name: 'Close history detail' })).toBeFocused();
    await page.keyboard.press('Shift+Tab');
    await expect(page.locator('#historyModal :focus')).toBeVisible();
    await page.keyboard.press('Tab');
    await expect(page.getByRole('button', { name: 'Close history detail' })).toBeFocused();
    await page.keyboard.press('Escape');
    await expect(page.getByRole('dialog')).toBeHidden();
    await expect(opener).toBeFocused();
    await opener.click();
    await page.locator('#historyModal').click({ position: { x: 5, y: 5 } });
    await expect(page.getByRole('dialog')).toBeHidden();
    await expect(opener).toBeFocused();

    const results = await new AxeBuilder({ page }).withTags(['wcag2a', 'wcag2aa', 'wcag21aa']).analyze();
    expect(results.violations, JSON.stringify(results.violations, null, 2)).toEqual([]);
  });
});
