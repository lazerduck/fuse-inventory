import { randomUUID } from 'node:crypto';
import { test, expect, signIn } from '../fixtures/auth';

test('a real read-only user can inspect graph resources and relationships without mutation actions', async ({ page, request, adminApi }) => {
  const suffix = randomUUID().slice(0, 8);
  const applicationName = `Graph reader app ${suffix}`;
  const targetName = `Graph reader target ${suffix}`;

  const environmentResponse = await adminApi.post('/api/environment', { data: { name: `Graph reader environment ${suffix}` } });
  expect(environmentResponse.ok(), await environmentResponse.text()).toBeTruthy();
  const environment = await environmentResponse.json();
  const applicationResponse = await adminApi.post('/api/application', { data: { name: applicationName } });
  expect(applicationResponse.ok(), await applicationResponse.text()).toBeTruthy();
  const application = await applicationResponse.json();
  const instanceResponse = await adminApi.post(`/api/application/${application.id}/instances`, {
    data: { environmentId: environment.id, version: 'reader-fixture' },
  });
  expect(instanceResponse.ok(), await instanceResponse.text()).toBeTruthy();
  const instance = await instanceResponse.json();
  const targetResponse = await adminApi.post('/api/externalResource', { data: { name: targetName } });
  expect(targetResponse.ok(), await targetResponse.text()).toBeTruthy();
  const target = await targetResponse.json();
  const dependencyData = { targetKind: 'External', targetId: target.id, port: 443, authKind: 'None', severity: 'Partial' };
  const dependencyPath = `/api/application/${application.id}/instances/${instance.id}/dependencies`;
  const dependencyResponse = await adminApi.post(dependencyPath, { data: dependencyData });
  expect(dependencyResponse.ok(), await dependencyResponse.text()).toBeTruthy();
  const dependency = await dependencyResponse.json();

  const roleResponse = await adminApi.post('/api/role', { data: {
    name: `Graph reader ${suffix}`,
    description: 'Read-only graph browser regression',
    permissions: [
      'application:read', 'environments:read', 'datastores:read', 'externalresources:read',
      'messagebrokers:read', 'platforms:read', 'accounts:read', 'identities:read',
      'tags:read', 'risks:read', 'secretproviders:read',
    ],
  } });
  expect(roleResponse.ok(), await roleResponse.text()).toBeTruthy();
  const role = await roleResponse.json();
  const user = { userName: `GraphReader-${suffix}`, password: 'DisposableTest123!' };
  const userResponse = await adminApi.post('/api/security/accounts', { data: { ...user, roleIds: [role.id], isAdmin: false } });
  expect(userResponse.ok(), await userResponse.text()).toBeTruthy();
  const session = await signIn(page, request, user);

  await page.goto('/applications');
  const search = page.getByRole('textbox', { name: 'Search applications' });
  await search.fill('no-such-application-' + suffix);
  await page.getByRole('button', { name: 'Clear search', exact: true }).click();
  await expect(search).toHaveValue('');
  await search.fill(applicationName);
  const row = page.getByRole('row').filter({ has: page.getByRole('link', { name: applicationName, exact: true }) });
  await expect(row.getByRole('button', { name: 'Edit application' })).toHaveCount(0);
  await row.getByRole('link', { name: applicationName }).click();
  await expect(page).toHaveURL(`/view/app/${application.id}`);

  await page.goto('/graph');
  await expect(page.getByRole('heading', { name: 'Graph', exact: true })).toBeVisible();
  await page.getByRole('combobox', { name: 'Find a resource', exact: true }).fill(applicationName);
  await page.getByRole('option').filter({ hasText: applicationName }).click();
  const inspector = page.getByRole('complementary', { name: 'Graph inspector' });
  await expect(inspector.getByRole('heading', { name: applicationName, exact: true })).toBeVisible();
  await expect(inspector.getByRole('button', { name: 'Add dependency', exact: true })).toHaveCount(0);
  await expect(inspector.getByRole('link', { name: 'Edit instance', exact: true })).toHaveCount(0);
  await expect(page).toHaveURL(/\/graph\?.*node=/);
  const graphUrl = page.url();
  await inspector.getByRole('link', { name: 'Open details', exact: true }).click();
  await expect(page).toHaveURL(`/view/instance/${instance.id}`);
  await expect(page.getByText('reader-fixture', { exact: true })).toBeVisible();

  await page.goto(graphUrl);
  await expect(inspector.getByRole('heading', { name: applicationName, exact: true })).toBeVisible();
  await inspector.getByLabel(`Inspect relationship ${targetName}`, { exact: true }).click();
  await expect(inspector.getByRole('heading', { name: 'Dependency details', exact: true })).toBeVisible();
  await expect(inspector.getByText('443', { exact: true })).toBeVisible();
  await expect(inspector.getByText('Partial', { exact: true })).toBeVisible();
  await expect(inspector.getByRole('button', { name: 'Edit relationship', exact: true })).toHaveCount(0);
  await expect(inspector.getByRole('button', { name: 'Remove relationship', exact: true })).toHaveCount(0);
  await inspector.getByRole('button', { name: `Inspect target ${targetName}`, exact: true }).click();
  await expect(inspector.getByRole('heading', { name: targetName, exact: true })).toBeVisible();
  await expect(inspector.getByRole('button', { name: 'Add dependent', exact: true })).toHaveCount(0);
  await inspector.getByRole('link', { name: 'Open details', exact: true }).click();
  await expect(page).toHaveURL(`/view/external/${target.id}`);

  // The same real session must also be denied mutations at the API boundary.
  const headers = { Authorization: `Bearer ${session.token}` };
  expect((await request.post(dependencyPath, { headers, data: dependencyData })).status()).toBe(403);
  expect((await request.put(`${dependencyPath}/${dependency.id}`, { headers, data: { ...dependencyData, port: 444 } })).status()).toBe(403);
  expect((await request.delete(`${dependencyPath}/${dependency.id}`, { headers })).status()).toBe(403);
  const savedApplication = await (await adminApi.get(`/api/application/${application.id}`)).json();
  const savedInstance = savedApplication.instances.find((item: { id: string }) => item.id === instance.id);
  expect(savedInstance.dependencies).toHaveLength(1);
  expect(savedInstance.dependencies[0]).toMatchObject({ id: dependency.id, port: 443, severity: 'Partial' });
});
