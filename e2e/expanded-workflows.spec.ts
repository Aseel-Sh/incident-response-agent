import { test, expect } from '@playwright/test';
import AxeBuilder from '@axe-core/playwright';
import { createIncident, openIncident, recentIncidents } from './support/incident-fixtures';

test('responder UI exposes operational work and read-only admin surfaces', async ({page, request}) => {
  const record = await createIncident(request, {title:'Responder permission walkthrough'});
  await request.put(`/api/incidents/${record.incidentId}/status`, {data:{status:'resolved'}});
  await page.route('**/auth/session', async route => {
    const response = await route.fetch();
    await route.fulfill({json:{...await response.json(), roles:['responder']}});
  });
  await page.goto('/');
  await expect(page.locator('html')).toHaveAttribute('data-role','responder');
  await page.getByRole('button',{name:'Sources',exact:true}).click();
  await expect(page.getByText('Add a project workspace',{exact:true})).toBeHidden();
  await page.getByRole('button',{name:'RAG',exact:true}).click();
  await expect(page.getByText('Connect a runbook source',{exact:true})).toBeHidden();
  await expect(page.locator('[data-runbook-source-action]:visible')).toHaveCount(0);
  await openIncident(page,'Responder permission walkthrough',record.incidentId);
  await expect(page.getByRole('button',{name:'Delete incident',exact:true})).toBeHidden();
  await expect(page.getByRole('button',{name:'Approve and publish',exact:true})).toHaveCount(0);
  await expect(page.locator('[data-knowledge-content]')).toHaveAttribute('readonly','');
  await expect(page.getByRole('button',{name:'Assign to me',exact:true})).toBeVisible();
  await expect(page.getByRole('button',{name:'Reopen',exact:true})).toBeVisible();
});

test('unknown assignments are rejected and acknowledgement never changes ownership', async ({request}) => {
  const record = await createIncident(request,{title:'Assignment validation'});
  const directory = await (await request.get('/api/responders')).json();
  const self = directory.find((p:any) => p.name === 'local-operator');
  expect(self.id).toBe('development:local-operator');
  expect((await request.put(`/api/incidents/${record.incidentId}/coordination`,{data:{assignee:'made-up-user'}})).status()).toBe(400);
  expect((await request.put(`/api/incidents/${record.incidentId}/coordination`,{data:{assignee:self.id}})).ok()).toBeTruthy();
  expect((await request.put(`/api/incidents/${record.incidentId}/coordination`,{data:{assignee:null,acknowledge:true}})).ok()).toBeTruthy();
  const saved=(await recentIncidents(request)).find(i=>i.incidentId===record.incidentId);
  expect(saved.assignee).toBe(self.id);
  expect(saved.acknowledgedBy).toBe('local-operator');
  expect(saved.status).not.toBe('resolved');
});

test('both themes pass contrast across tools and populated incident coordination', async ({page,request},testInfo) => {
  test.setTimeout(120_000);
  const record=await createIncident(request,{title:'Contrast audit incident'});
  await request.put(`/api/incidents/${record.incidentId}/status`,{data:{status:'active'}});
  await request.put(`/api/incidents/${record.incidentId}/status`,{data:{status:'mitigated'}});
  await page.goto('/');
  await expect(page.locator('[data-controller-ready="true"]')).toBeVisible();
  expect((await request.get('/favicon.svg')).ok()).toBeTruthy();
  await expect(page.locator('link[rel="icon"]')).toHaveAttribute('href','/favicon.svg');
  for(const theme of ['light','dark']) {
    await page.evaluate(theme=>{document.documentElement.dataset.theme=theme;localStorage.setItem('incidentops.theme',theme)},theme);
    for(const tab of ['Incidents','History','Sources','Diagnostics','RAG','Monitor','Config']) {
      await page.getByRole('button',{name:tab,exact:true}).click();
      const result=await new AxeBuilder({page}).withRules(['color-contrast']).analyze();
      expect(result.violations,`${theme}/${tab}: ${JSON.stringify(result.violations)}`).toEqual([]);
    }
    await openIncident(page,'Contrast audit incident',record.incidentId);
    await expect(page.locator('.timeline-event[data-tone="warning"]')).not.toHaveCount(0);
    const detail=await new AxeBuilder({page}).withTags(['wcag2a','wcag2aa','wcag21aa']).analyze();
    expect(detail.violations,`${theme}/detail: ${JSON.stringify(detail.violations)}`).toEqual([]);
    await page.screenshot({path:testInfo.outputPath(`detail-${theme}.png`),fullPage:true});
    await page.getByRole('button',{name:'Close history detail'}).click();
  }
});
