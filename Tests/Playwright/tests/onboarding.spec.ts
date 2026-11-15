import { test, expect } from '../fixtures/auth';
import { randomUUID } from 'node:crypto';

test('guide follows the created application and remembers progress after reload', async ({ authenticatedPage: page, adminApi }) => {
  const original = await (await adminApi.get('/api/onboarding/progress')).json();
  let applicationId: string | undefined;
  const name = `Onboarding ${randomUUID().slice(0, 8)}`;
  try {
    expect((await adminApi.put('/api/onboarding/progress', { data: {
      completedStepIds: ['first-app-environment'], activeGuideId: 'first-application',
      hasCompletedGettingStarted: false, lastCompletedAt: null, applicationId: null,
    } })).ok()).toBeTruthy();
    await page.goto('/applications');
    await page.getByRole('button', { name: 'Help', exact: true }).click();
    await page.getByText('Guides', { exact: true }).click();
    const guide = page.locator('.guide-panel');
    await guide.getByText('Document my first application', { exact: true }).click();
    await expect(guide.getByText('1 of 4 steps complete', { exact: true })).toBeVisible();
    await guide.getByRole('button', { name: 'Create application', exact: true }).click();
    const dialog = page.getByRole('dialog');
    await dialog.getByLabel('Name', { exact: true }).fill(name);
    await dialog.getByRole('button', { name: 'Create', exact: true }).click();
    await expect(dialog).toBeHidden();
    await expect(guide.getByText('2 of 4 steps complete', { exact: true })).toBeVisible();
    const applications = await (await adminApi.get('/api/application')).json();
    applicationId = applications.find((app: { name: string }) => app.name === name).id;
    await guide.getByRole('button', { name: 'Open Instances tab', exact: true }).click();
    await expect(page).toHaveURL(new RegExp(`/applications/${applicationId}\\?tab=instances$`));
    await page.getByRole('button', { name: 'Add Instance', exact: true }).click();
    await dialog.getByRole('combobox', { name: 'Environment*', exact: true }).click();
    await page.getByRole('option', { name: 'UI test environment', exact: true }).click();
    await dialog.getByRole('button', { name: 'Create', exact: true }).click();
    await expect(dialog).toBeHidden();
    await expect(guide.getByText('3 of 4 steps complete', { exact: true })).toBeVisible();
    await expect.poll(async () => (await (await adminApi.get('/api/onboarding/progress')).json()).completedStepIds)
      .toContain('first-app-instance');
    await page.reload();
    await page.getByRole('button', { name: 'Help', exact: true }).click();
    await page.getByText('Guides', { exact: true }).click();
    await guide.getByText('Document my first application', { exact: true }).click();
    await expect(guide.getByText('3 of 4 steps complete', { exact: true })).toBeVisible();
    await expect(guide.getByRole('combobox', { name: 'Application for this guide' })).toHaveValue(name);
    await guide.getByRole('button', { name: 'View documentation', exact: true }).click();
    await expect(page).toHaveURL(`/view/app/${applicationId}`);
    await guide.getByRole('button', { name: 'Explore graph', exact: true }).click();
    await expect(page).toHaveURL('/graph');
    await guide.getByRole('checkbox', { name: 'Mark Explore your documentation and graph complete' }).check();
    await expect(guide.getByText('4 of 4 steps complete', { exact: true })).toBeVisible();
    await expect.poll(async () => (await (await adminApi.get('/api/onboarding/progress')).json()).hasCompletedGettingStarted).toBe(true);
    await guide.getByRole('button', { name: 'All guides', exact: true }).click();
    await expect(guide.getByText('Connect real dependencies', { exact: true })).toBeVisible();
  } finally {
    if (applicationId) await adminApi.delete(`/api/application/${applicationId}`);
    await adminApi.put('/api/onboarding/progress', { data: original });
  }
});
