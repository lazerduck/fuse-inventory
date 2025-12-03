import { test, expect } from '@playwright/test';
import { 
  navigateTo, 
  NavDestinations, 
  waitForPageLoad,
  clickCreateButton,
  fillInput,
  fillTextarea,
  clickSave,
  clickDeleteInRow,
  confirmDelete,
  expectSuccessNotification,
  waitForTableLoad,
  expectRowInTable,
  expectNoRowInTable,
  generateTestName
} from './helpers';

test.describe('Applications', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('/');
    await waitForPageLoad(page);
    await navigateTo(page, NavDestinations.applications);
    await waitForTableLoad(page);
  });

  test('can view applications page', async ({ page }) => {
    await expect(page.locator('h1:has-text("Applications")')).toBeVisible();
    await expect(page.locator('[data-tour-id="applications-table"]')).toBeVisible();
  });

  test('can create an application', async ({ page }) => {
    const appName = generateTestName('test-app');

    // Click create button
    await clickCreateButton(page, 'Create Application');
    
    // Wait for dialog to open
    await expect(page.locator('.q-dialog')).toBeVisible();

    // Fill in the form
    await fillInput(page, 'Name', appName);
    await fillInput(page, 'Version', '1.0.0');
    await fillInput(page, 'Owner', 'Test Owner');
    await fillInput(page, 'Framework', 'Vue.js');
    await fillTextarea(page, 'Description', 'Test application description');

    // Submit the form
    await clickSave(page);

    // Verify success - application creation redirects to edit page
    await expectSuccessNotification(page, 'created');
    
    // Navigate back to applications list
    await navigateTo(page, NavDestinations.applications);
    await waitForTableLoad(page);
    
    // Verify application exists in list
    await expectRowInTable(page, appName);
  });

  test('can delete an application', async ({ page }) => {
    const appName = generateTestName('delete-app');

    // First create an application
    await clickCreateButton(page, 'Create Application');
    await expect(page.locator('.q-dialog')).toBeVisible();
    await fillInput(page, 'Name', appName);
    await clickSave(page);
    await expectSuccessNotification(page, 'created');

    // Navigate back to applications list
    await navigateTo(page, NavDestinations.applications);
    await waitForTableLoad(page);

    // Now delete it
    await clickDeleteInRow(page, appName);
    await expect(page.locator('.q-dialog')).toBeVisible();
    await confirmDelete(page);

    // Verify success
    await expectSuccessNotification(page, 'deleted');
    await waitForTableLoad(page);
    
    // Verify it's gone
    await expectNoRowInTable(page, appName);
  });
});
