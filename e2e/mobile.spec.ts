import { expect, test } from '@playwright/test';

test('mobile app shell remains usable without horizontal overflow', async ({ page }) => {
  await page.goto('/');
  await expect(page.locator('#healthStatus')).toHaveText('Healthy');
  await expect(page.getByRole('navigation', { name: 'Primary' })).toBeVisible();
  await page.getByRole('button', { name: 'Create Incident' }).click();
  await expect(page.getByLabel('Title')).toBeVisible();
  const dimensions = await page.evaluate(() => ({ width: document.documentElement.clientWidth, scrollWidth: document.documentElement.scrollWidth }));
  expect(dimensions.scrollWidth).toBeLessThanOrEqual(dimensions.width + 1);
});
