import { test, expect, credentials } from '../fixtures/auth';
test('login errors, successful session, reload and logout', async ({ page, request }) => {
  await page.addInitScript(() => localStorage.setItem('fuse_onboarding_state', JSON.stringify({ hasCompletedTour: true, dismissedBanner: true, guideProgressVersion: 3 })));
  await page.goto('/tags');
  await page.getByTestId('auth-button').click();
  const dialog = page.getByRole('dialog');
  await dialog.getByLabel('Username', { exact: true }).fill(credentials.userName);
  await dialog.getByLabel('Password', { exact: true }).fill('WrongPassword123!');
  await dialog.getByRole('button', { name: 'Login', exact: true }).click();
  await expect(dialog.locator('.q-banner')).toBeVisible();
  await dialog.getByLabel('Password', { exact: true }).fill(credentials.password);
  await dialog.getByRole('button', { name: 'Login', exact: true }).click();
  await expect(dialog).toBeHidden();
  await expect(page.getByRole('button', { name: 'Create Tag', exact: true })).toBeEnabled();
  await page.reload();
  await expect(page.getByRole('button', { name: 'Create Tag', exact: true })).toBeEnabled();
  await page.getByTestId('auth-button').click();
  await page.getByRole('dialog').getByRole('button', { name: 'OK', exact: true }).click();
  await expect(page.getByRole('button', { name: 'Create Tag', exact: true })).toBeDisabled();
  expect((await request.get('/api/tag')).status()).toBe(401);
});
test('authenticated API fixture proves access to protected inventory', async ({ adminApi, request }) => {
  expect((await request.get('/api/account')).status()).toBe(401);
  expect((await adminApi.get('/api/account')).status()).toBe(200);
});
for (const posture of ['Unrestricted', 'RestrictedEditing', 'FullyRestricted']) {
  test(`anonymous controls and requests under ${posture}`, async ({ page, adminApi, request }) => {
    expect((await adminApi.post('/api/security/settings', { data: { posture } })).ok()).toBeTruthy();
    try {
      await page.goto('/tags');
      const create = page.getByRole('button', { name: 'Create Tag', exact: true });
      if (posture === 'Unrestricted') await expect(create).toBeEnabled(); else await expect(create).toBeDisabled();
      expect((await request.get('/api/tag')).status()).toBe(posture === 'FullyRestricted' ? 401 : 200);
      if (posture !== 'Unrestricted') expect((await request.post('/api/tag', { data: { name: 'Forbidden' } })).status()).toBe(401);
    } finally {
      expect((await adminApi.post('/api/security/settings', { data: { posture: 'FullyRestricted' } })).ok()).toBeTruthy();
    }
  });
}
