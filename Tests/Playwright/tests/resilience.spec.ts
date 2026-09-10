import { test, expect } from '../fixtures/auth';
import { randomUUID } from 'node:crypto';
test('failed tag save retains input and recovers without a phantom record', async ({ authenticatedPage: page, adminApi }) => {
  const name = `Recovery-${randomUUID()}`;
  await page.goto('/tags');
  await page.getByRole('button', { name: 'Create Tag', exact: true }).click();
  const dialog = page.getByRole('dialog');
  await dialog.getByLabel('Name', { exact: true }).fill(name);
  await page.route('**/api/Tag', async route => {
    if (route.request().method() === 'POST') await route.fulfill({ status: 500, json: { message: 'Test save failure' } });
    else await route.continue();
  });
  await dialog.getByRole('button', { name: 'Create', exact: true }).click();
  await expect(page.locator('.q-notification')).toBeVisible();
  await expect(dialog.getByLabel('Name', { exact: true })).toHaveValue(name);
  expect((await (await adminApi.get('/api/tag')).json()).some((tag: any) => tag.name === name)).toBe(false);
  await page.unroute('**/api/Tag');
  await dialog.getByRole('button', { name: 'Create', exact: true }).click();
  await expect(dialog).toBeHidden();
  await expect(page.getByRole('row').filter({ hasText: name })).toBeVisible();
});
test('tag search persists across reload and can recover from no matches', async ({ authenticatedPage: page, adminApi }) => {
  const name = `Search-${randomUUID()}`;
  expect((await adminApi.post('/api/tag', { data: { name } })).ok()).toBeTruthy();
  await page.goto('/tags');
  await page.getByPlaceholder('Search...').fill(name);
  await expect(page.locator('tbody tr')).toHaveCount(1);
  await expect(page.getByRole('row').filter({ hasText: name })).toBeVisible();
  await page.reload();
  await expect(page.getByPlaceholder('Search...')).toHaveValue(name);
  await page.getByPlaceholder('Search...').fill('no-such-regression-record');
  await expect(page.getByText('No tags defined yet.')).toBeVisible();
  await page.getByPlaceholder('Search...').fill(name);
  await expect(page.getByRole('row').filter({ hasText: name })).toBeVisible();
});
test('blank and duplicate tag names show validation errors without writes', async ({ authenticatedPage: page, adminApi }) => {
  const name = `Duplicate-${randomUUID()}`;
  expect((await adminApi.post('/api/tag', { data: { name } })).ok()).toBeTruthy();
  await page.goto('/tags');
  await page.getByRole('button', { name: 'Create Tag', exact: true }).click();
  const dialog = page.getByRole('dialog');
  await dialog.getByRole('button', { name: 'Create', exact: true }).click();
  await expect(page.locator('.q-notification')).toBeVisible();
  await expect(dialog).toBeVisible();
  await dialog.getByLabel('Name', { exact: true }).fill(name);
  const rejected = page.waitForResponse(response => response.url().endsWith('/api/Tag') && response.request().method() === 'POST');
  await dialog.getByRole('button', { name: 'Create', exact: true }).click();
  expect((await rejected).status()).toBe(409);
  await expect(dialog.getByLabel('Name', { exact: true })).toHaveValue(name);
  expect((await (await adminApi.get('/api/tag')).json()).filter((tag: any) => tag.name === name)).toHaveLength(1);
});
test('pending save is visibly busy and saves once when the response arrives', async ({ authenticatedPage: page, adminApi }) => {
  let release!: () => void;
  const gate = new Promise<void>(resolve => { release = resolve; });
  let posts = 0;
  await page.route('**/api/Tag', async route => {
    if (route.request().method() === 'POST') { posts++; await gate; }
    await route.continue();
  });
  const name = `Pending-${randomUUID()}`;
  try {
    await page.goto('/tags');
    await page.getByRole('button', { name: 'Create Tag', exact: true }).click();
    const dialog = page.getByRole('dialog');
    await dialog.getByLabel('Name', { exact: true }).fill(name);
    await dialog.getByRole('button', { name: 'Create', exact: true }).click();
    await expect.poll(() => posts).toBe(1);
    await expect(dialog.locator('button[type=submit] .q-spinner')).toBeVisible();
    await dialog.locator('button[type=submit]').click();
    expect(posts).toBe(1);
    release();
    await expect(dialog).toBeHidden();
    expect((await (await adminApi.get('/api/tag')).json()).filter((tag: any) => tag.name === name)).toHaveLength(1);
    expect(posts).toBe(1);
  } finally { release(); }
});
test('tag pagination changes page and preserves the selected page on reload', async ({ authenticatedPage: page, adminApi }) => {
  const prefix = `Paged-${randomUUID().slice(0, 8)}`;
  for (let index = 0; index < 12; index++) {
    expect((await adminApi.post('/api/tag', { data: { name: `${prefix}-${String(index).padStart(2, '0')}` } })).ok()).toBeTruthy();
  }
  await page.goto('/tags');
  await page.getByPlaceholder('Search...').fill(prefix);
  await expect(page.getByText('1–10 of 12', { exact: true })).toBeVisible();
  await page.getByRole('button', { name: 'Next page', exact: true }).click();
  await expect(page.locator('tbody tr')).toHaveCount(2);
  await expect(page.getByText('11–12 of 12', { exact: true })).toBeVisible();
  await page.getByRole('button', { name: 'Previous page', exact: true }).click();
  await expect(page.locator('tbody tr')).toHaveCount(10);
  await page.getByRole('button', { name: 'Next page', exact: true }).click();
  await expect(page.getByText('11–12 of 12', { exact: true })).toBeVisible();
  await page.reload();
  test.fail(true, 'UI-003 in Tests/UI_TEST_FINDINGS.md: Tags page does not persist pagination changes');
  await expect(page.getByText('11–12 of 12', { exact: true })).toBeVisible();
});
