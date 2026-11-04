import { randomUUID } from 'node:crypto';
import { test, expect } from '../fixtures/auth';

test('application activity compares the selected production edit after later changes', async ({ authenticatedPage: page, adminApi }) => {
  const suffix = randomUUID().slice(0, 8);
  const app = await (await adminApi.post('/api/application', { data: { name: `Payments ${suffix}` } })).json();
  const env = await (await adminApi.post('/api/environment', { data: { name: `Production ${suffix}` } })).json();
  const instance = await (await adminApi.post(`/api/application/${app.id}/instances`, {
    data: { environmentId: env.id, version: '1.0' }
  })).json();
  for (const version of ['2.0', '3.0']) {
    const response = await adminApi.put(`/api/application/${app.id}/instances/${instance.id}`, { data: { environmentId: env.id, version } });
    expect(response.ok()).toBeTruthy();
  }
  const target = await (await adminApi.post('/api/externalResource', { data: { name: `Billing ${suffix}` } })).json();
  const dependencies = `/api/application/${app.id}/instances/${instance.id}/dependencies`;
  const dependency = await (await adminApi.post(dependencies, { data: { targetKind: 'External', targetId: target.id, port: 443, authKind: 'None' } })).json();
  expect((await adminApi.delete(`${dependencies}/${dependency.id}`)).ok()).toBeTruthy();
  const events = async () => (await (await adminApi.get(`/api/activity/Application/${app.id}`)).json()).items;
  await expect.poll(async () => (await events()).length).toBe(6);
  await page.goto('/activities');
  const rows = page.getByRole('row').filter({ hasText: app.name });
  await expect(rows).toHaveCount(6);
  // Newest first. Inspect the earlier update while the live instance is already on 3.0.
  await rows.nth(3).getByRole('button', { name: 'View changes' }).click();
  const dialog = page.getByRole('dialog');
  await expect(dialog.getByRole('heading', { name: app.name })).toBeVisible();
  await expect(dialog.getByRole('heading', { name: `Instance · ${env.name}` })).toBeVisible();
  const version = dialog.getByRole('row').filter({ hasText: 'Deployed version' });
  await expect(version.getByRole('cell')).toHaveText(['1.0', '2.0']);
  await expect(dialog).toContainText(instance.id);
  await dialog.getByRole('button', { name: 'Close changes' }).click();
  await expect(dialog).toBeHidden();
  for (const [index, action] of ['Dependency removed', 'Dependency added'].entries()) {
    await rows.nth(index).getByRole('button', { name: 'View changes' }).click();
    const relationship = dialog.getByRole('row').filter({ hasText: action });
    await expect(relationship.getByRole('cell').nth(index)).toContainText(target.name);
    await expect(relationship.getByRole('cell').nth(1 - index)).toHaveText('—');
    await dialog.getByRole('button', { name: 'Close changes' }).click();
  }
});

test('application history requires both activity and application read access', async ({ request, adminApi }) => {
  const name = `History-reader-${randomUUID().slice(0, 8)}`;
  const roleData = { name, description: 'History regression', permissions: ['activity:read'] };
  const role = await (await adminApi.post('/api/role', { data: roleData })).json();
  const credentials = { userName: name, password: 'DisposableTest123!' };
  expect((await adminApi.post('/api/security/accounts', { data: { ...credentials, roleIds: [role.id] } })).ok()).toBeTruthy();
  const session = await (await request.post('/api/security/login', { data: credentials })).json();
  const headers = { Authorization: `Bearer ${session.token}` };
  const route = `/api/activity/applications/changes/${randomUUID()}`;
  for (const permissions of [['activity:read'], ['application:read'], ['activity:read', 'application:read']]) {
    expect((await adminApi.put(`/api/role/${role.id}`, { data: { ...roleData, permissions } })).ok()).toBeTruthy();
    expect((await request.get(route, { headers })).status()).toBe(permissions.length === 2 ? 404 : 403);
  }
});
