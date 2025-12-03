import { test, expect } from '@playwright/test';
import { 
  navigateTo, 
  NavDestinations, 
  waitForPageLoad,
  clickCreateButton,
  fillInput,
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

test.describe('Platforms', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('/');
    await waitForPageLoad(page);
    await dismissInitialDialogs(page);
    await navigateTo(page, NavDestinations.platforms);
    await waitForTableLoad(page);
  });

  test('can view platforms page', async ({ page }) => {
    await expect(page.locator('h1:has-text("Platforms")')).toBeVisible();
  });

  test('can create a platform', async ({ page }) => {
    const platformName = generateTestName('test-platform');

    // Click create button
    await clickCreateButton(page, 'Create Platform');
    
    // Wait for dialog to open
    await expect(page.locator('.q-dialog')).toBeVisible();

    // Fill in the form - Platform uses "Name*" label
    await fillInput(page, 'Name', platformName);
    await fillInput(page, 'DNS Name', 'test.example.com');
    await fillInput(page, 'IP Address', '192.168.1.1');

    // Submit the form
    await clickSave(page);

    // Verify success
    await expectSuccessNotification(page, 'created');
    await waitForTableLoad(page);
    await expectRowInTable(page, platformName);
  });

  test('can edit a platform', async ({ page }) => {
    const platformName = generateTestName('edit-platform');

    // First create a platform
    await clickCreateButton(page, 'Create Platform');
    await expect(page.locator('.q-dialog')).toBeVisible();
    await fillInput(page, 'Name', platformName);
    await clickSave(page);
    await expectSuccessNotification(page, 'created');
    await waitForTableLoad(page);

    // Now edit it
    await clickEditInRow(page, platformName);
    await expect(page.locator('.q-dialog')).toBeVisible();

    // Update the IP address
    await fillInput(page, 'IP Address', '10.0.0.1');
    await clickSave(page);

    // Verify success
    await expectSuccessNotification(page, 'updated');
    await waitForTableLoad(page);
    
    // Verify the platform still exists
    await expectRowInTable(page, platformName);
  });

  test('can delete a platform', async ({ page }) => {
    const platformName = generateTestName('delete-platform');

    // First create a platform
    await clickCreateButton(page, 'Create Platform');
    await expect(page.locator('.q-dialog')).toBeVisible();
    await fillInput(page, 'Name', platformName);
    await clickSave(page);
    await expectSuccessNotification(page, 'created');
    await waitForTableLoad(page);

    // Now delete it
    await clickDeleteInRow(page, platformName);
    await expect(page.locator('.q-dialog')).toBeVisible();
    await confirmDelete(page);

    // Verify success
    await expectSuccessNotification(page, 'deleted');
    await waitForTableLoad(page);
    
    // Verify it's gone
    await expectNoRowInTable(page, platformName);
  });
});
