import { randomUUID } from 'node:crypto';
import { test, expect, signIn } from '../fixtures/auth';
import type { APIRequestContext } from '@playwright/test';

async function create(api: APIRequestContext, path: string, data: object) {
  const response = await api.post(`/api/${path}`, { data });
  expect(response.ok(), await response.text()).toBeTruthy();
  return response.json();
}

test('documentation catalogue supports browsing, metadata search and returning through related records', async ({ authenticatedPage: page, adminApi }, testInfo) => {
  const suffix = randomUUID().slice(0, 8);
  const tag = await create(adminApi, 'tag', { name: `DocsTag-${suffix}` });
  const environment = await create(adminApi, 'environment', { name: `Docs Prod-${suffix}` });
  const app = await create(adminApi, 'application', { name: `Docs service-${suffix}`, owner: `Docsowner ${suffix}`, description: 'Processes customer orders and publishes events.', tagIds: [tag.id] });
  const instance = await create(adminApi, `application/${app.id}/instances`, { environmentId: environment.id, version: 'docs-v1' });
  const broker = await create(adminApi, 'messageBroker', { name: `Docs events-${suffix}`, kind: 'RabbitMQ', environmentId: environment.id, queues: [{ name: 'orders', description: 'Order events' }] });
  const dependency = await create(adminApi, `application/${app.id}/instances/${instance.id}/dependencies`, { targetKind: 'MessageBroker', targetId: broker.id, port: 5672, authKind: 'None', severity: 'Partial' });
  const errors: string[] = [];
  page.on('pageerror', error => errors.push(error.message));
  await page.goto('/view');
  await expect(page.getByRole('heading', { name: 'Documentation', exact: true })).toBeVisible();
  await expect(page.getByRole('button', { name: 'Browse Applications', exact: true })).toBeEnabled();
  await page.screenshot({ path: testInfo.outputPath('documentation-catalogue.png'), fullPage: true, animations: 'disabled' });
  await page.getByRole('button', { name: 'Browse Applications', exact: true }).click();
  await expect(page.locator('.search-results').getByRole('link').filter({ hasText: app.name })).toBeVisible();
  const search = page.getByRole('textbox', { name: 'Search documentation', exact: true });
  // Character-by-character input catches URL synchronization eating spaces.
  await search.pressSequentially(`Docsowner ${suffix}`, { delay: 20 });
  await expect(search).toHaveValue(`Docsowner ${suffix}`);
  await expect(page.locator('.search-results').getByRole('link')).toHaveCount(1);
  await expect(page).toHaveURL(new RegExp(`q=Docsowner(?:%20|\\+)${suffix}&type=app`));
  const catalogueUrl = page.url();
  await page.locator('.search-results').getByRole('link').click();
  await expect(page.getByRole('heading', { name: `Application: ${app.name}`, exact: true })).toBeVisible();
  await page.getByRole('complementary', { name: 'Related resources', exact: true }).locator(`a[href^="/view/instance/${instance.id}"]`).click();
  await expect(page.getByText('docs-v1', { exact: true })).toBeVisible();
  await expect(page.getByRole('link', { name: 'View in graph', exact: true })).toHaveAttribute('href', `/graph?node=appi-${instance.id}`);
  await page.getByRole('complementary', { name: 'Related resources', exact: true }).locator(`a[href^="/view/dependency/${dependency.id}"]`).click();
  await expect(page.getByRole('main', { name: 'Resource details' })).toContainText(broker.name);
  // A link inside the detail body also retains the catalogue origin.
  await page.getByRole('main', { name: 'Resource details' }).locator(`a[href^="/view/message-broker/${broker.id}"]`).click();
  await expect(page.getByRole('heading', { name: `Message Broker: ${broker.name}`, exact: true })).toBeVisible();
  await page.getByRole('link', { name: 'Back to catalogue', exact: true }).click();
  await expect(page).toHaveURL(catalogueUrl);
  await expect(search).toHaveValue(`Docsowner ${suffix}`);
  await expect(page.locator('.search-results').getByRole('link')).toHaveCount(1);
  await search.fill(tag.name);
  await expect(page.locator('.search-results').getByRole('link').filter({ hasText: app.name })).toBeVisible();
  await search.fill('DefinitelyMissingRecord');
  await expect(page.getByRole('heading', { name: 'No matching records', exact: true })).toBeVisible();
  await page.getByRole('button', { name: 'Back to categories', exact: true }).click();
  await expect(page.getByRole('heading', { name: 'Browse by category', exact: true })).toBeVisible();
  await page.getByRole('button', { name: 'Switch to dark theme', exact: true }).click();
  await page.screenshot({ path: testInfo.outputPath('documentation-dark.png'), fullPage: true, animations: 'disabled' });
  await page.setViewportSize({ width: 600, height: 900 });
  await page.getByRole('button', { name: 'Browse Applications', exact: true }).click();
  await search.fill(app.name);
  await page.locator('.search-results').getByRole('link').first().click();
  await page.screenshot({ path: testInfo.outputPath('documentation-detail-narrow.png'), fullPage: true, animations: 'disabled' });
  const detailTop = await page.getByRole('main', { name: 'Resource details' }).evaluate(element => element.getBoundingClientRect().top);
  const contextTop = await page.getByRole('complementary', { name: 'Resource context' }).evaluate(element => element.getBoundingClientRect().top);
  expect(detailTop).toBeLessThan(contextTop);
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBe(true);
  expect(errors).toEqual([]);
});

