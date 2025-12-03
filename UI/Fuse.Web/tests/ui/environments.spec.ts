import { test, expect } from '@playwright/test';
import { 
  navigateTo, 
  NavDestinations, 
  waitForPageLoad,
  clickCreateButton,
  fillInput,
  fillTextarea,
  clickSave,
  clickEditInRow,
  clickDeleteInRow,
  confirmDelete,
  expectSuccessNotification,
  waitForTableLoad,
  expectRowInTable,
  expectNoRowInTable,
  generateTestName,
  dismissInitialDialogs
} from './helpers';

test.describe('Environments', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('/');
    await waitForPageLoad(page);
    await dismissInitialDialogs(page);
    await navigateTo(page, NavDestinations.environments);
    await waitForTableLoad(page);
  });

  test('can view environments page', async ({ page }) => {
    await expect(page.locator('h1:has-text("Environments")')).toBeVisible();
    await expect(page.locator('[data-tour-id="environments-table"]')).toBeVisible();
  });

  test('can create an environment', async ({ page }) => {
    const envName = generateTestName('test-env');

    // Click create button
    await clickCreateButton(page, 'Create Environment');
    
    // Wait for dialog to open
    await expect(page.locator('.q-dialog')).toBeVisible();

    // Fill in the form
    await fillInput(page, 'Name', envName);
    await fillTextarea(page, 'Description', 'Test environment description');

    // Submit the form
    await clickSave(page);

    // Verify success and wait for dialog to close
    await expectSuccessNotification(page, 'created');
    await expect(page.locator('.q-dialog .q-card')).not.toBeVisible({ timeout: 5000 });
    
    // Wait a bit for query to refetch
    await page.waitForTimeout(500);
    await waitForTableLoad(page);
    await expectRowInTable(page, envName);
  });

  test('can edit an environment', async ({ page }) => {
    const envName = generateTestName('edit-env');
    const updatedDescription = 'Updated description';

    // First create an environment
    await clickCreateButton(page, 'Create Environment');
    await expect(page.locator('.q-dialog')).toBeVisible();
    await fillInput(page, 'Name', envName);
    await clickSave(page);
    await expectSuccessNotification(page, 'created');
    await expect(page.locator('.q-dialog .q-card')).not.toBeVisible({ timeout: 5000 });
    await page.waitForTimeout(500);
    await waitForTableLoad(page);
    
    // Wait for the row to appear
    await expectRowInTable(page, envName);

    // Now edit it
    await clickEditInRow(page, envName);
    await expect(page.locator('.q-dialog')).toBeVisible();

    // Update the description
    await fillTextarea(page, 'Description', updatedDescription);
    await clickSave(page);

    // Verify success
    await expectSuccessNotification(page, 'updated');
    await expect(page.locator('.q-dialog .q-card')).not.toBeVisible({ timeout: 5000 });
    await page.waitForTimeout(500);
    await waitForTableLoad(page);
    
    // Verify the environment still exists
    await expectRowInTable(page, envName);
  });

  test('can delete an environment', async ({ page }) => {
    const envName = generateTestName('delete-env');

    // First create an environment
    await clickCreateButton(page, 'Create Environment');
    await expect(page.locator('.q-dialog')).toBeVisible();
    await fillInput(page, 'Name', envName);
    await clickSave(page);
    await expectSuccessNotification(page, 'created');
    await expect(page.locator('.q-dialog .q-card')).not.toBeVisible({ timeout: 5000 });
    await page.waitForTimeout(500);
    await waitForTableLoad(page);
    
    // Wait for the row to appear
    await expectRowInTable(page, envName);

    // Now delete it
    await clickDeleteInRow(page, envName);
    await expect(page.locator('.q-dialog')).toBeVisible();
    await confirmDelete(page);

    // Verify success
    await expectSuccessNotification(page, 'deleted');
    await page.waitForTimeout(500);
    await waitForTableLoad(page);
    
    // Verify it's gone
    await expectNoRowInTable(page, envName);
  });
});
