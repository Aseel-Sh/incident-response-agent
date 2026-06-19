import { expect, test, type APIRequestContext, type Page } from '@playwright/test';
import { feedbackCases, learningFixtures } from './fixtures/learning/golden-fixtures';
import { openHistory, openIncident, recentIncidents } from './support/incident-fixtures';

const timestamp = '2026-06-19T12:30:00Z';

async function createViaApi(request: APIRequestContext, fixture: any, sessionId?: string) {
  const response = await request.post('/api/incidents/analyze', { data: { ...fixture, timestamp, sessionId } });
  expect(response.ok(), await response.text()).toBeTruthy();
  return response.json();
}

async function createViaUi(page: Page, fixture: any, sessionId?: string) {
  await page.getByRole('button', { name: 'Dashboard', exact: true }).click();
  await page.getByRole('button', { name: 'Create Incident' }).click();
  await page.locator('#incidentForm [name="title"]').fill(fixture.title);
  await page.locator('#incidentForm [name="description"]').fill(fixture.description);
  await page.locator('#incidentForm [name="severity"]').selectOption(fixture.severity);
  await page.locator('#incidentForm [name="serviceName"]').fill(fixture.serviceName);
  await page.locator('#incidentForm [name="environment"]').fill(fixture.environment);
  await page.locator('#incidentForm [name="tags"]').fill(fixture.tags.join(', '));
  if (sessionId) await page.locator('#incidentForm [name="sessionId"]').fill(sessionId);
  const confirmation = page.waitForResponse(response =>
    response.request().method() === 'POST' && /\/api\/incidents\/candidates\/[^/]+\/confirm/.test(new URL(response.url()).pathname));
  await page.getByRole('button', { name: 'Create candidate & confirm' }).click();
  const response = await confirmation;
  expect(response.ok(), await response.text()).toBeTruthy();
  return response.json();
}

async function addOutcome(request: APIRequestContext, incidentId: string, description: string, status: string) {
  const response = await request.post(`/api/incidents/${incidentId}/outcomes`, { data: { description, status } });
  expect(response.ok()).toBeTruthy();
  return response.json();
}

async function resolve(request: APIRequestContext, incidentId: string) {
  const response = await request.put(`/api/incidents/${incidentId}/status`, { data: { status: 'resolved' } });
  expect(response.ok()).toBeTruthy();
}

async function review(request: APIRequestContext, incidentId: string, decision: 'approved' | 'rejected', content?: string) {
  const response = await request.post(`/api/incidents/${incidentId}/knowledge-review`, { data: { decision, content } });
  expect(response.ok(), await response.text()).toBeTruthy();
  return response.json();
}

