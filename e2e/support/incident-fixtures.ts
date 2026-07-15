import type { APIRequestContext, Page } from '@playwright/test';

export const manualLatencyIncident = {
  title: 'Manual SEV-2 API latency incident',
  description: 'Checkout API latency exceeded 2500ms and HTTP 500 errors increased during payment authorization.',
  severity: 'sev2',
  serviceName: 'checkout-api',
  environment: 'production',
  timestamp: '2026-06-09T12:35:00Z',
  tags: ['checkout', 'latency', '5xx']
};

export type CreatedIncident = {
  incidentId: string;
  sessionId: string;
  sessionTurnNumber: number;
  [key: string]: unknown;
};

export async function createIncident(
  request: APIRequestContext,
  overrides: Partial<typeof manualLatencyIncident> & { sessionId?: string } = {}
): Promise<CreatedIncident> {
  const payload = { ...manualLatencyIncident, ...overrides };
  await request.post('/api/signals/metrics', {
    data: {
      metricName: payload.tags?.includes('request_error_rate') ? 'request_error_rate' : 'p95_latency',
      serviceName: payload.serviceName,
      environment: payload.environment,
      timestamp: payload.timestamp,
      value: payload.tags?.includes('request_error_rate') ? 42 : 2600
    }
  });
  const response = await request.post('/api/incidents/analyze', { data: payload });
  if (!response.ok()) throw new Error(`Fixture incident creation failed: ${response.status()} ${await response.text()}`);
  return response.json();
}

export async function recentIncidents(request: APIRequestContext, maxResults = 100): Promise<any[]> {
  const response = await request.get(`/api/incidents/recent?maxResults=${maxResults}`);
  if (!response.ok()) throw new Error(`Recent incidents request failed: ${response.status()}`);
  return response.json();
}

export async function openHistory(page: Page): Promise<void> {
  await page.getByLabel('Primary').getByRole('button', { name: 'History', exact: true }).click();
  await page.locator('#recentOutput').waitFor();
}

export async function openIncident(page: Page, title: string, incidentId?: string): Promise<void> {
  await openHistory(page);
  const opener = incidentId
    ? page.locator(`tr[data-history-id="${incidentId}"] .link-button`)
    : page.getByRole('button', { name: title, exact: true }).first();
  await opener.click();
  await page.getByRole('dialog').waitFor();
}
