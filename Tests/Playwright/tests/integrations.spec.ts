import { test, expect } from '../fixtures/auth';
for (const [path, text] of [
  ['/sql-integrations', 'No SQL integrations configured.'],
  ['/kuma-dashboard', 'No Kuma integrations are configured.'],
  ['/secret-providers', 'No'],
]) {
  test(`unconfigured integration state: ${path}`, async ({ authenticatedPage: page }) => {
    await page.goto(path);
    if (path === '/secret-providers') {
      await expect(page.getByRole('heading', { name: 'Azure App Config & Key Vault', exact: true })).toBeVisible();
      await page.getByRole('button', { name: 'Add Integration', exact: true }).click();
      await expect(page.getByRole('dialog')).toBeVisible();
      await page.getByRole('dialog').getByRole('button', { name: 'Cancel', exact: true }).click();
      await expect(page.getByRole('dialog')).toBeHidden();
    } else await expect(page.getByText(text, { exact: false })).toBeVisible();
  });
}
test('SQL read failure shows an error and recovers on reload', async ({ authenticatedPage: page }) => {
  await page.route('**/api/SqlIntegration', route => route.fulfill({ status: 500, json: { message: 'SQL list unavailable' } }));
  await page.goto('/sql-integrations');
  await expect(page.locator('.q-banner.bg-red-1')).toBeVisible({ timeout: 15_000 });
  await page.unroute('**/api/SqlIntegration');
  await page.reload();
  await expect(page.getByText('No SQL integrations configured.', { exact: false })).toBeVisible();
  await expect(page.locator('.q-banner.bg-red-1')).toBeHidden();
});
for (const [status, isValid, label] of [['Valid', true, 'Licensed'], ['Expired', false, 'Unlicensed']] as const) {
  test(`license boundary: ${status}`, async ({ authenticatedPage: page }) => {
    await page.route('**/api/License', route => route.fulfill({ json: { status, isValid, message: `Test ${status}`, licenseType: 'Test' } }));
    await page.goto('/tags');
    await expect(page.getByRole('button', { name: label, exact: true })).toBeVisible();
  });
}
// These endpoints stand in for external Kuma connectivity; inventory itself is real.
test('Kuma health boundary renders up/down states and environment filtering', async ({ authenticatedPage: page, adminApi }) => {
  const envs = await (await adminApi.get('/api/environment')).json();
  const appResponse = await adminApi.post('/api/application', { data: { name: 'Monitored UI app' } });
  expect(appResponse.ok()).toBeTruthy();
  const app = await appResponse.json();
  const instanceResponse = await adminApi.post(`/api/application/${app.id}/instances`, { data: { environmentId: envs[0].id, healthUri: 'https://example.test/health' } });
  expect(instanceResponse.ok(), await instanceResponse.text()).toBeTruthy();
  await page.route('**/api/KumaIntegration', route => route.fulfill({ json: [{ id: '11111111-1111-1111-1111-111111111111', name: 'Test Kuma', url: 'https://example.test' }] }));
  let status = 'Up';
  await page.route('**/instances/*/health', route => route.fulfill({ json: { status, monitorId: 1 } }));
  await page.goto('/kuma-dashboard');
  await expect(page.getByText('Up: 1', { exact: true })).toBeVisible();
  await expect(page.getByText('Monitored UI app', { exact: true })).toBeVisible();
  status = 'Down';
  await page.reload();
  await expect(page.getByText('Down: 1', { exact: true })).toBeVisible();
  await page.getByLabel('Filter by Environment', { exact: true }).click();
  await page.getByRole('option', { name: 'UI test environment', exact: true }).click();
  await page.keyboard.press('Escape');
  await expect(page).toHaveURL(new RegExp(`environments=${envs[0].id}`));
  await expect(page.getByText('Monitored UI app', { exact: true })).toBeVisible();
});
for (const kind of ['vault', 'configuration'] as const) {
  test(`Azure ${kind} boundary: failure, retry and returned entries`, async ({ authenticatedPage: page }) => {
    const id = '22222222-2222-2222-2222-222222222222';
    const endpoint = kind === 'vault' ? 'secrets' : 'app-configuration';
    await page.route('**/api/SecretProvider', route => route.fulfill({ json: [{ id, name: 'Test Azure', vaultUri: kind === 'vault' ? 'https://example.vault.azure.net' : 'https://example.azconfig.io', capabilities: 'Check, Read' }] }));
    let failed = true;
    await page.route(`**/api/SecretProvider/${id}/${endpoint}*`, route => route.fulfill(failed
      ? { status: 500, json: { error: 'External service unavailable' } }
      : { json: kind === 'vault' ? [{ name: 'TestSecret', enabled: true }] : [{ key: 'Shared:ApiUrl', value: 'https://example.test', isLocked: false }] }));
    await page.goto(`/secret-providers/${id}/${kind === 'vault' ? 'explorer' : 'app-configuration'}`);
    await expect(page.locator('.q-banner.bg-red-1')).toBeVisible({ timeout: 15_000 });
    failed = false;
    await page.getByRole('button', { name: 'Retry', exact: true }).click();
    await expect(page.locator('.q-banner.bg-red-1')).toBeHidden();
    await expect(page.getByRole('row').filter({ hasText: kind === 'vault' ? 'TestSecret' : 'Shared:ApiUrl' })).toBeVisible();
  });
}
test('SQL permissions boundary displays connection warnings and recovers on reload', async ({ authenticatedPage: page }) => {
  const id = '33333333-3333-3333-3333-333333333333';
  let failed = true;
  await page.route(`**/api/SqlIntegration/${id}/permissions-overview`, route => route.fulfill(failed
    ? { status: 500, json: { error: 'Connection unavailable' } }
    : { json: { overview: { integrationId: id, integrationName: 'Test SQL', accounts: [], orphanPrincipals: [], errorMessage: 'Database access unavailable' }, isCached: false } }));
  await page.goto(`/sql-integrations/${id}/permissions`);
  await expect(page.getByText('Unable to load permissions overview.', { exact: false })).toBeVisible();
  failed = false;
  await page.reload();
  await expect(page.getByText('Database access unavailable', { exact: true })).toBeVisible();
  await expect(page.getByText('Unable to load permissions overview.', { exact: false })).toBeHidden();
});
