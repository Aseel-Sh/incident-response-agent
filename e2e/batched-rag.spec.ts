import {test,expect} from '@playwright/test';
test('external batched retrieval fails closed on quota and recovers without restarting',async({request,page})=>{
  await page.goto('/#rag');
  await expect(page.locator('#ragSummary')).toContainText('openrouter');
  await expect(page.locator('#ragSummary')).not.toContainText('unavailable');
  const batchRequests=await (await request.get('http://127.0.0.1:5199/__requests')).json();
  expect(batchRequests.some((item:any)=>item.kind==='embedding-batch'&&item.count>1)).toBeTruthy();
  const outage=await (await request.get('/api/runbooks/search?query=RAG_OUTAGE_TEST')).json();
  expect(outage.ragStatus).toBe('unavailable');
  expect(outage.degradedReason).toContain('HTTP 429');
  expect(outage.embeddingProvider).toBe('openrouter');
  const recovered=await (await request.get('/api/runbooks/search?query=database%20pool%20timeout')).json();
  expect(recovered.ragStatus).not.toBe('unavailable');
  expect(recovered.isDegraded).toBeFalsy();
  expect(recovered.embeddingProvider).toBe('openrouter');
});

for (const title of ['Malformed always disabled fallback', 'Empty completion choices disabled fallback']) {
test(`${title} preserves a failed incident`, async ({ request, page }) => {
  await page.goto('/');
  await page.getByRole('button', { name: 'Create Incident' }).click();
  await page.locator('#incidentForm [name="title"]').fill(title);
  await page.locator('#incidentForm [name="description"]').fill('checkout database connection timeout');
  await page.locator('#incidentForm [name="severity"]').selectOption('sev2');
  const responsePromise = page.waitForResponse(response => /\/candidates\/.*\/confirm/.test(response.url()));
  await page.getByRole('button', { name: 'Create candidate & confirm' }).click();
  const saved = await (await responsePromise).json();
  expect(saved.analysisState).toBe('failed');
  await expect(page.locator('#analysisOutput')).toContainText('Local analysis fallback is disabled');
  await expect(page.locator('#analysisOutput')).not.toContainText('BytePositionInLine');
  await expect(page.locator('#analysisOutput')).not.toContainText("Parameter 'index'");
  const records = await (await request.get('/api/incidents/recent?maxResults=100')).json();
  expect(records.find((item:any) => item.incidentId === saved.incidentId).analysisState).toBe('failed');
  const calls = await (await request.get('http://127.0.0.1:5199/__requests')).json();
  expect(calls.filter((item:any) => item.title === title)).toHaveLength(2);
});
}
