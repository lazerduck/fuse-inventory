import { test, expect } from '../fixtures/auth';
import { randomUUID } from 'node:crypto';
// Scoped positional action locators until icon-only buttons gain names (UI-001).
const resources = [
  { route: 'data-stores', create: 'Create Data Store', api: 'dataStore', field: 'name' },
  { route: 'message-brokers', create: 'Create Broker', api: 'messageBroker', field: 'name' },
  { route: 'tags', create: 'Create Tag', api: 'tag', field: 'name' },
  { route: 'environments', create: 'Create Environment', api: 'environment', field: 'name' },
  { route: 'platforms', create: 'Create Platform', api: 'platform', field: 'displayName' },
  { route: 'external-resources', create: 'Create Resource', api: 'externalResource', field: 'name' },
  { route: 'positions', create: 'Create Position', api: 'position', field: 'name' },
  { route: 'responsibility-types', create: 'Create Responsibility Type', api: 'responsibilityType', field: 'name' },
];
for (const resource of resources) {
  test(`${resource.route}: create, cancel edit, update, reload, delete`, async ({ authenticatedPage: page, adminApi }) => {
    const name = `UI-${randomUUID().slice(0, 8)}`;
    await page.goto(`/${resource.route}`);
    await page.getByRole('button', { name: resource.create, exact: true }).click();
    const dialog = page.getByRole('dialog');
    await dialog.getByLabel(/^Name\*?$/).fill(name);
    if (['data-stores', 'message-brokers'].includes(resource.route)) {
      await dialog.getByLabel('Kind*', { exact: true }).click();
      await page.getByRole('option').first().click();
      await dialog.getByLabel('Environment*', { exact: true }).click();
      await page.getByRole('option', { name: 'UI test environment', exact: true }).click();
    }
    await dialog.getByRole('button', { name: 'Create', exact: true }).click();
    await expect(dialog).toBeHidden();
    let row = page.getByRole('row').filter({ hasText: name });
    await expect(row).toBeVisible();
    await row.getByRole('button').first().click();
    await dialog.getByLabel(/^Name\*?$/).fill(`${name}-cancelled`);
    await dialog.getByRole('button', { name: 'Cancel', exact: true }).click();
    await page.reload();
    await expect(row).toBeVisible();
    await row.getByRole('button').first().click();
    await dialog.getByLabel(/^Name\*?$/).fill(`${name}-saved`);
    await dialog.getByRole('button', { name: 'Save', exact: true }).click();
    await expect(dialog).toBeHidden();
    await page.reload();
    row = page.getByRole('row').filter({ hasText: `${name}-saved` });
    await expect(row).toBeVisible();
    const response = await adminApi.get(`/api/${resource.api}`);
    expect(response.ok(), await response.text()).toBeTruthy();
    const saved = (await response.json()).find((record: any) => record[resource.field] === `${name}-saved`);
    expect(saved).toBeTruthy();
    await row.getByRole('button').last().click();
    await dialog.getByRole('button', { name: 'OK', exact: true }).click();
    await expect(row).toBeHidden();
    await page.reload();
    await expect(row).toBeHidden();
    expect((await (await adminApi.get(`/api/${resource.api}`)).json()).some((record: any) => record.id === saved.id)).toBe(false);
  });
}
