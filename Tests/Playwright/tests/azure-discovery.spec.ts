import { test, expect } from '../fixtures/auth';

test('Azure integrations separate services, search endpoints and verify access', async ({ authenticatedPage: page }, testInfo) => {
  const providers = [
    { id: '44444444-4444-4444-4444-444444444444', name: 'Production vault', vaultUri: 'https://production.vault.azure.net/', authMode: 'ManagedIdentity', integrationKind: 'KeyVault', credentialSource: 'ManagedIdentity', capabilities: 'Check, Read' },
    { id: '55555555-5555-5555-5555-555555555555', name: 'Production configuration', vaultUri: 'https://production.azconfig.io/', authMode: 'ClientSecret', integrationKind: 'AppConfiguration', credentialSource: 'SharedCredentials', capabilities: 'Check, Create, Rotate' },
  ];
  await page.route('**/api/SecretProvider', route => route.fulfill({ json: providers }));
  await page.route('**/api/SecretProvider/azure-manager', route => route.fulfill({ json: { hasClientSecretCredentials: true, tenantId: 'test-tenant', clientId: 'test-client' } }));
  await page.route(`**/api/SecretProvider/${providers[0]!.id}/test-connection`, async route => {
    Object.assign(providers[0]!, { lastCheckedAt: '2026-11-01T19:00:00Z', lastCheckSucceeded: true, lastCheckMessage: 'Secret metadata can be browsed. Write operations were not tested.' });
    await route.fulfill({ json: { success: true, checkedAt: '2026-11-01T19:00:00Z', message: 'Secret metadata can be browsed. Write operations were not tested.' } });
  });
  await page.goto('/secret-providers');
  await expect(page.getByRole('tab', { name: 'Key Vaults (1)', exact: true })).toBeVisible();
  await expect(page.getByRole('cell', { name: 'Production vault', exact: false })).toBeVisible();
  await expect(page.getByRole('cell', { name: 'Production configuration', exact: false })).toBeHidden();
  await page.getByRole('button', { name: 'Test integration access', exact: true }).click();
  await expect(page.getByText('Browse access verified', { exact: true })).toBeVisible();
  await page.getByRole('tab', { name: 'App Configuration (1)', exact: true }).click();
  await expect(page.getByRole('button', { name: 'Add App Configuration', exact: true })).toBeVisible();
  await expect(page.getByRole('cell', { name: 'Shared credentials', exact: true })).toBeVisible();
  await expect(page.getByText('Update', { exact: true })).toBeVisible();
  await expect(page.getByText('Rotate', { exact: true })).toBeHidden();
  await page.getByLabel('Search Azure integrations', { exact: true }).fill('missing-endpoint');
  await expect(page.getByText('No integrations match your search.', { exact: true })).toBeVisible();
  await page.getByLabel('Search Azure integrations', { exact: true }).fill('production.azconfig.io');
  await expect(page.getByRole('cell', { name: 'Production configuration', exact: false })).toBeVisible();
  await page.screenshot({ path: testInfo.outputPath('azure-integrations.png'), fullPage: true });
  await page.getByRole('button', { name: 'Add App Configuration', exact: true }).click();
  await expect(page.getByRole('dialog').getByLabel('App Configuration endpoint*', { exact: true })).toBeVisible();
  await expect(page.getByRole('dialog').getByText('Create configuration entries', { exact: true })).toBeVisible();
});

test('Azure discovery remembers exclusions, avoids existing resources and retries failed imports', async ({ authenticatedPage: page }, testInfo) => {
  const subscription = '66666666-6666-6666-6666-666666666666';
  const resource = (name: string, kind = 'KeyVault', extra = {}) => ({
    resourceId: `/subscriptions/${subscription}/resourceGroups/production/providers/${kind === 'KeyVault' ? 'Microsoft.KeyVault/vaults' : 'Microsoft.AppConfiguration/configurationStores'}/${name}`,
    name, kind, endpoint: `https://${name}.${kind === 'KeyVault' ? 'vault.azure.net' : 'azconfig.io'}/`,
    subscriptionId: subscription, resourceGroup: 'production', location: 'uksouth', alreadyAdded: false, ignored: false, ...extra
  });
  const resources = [resource('new-vault'), resource('new-config', 'AppConfiguration'), resource('existing', 'KeyVault', { alreadyAdded: true }), resource('ignored-vault', 'KeyVault', { ignored: true })];
  const excluded: string[] = [];
  const imports: string[][] = [];
  await page.route('**/api/SecretProvider', route => route.fulfill({ json: [] }));
  await page.route('**/api/SecretProvider/azure-manager', route => route.fulfill({ json: { hasClientSecretCredentials: true } }));
  await page.route('**/api/azure-discovery/subscriptions', route => route.fulfill({ json: { subscriptions: [{ id: subscription, name: 'Production subscription' }], warnings: [] } }));
  await page.route('**/api/azure-discovery/resources', route => route.fulfill({ json: { resources, warnings: [] } }));
  await page.route('**/api/azure-discovery/exclusions', route => {
    excluded.push(...route.request().postDataJSON().resourceIds);
    return route.fulfill({ json: { resourceIds: excluded } });
  });
  await page.route('**/api/azure-discovery/import', route => {
    const ids = route.request().postDataJSON().resourceIds as string[];
    imports.push(ids);
    return route.fulfill({ json: { results: ids.map(resourceId => ({ resourceId, name: 'new-vault', status: imports.length === 1 ? 'Failed' : 'Added', error: imports.length === 1 ? 'Azure temporarily unavailable' : undefined })), warnings: [] } });
  });
  await page.goto('/secret-providers');
  await page.getByRole('button', { name: 'Discover Azure resources', exact: true }).click();
  const dialog = page.getByRole('dialog');
  await dialog.getByRole('button', { name: 'Discover resources', exact: true }).click();
  await expect(dialog.getByRole('checkbox', { name: 'Include existing', exact: true })).toBeDisabled();
  await expect(dialog.getByRole('checkbox', { name: 'Include ignored-vault', exact: true })).toBeHidden();
  await expect(dialog.getByRole('button', { name: 'Add selected (2)', exact: true })).toBeVisible();
  await dialog.getByRole('checkbox', { name: 'Include new-config', exact: true }).click();
  await expect(dialog.getByRole('button', { name: 'Add selected (1)', exact: true })).toBeInViewport();
  await expect(dialog.getByRole('checkbox', { name: 'Remember unselected resources as exclusions', exact: true })).toBeInViewport();
  await page.screenshot({ path: testInfo.outputPath('azure-discovery.png'), fullPage: true });
  await dialog.getByRole('button', { name: 'Add selected (1)', exact: true }).click();
  await expect(dialog.getByText('new-vault: Azure temporarily unavailable', { exact: true })).toBeVisible();
  expect(excluded).toEqual([resources[1]!.resourceId]);
  expect(imports).toEqual([[resources[0]!.resourceId]]);
  await dialog.getByRole('button', { name: 'Add selected (1)', exact: true }).click();
  await expect(dialog.getByText('1 added · 0 already added · 0 failed', { exact: false })).toBeVisible();
  expect(imports).toHaveLength(2);
  await dialog.getByRole('button', { name: 'Done', exact: true }).click();
  await expect(dialog).toBeHidden();
});
