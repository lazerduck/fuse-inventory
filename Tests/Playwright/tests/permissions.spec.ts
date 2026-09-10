import { test, expect, signIn } from '../fixtures/auth';
import { randomUUID } from 'node:crypto';
for (const [name, permissions, canCreate, canUpdate, canDelete] of [
  ['read-only', ['tags:read'], false, false, false],
  ['creator', ['tags:read', 'tags:create'], true, false, false],
  ['editor', ['tags:read', 'tags:update'], false, true, false],
  ['deleter', ['tags:read', 'tags:delete'], false, false, true],
  ['unrelated', ['tags:read', 'application:update'], false, false, false],
] as const) {
  test(`real role: ${name}`, async ({ page, request, adminApi }) => {
    const suffix = randomUUID().slice(0, 8);
    const roleResponse = await adminApi.post('/api/role', { data: { name: `${name}-${suffix}`, description: 'UI regression role', permissions: [...permissions, 'roles:read'] } });
    expect(roleResponse.ok(), await roleResponse.text()).toBeTruthy();
    const role = await roleResponse.json();
    const user = { userName: `User-${suffix}`, password: 'DisposableTest123!' };
    const created = await adminApi.post('/api/security/accounts', { data: { ...user, isAdmin: false, roleIds: [role.id] } });
    expect(created.ok(), await created.text()).toBeTruthy();
    const tagResponse = await adminApi.post('/api/tag', { data: { name: `Role tag-${suffix}` } });
    expect(tagResponse.ok()).toBeTruthy();
    const tag = await tagResponse.json();
    const session = await signIn(page, request, user);
    await page.goto('/tags');
    const create = page.getByRole('button', { name: 'Create Tag', exact: true });
    if (canCreate) await expect(create).toBeEnabled(); else await expect(create).toBeDisabled();
    const row = page.getByRole('row').filter({ hasText: `Role tag-${suffix}` });
    if (canUpdate) await expect(row.getByRole('button').first()).toBeEnabled(); else await expect(row.getByRole('button').first()).toBeDisabled();
    if (canDelete) await expect(row.getByRole('button').last()).toBeEnabled(); else await expect(row.getByRole('button').last()).toBeDisabled();
    const headers = { Authorization: `Bearer ${session.token}` };
    expect((await request.get('/api/tag', { headers })).status()).toBe(200);
    if (!canCreate) expect((await request.post('/api/tag', { headers, data: { name: 'Forbidden' } })).status()).toBe(403);
    if (!canUpdate) expect((await request.put(`/api/tag/${tag.id}`, { headers, data: { name: 'Forbidden' } })).status()).toBe(403);
    if (!canDelete) expect((await request.delete(`/api/tag/${tag.id}`, { headers })).status()).toBe(403);
  });
}

// UI-002: the backend authorises tags:read, but UI permission resolution requires roles:read.
test('UI-002: a tag reader can view tags without administrative role-read permission', async ({ page, request, adminApi }) => {
  const suffix = randomUUID().slice(0, 8);
  const roleResponse = await adminApi.post('/api/role', { data: { name: `Minimal-${suffix}`, description: 'Minimal tag reader', permissions: ['tags:read'] } });
  expect(roleResponse.ok()).toBeTruthy();
  const role = await roleResponse.json();
  const user = { userName: `Minimal-${suffix}`, password: 'DisposableTest123!' };
  expect((await adminApi.post('/api/security/accounts', { data: { ...user, roleIds: [role.id], isAdmin: false } })).ok()).toBeTruthy();
  const session = await signIn(page, request, user);
  expect((await request.get('/api/tag', { headers: { Authorization: `Bearer ${session.token}` } })).status()).toBe(200);
  await page.goto('/tags');
  await expect(page.getByRole('heading', { name: 'Tags', exact: true })).toBeVisible();
  // Activate expected failure only after prerequisites and backend authorisation are established.
  test.fail(true, 'UI-002 in Tests/UI_TEST_FINDINGS.md');
  await expect(page.getByRole('table')).toBeVisible();
});
test('server revocation rejects an open create form and refresh updates controls', async ({ page, request, adminApi }) => {
  const suffix = randomUUID().slice(0, 8);
  const roleData = { name: `Revoked-${suffix}`, description: 'Revocation regression', permissions: ['tags:read', 'tags:create', 'roles:read'] };
  const roleResponse = await adminApi.post('/api/role', { data: roleData });
  expect(roleResponse.ok()).toBeTruthy();
  const role = await roleResponse.json();
  const user = { userName: `Revoked-${suffix}`, password: 'DisposableTest123!' };
  expect((await adminApi.post('/api/security/accounts', { data: { ...user, roleIds: [role.id], isAdmin: false } })).ok()).toBeTruthy();
  await signIn(page, request, user);
  await page.goto('/tags');
  await page.getByRole('button', { name: 'Create Tag', exact: true }).click();
  const dialog = page.getByRole('dialog');
  await dialog.getByLabel('Name', { exact: true }).fill(`Forbidden-${suffix}`);
  expect((await adminApi.put(`/api/role/${role.id}`, { data: { ...roleData, permissions: ['tags:read', 'roles:read'] } })).ok()).toBeTruthy();
  const responsePromise = page.waitForResponse(response => response.url().endsWith('/api/Tag') && response.request().method() === 'POST');
  await dialog.getByRole('button', { name: 'Create', exact: true }).click();
  expect((await responsePromise).status()).toBe(403);
  await expect(dialog).toBeVisible();
  expect((await (await adminApi.get('/api/tag')).json()).some((tag: any) => tag.name === `Forbidden-${suffix}`)).toBe(false);
  await page.reload();
  await expect(page.getByRole('button', { name: 'Create Tag', exact: true })).toBeDisabled();
});