test('documentation distinguishes unavailable categories from empty results and retries failed loads', async ({ authenticatedPage: page, adminApi }) => {
  const suffix = randomUUID().slice(0, 8);
  const app = await create(adminApi, 'application', { name: `Docs retry-${suffix}` });
  let failed = true;
  await page.route(/\/api\/application(?:\?|$)/i, route => failed ? route.fulfill({ status: 503, json: { error: 'Temporarily unavailable' } }) : route.continue());
  await page.goto('/view?type=app');
  await expect(page.getByRole('alert')).toContainText('Some documentation could not be loaded.', { timeout: 15_000 });
  await expect(page.getByRole('heading', { name: 'Documentation is partly unavailable', exact: true })).toBeVisible();
  failed = false;
  await page.getByRole('button', { name: 'Retry loading', exact: true }).click();
  await expect(page.locator('.search-results').getByRole('link').filter({ hasText: app.name })).toBeVisible();
  await expect(page.getByRole('alert')).toHaveCount(0);
});

test('documentation only requests and displays categories a real restricted reader can access', async ({ page, request, adminApi }) => {
  const suffix = randomUUID().slice(0, 8);
  const tag = await create(adminApi, 'tag', { name: `Docs permitted-${suffix}` });
  const role = await create(adminApi, 'role', { name: `Docs tag reader-${suffix}`, description: 'Documentation reader regression', permissions: ['tags:read'] });
  const user = { userName: `DocsReader-${suffix}`, password: 'DisposableTest123!' };
  await create(adminApi, 'security/accounts', { ...user, roleIds: [role.id], isAdmin: false });
  await signIn(page, request, user);
  const forbiddenRequests: string[] = [];
  page.on('request', req => { if (/\/api\/(application|account|identity|dataStore|messageBroker|Risk)(?:\?|$)/i.test(req.url())) forbiddenRequests.push(req.url()); });
  await page.goto('/view');
  await expect(page.getByRole('button', { name: 'Browse Applications', exact: true })).toBeDisabled();
  await expect(page.getByRole('button', { name: 'Browse Tags', exact: true })).toBeEnabled();
  await page.getByRole('button', { name: 'Browse Tags', exact: true }).click();
  await expect(page.locator('.search-results').getByRole('link').filter({ hasText: tag.name })).toBeVisible();
  expect(forbiddenRequests).toEqual([]);
  await page.goto('/view?type=account');
  await expect(page.getByText('Read access is required to browse accounts.', { exact: true })).toBeVisible();
  await expect(page.getByRole('heading', { name: 'No records in this category yet', exact: true })).toHaveCount(0);
});

test('documentation restores pagination and resets it when the search changes', async ({ authenticatedPage: page, adminApi }) => {
  const prefix = `DocsPage-${randomUUID().slice(0, 8)}`;
  for (let index = 0; index < 25; index++) await create(adminApi, 'tag', { name: `${prefix}-${String(index).padStart(2, '0')}` });
  const catalogue = `/view?q=${prefix}&type=tag&page=2`;
  await page.goto(catalogue);
  await expect(page.locator('.search-results').getByRole('link')).toHaveCount(1);
  await expect(page.locator('.search-results')).toContainText(`${prefix}-24`);
  await page.locator('.search-results').getByRole('link').click();
  await page.getByRole('link', { name: 'Back to catalogue', exact: true }).click();
  await expect(page).toHaveURL(catalogue);
  await expect(page.locator('.search-results').getByRole('link')).toHaveCount(1);
  await page.getByRole('textbox', { name: 'Search documentation', exact: true }).fill(`${prefix}-0`);
  await expect(page.locator('.search-results').getByRole('link')).toHaveCount(10);
  await expect(page).not.toHaveURL(/page=/);
  await page.goto(`/view?q=${prefix}&type=tag&page=999`);
  await expect(page).toHaveURL(catalogue);
  await expect(page.locator('.search-results').getByRole('link')).toHaveCount(1);
});
