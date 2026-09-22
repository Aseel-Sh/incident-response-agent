import { test, expect } from '@playwright/test';
import AxeBuilder from '@axe-core/playwright';

const signedOut = { browserLoginEnabled: true, authenticated: false, canAccess: false, roles: [], csrfToken: null };

test('anonymous deep links show only sign-in and never request workspace data', async ({ page }) => {
  const dataRequests: string[] = [];
  page.on('request', request => { if (new URL(request.url()).pathname.startsWith('/api/')) dataRequests.push(request.url()); });
  await page.route('**/auth/session', route => route.fulfill({ json: signedOut }));
  await page.goto('/#history');
  await expect(page.getByRole('heading', { name: 'Sign in to your workspace' })).toBeVisible();
  await expect(page.locator('[data-react-console]')).toHaveCount(0);
  await expect(page.getByRole('navigation', { name: 'Primary' })).toHaveCount(0);
  await expect(page.getByRole('link', { name: 'Continue to sign in' })).toHaveAttribute('href', '/auth/login?returnUrl=%2F%23history');
  expect(dataRequests).toEqual([]);
  for (const theme of ['light', 'dark']) {
    await page.evaluate(theme => { document.documentElement.dataset.theme = theme; }, theme);
    expect((await new AxeBuilder({ page }).withTags(['wcag2a', 'wcag2aa', 'wcag21aa']).analyze()).violations).toEqual([]);
  }
});

test('session failures stay closed and can be retried', async ({ page }) => {
  let failing = true;
  await page.route('**/auth/session', route => failing ? route.fulfill({ status: 503 }) : route.fulfill({ json: signedOut }));
  await page.goto('/');
  await expect(page.getByRole('heading', { name: 'Unable to check your session' })).toBeVisible();
  await expect(page.locator('[data-react-console]')).toHaveCount(0);
  failing = false;
  await page.getByRole('button', { name: 'Retry connection' }).click();
  await expect(page.getByRole('heading', { name: 'Sign in to your workspace' })).toBeVisible();
});

test('an authenticated account without access sees a permission page', async ({ page }) => {
  await page.route('**/auth/session', route => route.fulfill({ json: { ...signedOut, authenticated: true, name: 'Unassigned operator', csrfToken: 'test-csrf' } }));
  await page.goto('/#sources');
  await expect(page.getByRole('heading', { name: 'Workspace access required' })).toBeVisible();
  await expect(page.getByRole('button', { name: 'Sign out' })).toBeVisible();
  await expect(page.locator('[data-react-console]')).toHaveCount(0);
});

test('expired sessions remove the workspace and preserve the destination', async ({ page }) => {
  let expired = false;
  await page.route('**/auth/session', route => expired ? route.fulfill({ json: signedOut }) : route.continue());
  await page.goto('/#history');
  await expect(page.locator('[data-controller-ready="true"]')).toBeVisible();
  await expect(page.getByRole('button', { name: 'History', exact: true })).toHaveAttribute('aria-current', 'page');
  expired = true;
  await page.route('**/api/incidents/recent?*', route => route.fulfill({ status: 401 }));
  await page.locator('#historyReloadButton').click();
  await expect(page.getByRole('heading', { name: 'Sign in to your workspace' })).toBeVisible();
  await expect(page.getByRole('status')).toContainText('Your session has expired');
  await expect(page.locator('[data-react-console]')).toHaveCount(0);
  await expect(page.getByRole('link', { name: 'Continue to sign in' })).toHaveAttribute('href', '/auth/login?returnUrl=%2F%23history');
});

test('missing configuration has no guest entry and external return URLs are discarded', async ({ page }) => {
  await page.route('**/auth/session', route => route.fulfill({ json: { ...signedOut, browserLoginEnabled: false } }));
  await page.goto('/login?returnUrl=https://evil.example/');
  await expect(page.getByRole('status')).toContainText('Sign-in is unavailable');
  await expect(page.getByRole('link', { name: 'Continue to sign in' })).toHaveCount(0);
  await page.unroute('**/auth/session');
  await page.route('**/auth/session', route => route.fulfill({ json: signedOut }));
  await page.reload();
  await expect(page.getByRole('link', { name: 'Continue to sign in' })).toHaveAttribute('href', '/auth/login?returnUrl=%2F');
});
