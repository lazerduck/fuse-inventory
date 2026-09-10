import { test, expect } from '../fixtures/auth';
import { randomUUID } from 'node:crypto';
test('risk creation, mitigation edit and persistence', async ({ authenticatedPage: page, adminApi }) => {
  const name = `Risk-${randomUUID().slice(0, 8)}`;
  expect((await adminApi.post('/api/application', { data: { name: `${name}-app` } })).ok()).toBeTruthy();
  expect((await adminApi.post('/api/position', { data: { name: `${name}-owner` } })).ok()).toBeTruthy();
  await page.goto('/risks/create');
  await page.getByLabel('Title *', { exact: true }).fill(name);
  await page.getByLabel('Target Type *', { exact: true }).click();
  await page.getByRole('option', { name: 'Application', exact: true }).click();
  await page.getByLabel('Target *', { exact: true }).click();
  await page.getByRole('option', { name: `${name}-app`, exact: true }).click();
  await expect(page.getByRole('option')).toHaveCount(0);
  for (const label of ['Impact *', 'Likelihood *']) {
    await page.getByLabel(label, { exact: true }).click();
    await page.getByRole('option', { name: 'Low', exact: true }).click();
    await expect(page.getByLabel(label, { exact: true })).toHaveValue('Low');
    await expect(page.getByRole('option')).toHaveCount(0);
  }
  await page.getByLabel('Owner Position *', { exact: true }).click();
  await page.getByRole('option', { name: `${name}-owner`, exact: true }).click();
  await page.getByRole('button', { name: 'Create Risk', exact: true }).click();
  await expect(page).toHaveURL('/risks');
  const created = (await (await adminApi.get('/api/risk')).json()).find((risk: any) => risk.title === name);
  expect(created).toBeTruthy();
  await page.goto(`/risks/${created.id}/edit`);
  await page.getByLabel('Mitigation Strategy', { exact: true }).fill('Add redundancy');
  await page.getByRole('button', { name: 'Save Changes', exact: true }).click();
  await expect(page).toHaveURL('/risks');
  await page.goto(`/risks/${created.id}/edit`);
  await expect(page.getByLabel('Mitigation Strategy', { exact: true })).toHaveValue('Add redundancy');
  const saved = await (await adminApi.get(`/api/risk/${created.id}`)).json();
  expect(saved.targetId).toBe(created.targetId);
  expect(saved.ownerPositionId).toBe(created.ownerPositionId);
});
