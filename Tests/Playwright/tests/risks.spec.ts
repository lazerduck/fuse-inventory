import { test, expect } from '../fixtures/auth';
import { randomUUID } from 'node:crypto';
for (const delayedSave of [false, true]) {
  const title = delayedSave
    ? 'risk save waits for a delayed acknowledgement and preserves mitigation'
    : 'risk creation, mitigation edit and persistence';
  test(title, async ({ authenticatedPage: page, adminApi }) => {
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
    // Wait for persistence separately from the following UI transition. A slow
    // successful write must not consume the navigation assertion's time budget.
    const [createResponse] = await Promise.all([
      page.waitForResponse(response => response.request().method() === 'POST'
        && new URL(response.url()).pathname === '/api/Risk'),
      page.getByRole('button', { name: 'Create Risk', exact: true }).click(),
    ]);
    expect(createResponse.status()).toBe(201);
    await expect(page).toHaveURL(/\/risks\/[0-9a-f-]+\/edit$/);
    const created = (await (await adminApi.get('/api/risk')).json()).find((risk: any) => risk.title === name);
    expect(created).toBeTruthy();
    await expect(page).toHaveURL(`/risks/${created.id}/edit`);
    await page.getByLabel('Mitigation Strategy', { exact: true }).fill('Add redundancy');
    if (delayedSave) {
      await page.route(`**/api/Risk/${created.id}`, async route => {
        if (route.request().method() !== 'PUT') return route.continue();
        const response = await route.fetch();
        // Exercise an acknowledgement slower than the 7-second UI assertion
        // budget, while retaining the ordinary 30-second test deadline.
        await new Promise(resolve => setTimeout(resolve, 8_000));
        await route.fulfill({ response });
      });
    }
    const [updateResponse] = await Promise.all([
      page.waitForResponse(response => response.request().method() === 'PUT'
        && new URL(response.url()).pathname === `/api/Risk/${created.id}`),
      page.getByRole('button', { name: 'Save and close', exact: true }).click(),
    ]);
    expect(updateResponse.status()).toBe(200);
    await expect(page).toHaveURL('/risks');
    await page.goto(`/risks/${created.id}/edit`);
    await expect(page.getByLabel('Mitigation Strategy', { exact: true })).toHaveValue('Add redundancy');
    const saved = await (await adminApi.get(`/api/risk/${created.id}`)).json();
    expect(saved.targetId).toBe(created.targetId);
    expect(saved.ownerPositionId).toBe(created.ownerPositionId);
  });
}