test.describe.serial('@learning safe feedback, outcomes, knowledge approval, and follow-up', () => {
  let approved: any;
  let rejected: any;
  let deleted: any;
  let recurring: any;
  const feedbackIncidentIds: string[] = [];

  test('all feedback ratings and reason tags persist through the UI and history API', async ({ page, request }) => {
    await page.clock.install({ time: new Date(timestamp) });
    await page.goto('/');

    for (let index = 0; index < feedbackCases.length; index++) {
      const feedback = feedbackCases[index];
      const incident = await createViaUi(page, {
        ...learningFixtures.approvedRecurring,
        title: `Feedback fixture ${index + 1}: ${feedback.usefulness}`
      });
      feedbackIncidentIds.push(incident.incidentId);
      const card = page.locator('#analysisOutput .feedback-card');
      await card.getByLabel('Analysis usefulness').selectOption({ label: feedback.usefulness });
      await card.getByLabel('Recommendation correctness').selectOption({ label: feedback.correctness });
      for (const reason of feedback.reasons) await card.getByLabel(reason, { exact: true }).check();
      await card.getByLabel('Comments (optional)').fill(`Deterministic feedback ${index + 1}`);
      const saved = page.waitForResponse(response => response.url().endsWith(`/api/incidents/${incident.incidentId}/feedback`));
      await card.getByRole('button', { name: 'Save feedback' }).click();
      expect((await saved).ok()).toBeTruthy();
      await expect(card.locator('[data-feedback-status]')).toHaveText('Feedback saved.');
    }

    const records = await recentIncidents(request);
    const stored = feedbackIncidentIds.map(id => records.find(item => item.incidentId === id));
    expect(stored.every(item => item?.feedback?.length === 1)).toBeTruthy();
    expect(stored.flatMap(item => item.feedback[0].reasonTags).map((tag: string) => tag.toLowerCase()).sort()).toEqual([
      'bad remediation', 'hallucinated evidence', 'ignored runbook', 'missing evidence', 'other',
      'repeated failed past action', 'shallow', 'wrong root cause', 'wrong sev'
    ]);

    await openIncident(page, 'Feedback fixture 3: Not Useful', feedbackIncidentIds[2]);
    await expect(page.getByRole('dialog')).toContainText('Saved feedback (1)');
    await expect(page.getByRole('dialog')).toContainText('not useful / wrong');
    await expect(page.getByRole('dialog')).toContainText('repeated failed past action');
  });

  test('worked, partial, and failed outcomes feed a complete human-approved knowledge proposal', async ({ page, request }) => {
    await page.clock.install({ time: new Date(timestamp) });
    await page.goto('/');
    approved = await createViaUi(page, learningFixtures.approvedRecurring);

    const outcomes = [
      ['Increase pool capacity from 40 to 60', 'worked'],
      ['Throttle checkout batch traffic by 20 percent', 'partial'],
      ['Restart the database primary', 'failed']
    ] as const;
    for (const [description, status] of outcomes) {
      const card = page.locator('#analysisOutput .outcome-card');
      await card.getByLabel('Log an action outcome:').fill(description);
      await card.getByLabel('Outcome status').selectOption(status);
      const saved = page.waitForResponse(response => response.url().endsWith(`/api/incidents/${approved.incidentId}/outcomes`));
      await card.getByRole('button', { name: 'Log', exact: true }).click();
      expect((await saved).ok()).toBeTruthy();
      await expect(card).toContainText(description);
      await expect(card).toContainText(status);
    }

    await resolve(request, approved.incidentId);
    await openIncident(page, learningFixtures.approvedRecurring.title, approved.incidentId);
    const dialog = page.getByRole('dialog');
    await expect(dialog).toContainText('action recorded');
    await expect(dialog).toContainText('runbook update generated');
    const proposal = dialog.locator('[data-knowledge-content]');
    const original = await proposal.inputValue();
    for (const expected of [
      '## Incident context', 'What happened:', 'Severity: SEV-2', 'Service: database-api', 'Environment: production',
      '## Grounded evidence', '## Actions tried', '[worked] Increase pool capacity',
      '[partial] Throttle checkout', '[failed] Restart the database primary', '## Recommended future steps'
    ]) expect(original).toContain(expected);

    const edited = `${original}\n\n## Human review\nValidated during the incident review.`;
    await proposal.fill(edited);
    page.once('dialog', dialogEvent => dialogEvent.accept());
    const reviewed = page.waitForResponse(response => response.url().endsWith(`/api/incidents/${approved.incidentId}/knowledge-review`));
    await dialog.getByRole('button', { name: 'Approve' }).click();
    expect((await reviewed).ok()).toBeTruthy();
    await expect(dialog).toContainText('approved');

    const stored = (await recentIncidents(request)).find(item => item.incidentId === approved.incidentId);
    expect(stored.actionOutcomes.map((item: any) => item.status)).toEqual(['worked', 'partial', 'failed']);
    expect(stored.timeline.filter((item: any) => item.type === 'action recorded')).toHaveLength(3);
    expect(stored.proposedKnowledgeUpdate).toMatchObject({ status: 'approved', content: edited });
    expect(stored.timeline.map((item: any) => item.type)).toContain('runbook update approved');
  });

  test('rejected, deleted, false-positive, and ignored sources remain excluded from learning', async ({ page, request }) => {
    await page.clock.install({ time: new Date(timestamp) });
    await page.goto('/');

    rejected = await createViaApi(request, learningFixtures.rejectedUpdate);
    await resolve(request, rejected.incidentId);
    await openIncident(page, learningFixtures.rejectedUpdate.title, rejected.incidentId);
    page.once('dialog', event => event.accept());
    const rejection = page.waitForResponse(response => response.url().endsWith(`/api/incidents/${rejected.incidentId}/knowledge-review`));
    await page.getByRole('dialog').getByRole('button', { name: 'Reject' }).click();
    expect((await rejection).ok()).toBeTruthy();

    deleted = await createViaApi(request, learningFixtures.deletedIncident);
    await addOutcome(request, deleted.incidentId, 'Deleted worked action must not survive', 'worked');
    await resolve(request, deleted.incidentId);
    await review(request, deleted.incidentId, 'approved');
    expect((await request.delete(`/api/incidents/${deleted.incidentId}`)).status()).toBe(204);

    for (const [fixture, decision] of [
      [learningFixtures.falsePositive, 'false_positive'],
      [learningFixtures.ignoredCandidate, 'ignored']
    ] as const) {
      const candidateResponse = await request.post('/api/incidents/candidates/manual', { data: { ...fixture, timestamp } });
      expect(candidateResponse.ok()).toBeTruthy();
      const candidate = await candidateResponse.json();
      const decisionResponse = await request.post(`/api/incidents/candidates/${candidate.id}/decision`, { data: { decision } });
      expect(decisionResponse.ok()).toBeTruthy();
    }

    const records = await recentIncidents(request);
    expect(records.some(item => item.incidentId === deleted.incidentId)).toBeFalsy();
    expect(records.find(item => item.incidentId === rejected.incidentId)?.proposedKnowledgeUpdate.status).toBe('rejected');
    expect(records.some(item => /False positive|Ignored database/.test(item.incidentTitle))).toBeFalsy();
  });

  test('future recurring analysis reuses approved outcomes and warns about the prior failure only', async ({ page, request }) => {
    await page.clock.install({ time: new Date(timestamp) });
    await page.goto('/');
    recurring = await createViaUi(page, learningFixtures.futureRecurring);

    const match = recurring.similarIncidents.find((item: any) => item.incidentId === approved.incidentId);
    expect(match).toBeTruthy();
    expect(match.successfulActions).toEqual(expect.arrayContaining([
      'Increase pool capacity from 40 to 60', 'Throttle checkout batch traffic by 20 percent'
    ]));
    expect(match.failedActions).toContain('Restart the database primary');
    expect(recurring.similarIncidents.map((item: any) => item.incidentId)).not.toContain(rejected.incidentId);
    expect(recurring.similarIncidents.map((item: any) => item.incidentId)).not.toContain(deleted.incidentId);

    const actionText = recurring.recommendedActions.map((item: any) => item.description).join(' ');
    expect(actionText).toContain('Do not repeat the previously failed action without new evidence: Restart the database primary');
    expect(recurring.recommendedActions.some((item: any) => item.description === 'Restart the database primary')).toBeFalsy();
    expect(recurring.retrievedEvidence.some((item: any) => item.source === `history.incident.${approved.incidentId}`)).toBeTruthy();
    expect(recurring.retrievedEvidence.some((item: any) => /false positive|ignored|rejected|deleted/i.test(item.summary))).toBeFalsy();

    const output = page.locator('#analysisOutput');
    await expect(output).toContainText('Prior action outcomes');
    await expect(output).toContainText('Increase pool capacity from 40 to 60');
    await expect(output).toContainText('Restart the database primary');
    await expect(output).toContainText('do not repeat blindly');

    const storedApproved = (await recentIncidents(request)).find(item => item.incidentId === approved.incidentId);
    expect(storedApproved.proposedKnowledgeUpdate.status).toBe('approved');
  });

  test('follow-up preserves original evidence and receives outcomes from its linked session', async ({ page, request }) => {
    await addOutcome(request, recurring.incidentId, 'Restart application pods during follow-up', 'failed');
    const before = (await recentIncidents(request)).find(item => item.incidentId === recurring.incidentId);
    const originalEvidence = JSON.stringify(before.evidence);

    await page.clock.install({ time: new Date(timestamp) });
    await page.goto('/');
    await openIncident(page, learningFixtures.futureRecurring.title, recurring.incidentId);
    await page.getByRole('dialog').getByRole('button', { name: 'Continue session' }).click();
    const followUp = await createViaUi(page, learningFixtures.followUp, recurring.sessionId);
    expect(followUp.sessionId).toBe(recurring.sessionId);
    expect(followUp.sessionTurnNumber).toBe(2);

    const linked = followUp.similarIncidents.find((item: any) => item.incidentId === recurring.incidentId);
    expect(linked).toBeTruthy();
    expect(linked.failedActions).toContain('Restart application pods during follow-up');
    expect(followUp.recommendedActions.map((item: any) => item.description).join(' ')).toContain(
      'Do not repeat the previously failed action without new evidence: Restart application pods during follow-up');

    const after = (await recentIncidents(request)).find(item => item.incidentId === recurring.incidentId);
    expect(JSON.stringify(after.evidence)).toBe(originalEvidence);
    expect(after.incidentDescription).toBe(learningFixtures.futureRecurring.description);

    await openHistory(page);
    await page.getByLabel('Session filter').selectOption('linked');
    await expect(page.locator('#recentOutput .thread-marker').filter({ hasText: 'Original' })).toBeVisible();
    await expect(page.locator('#recentOutput .thread-marker').filter({ hasText: 'Follow-up 1' })).toBeVisible();
  });
});
