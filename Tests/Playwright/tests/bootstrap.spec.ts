import { test, expect } from '@playwright/test';
import { credentials } from '../fixtures/setup';
test('fresh install creates its administrator and first environment through the UI', async ({ page, request }, testInfo) => {
  const posture = process.env.FUSE_TEST_SETUP_POSTURE || 'FullyRestricted';
  const labels: Record<string, string> = {
    FullyRestricted: 'Fully Restricted — recommended',
    RestrictedEditing: 'Restricted Editing',
    Unrestricted: 'Unrestricted',
  };
  expect(Object.keys(labels)).toContain(posture);
  const response = await request.get('/api/security/state');
  expect(response.ok()).toBeTruthy();
  expect((await response.json()).requiresSetup, 'Tests require fresh disposable data; use npm test.').toBe(true);
  await page.goto('/');
  await expect(page).toHaveURL('/security');
  await page.setViewportSize({ width: 375, height: 812 });
  await expect(page.getByText('Fuse starts Fully Restricted:', { exact: false })).toBeVisible();
  await expect(page.getByRole('combobox', { name: 'Administrator' })).toHaveCount(0);
  await page.getByLabel('Username', { exact: true }).fill(credentials.userName);
  await page.getByLabel('Password', { exact: true }).fill(credentials.password);
  await page.getByLabel('Confirm Password', { exact: true }).fill(credentials.password);
  await expect(page.getByRole('radio')).toBeHidden();
  await page.getByText('Change access level', { exact: true }).click();
  await expect(page.getByRole('radio', { name: labels.FullyRestricted, exact: true })).toBeChecked();
  await page.getByRole('radio', { name: 'Unrestricted', exact: true }).check();
  await expect(page.locator('.q-banner').filter({ hasText: 'You will be signed in as the administrator.' }))
    .toContainText('Anyone can read and edit inventory without signing in.');
  await page.getByRole('radio', { name: labels[posture], exact: true }).check();
  await expect(page.getByRole('radio', { name: labels[posture], exact: true })).toBeChecked();
  expect(await page.evaluate(() => document.documentElement.scrollWidth)).toBeLessThanOrEqual(375);
  await page.screenshot({ path: testInfo.outputPath('access-options-mobile.png'), fullPage: true });
  await page.getByText('Change access level', { exact: true }).click();
  await page.getByRole('checkbox', { name: 'Show passwords', exact: true }).check();
  await expect(page.getByLabel('Password', { exact: true })).toHaveAttribute('type', 'text');
  await page.getByRole('checkbox', { name: 'Show passwords', exact: true }).uncheck();
  await expect(page.getByLabel('Password', { exact: true })).toHaveAttribute('type', 'password');
  expect(await page.evaluate(() => document.documentElement.scrollWidth)).toBeLessThanOrEqual(375);
  await page.screenshot({ path: testInfo.outputPath('setup-mobile.png'), fullPage: true });
  await page.setViewportSize({ width: 1280, height: 900 });
  await page.getByRole('button', { name: 'Create Account & Continue', exact: true }).click();
  const wizard = page.getByRole('dialog');
  await expect(wizard.getByText('Set up Fuse Inventory', { exact: true })).toBeVisible();
  await wizard.getByLabel('Environment name', { exact: true }).fill('UI test environment');
  await wizard.getByRole('button', { name: 'Create environment and continue', exact: true }).click();
  await expect(wizard).toBeHidden();
  const sessionResponse = await request.post('/api/security/login', { data: credentials });
  expect(sessionResponse.ok()).toBeTruthy();
  const session = await sessionResponse.json();
  const headers = { Authorization: `Bearer ${session.token}` };
  const state = await (await request.get('/api/security/state', { headers })).json();
  expect(state.posture).toBe(posture);
  expect((await request.get('/api/environment')).status()).toBe(posture === 'FullyRestricted' ? 401 : 200);
  const anonymousWrite = await request.post('/api/tag', { data: { name: 'Setup access check' } });
  expect(anonymousWrite.status()).toBe(posture === 'Unrestricted' ? 201 : 401);
  if (anonymousWrite.ok()) {
    const tag = await anonymousWrite.json();
    expect((await request.delete(`/api/tag/${tag.id}`, { headers })).ok()).toBeTruthy();
  }
  const environments = await (await request.get('/api/environment', { headers })).json();
  expect(environments).toHaveLength(1);
  expect(environments[0].name).toBe('UI test environment');
  await expect(page.getByText('1 of 4 steps complete', { exact: true })).toBeVisible();
  await expect(page.getByRole('button', { name: 'Create application', exact: true })).toBeVisible();
  await page.screenshot({ path: testInfo.outputPath('first-guide.png'), fullPage: true });
  await expect.poll(async () => (await request.get('/api/health/ready')).status()).toBe(200);
});
