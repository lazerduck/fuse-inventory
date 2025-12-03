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
  generateTestName
} from './helpers';

test.describe('Data Stores', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('/');
    await waitForPageLoad(page);
    await navigateTo(page, NavDestinations.dataStores);
    await waitForTableLoad(page);
  });

  test('can view data stores page', async ({ page }) => {
    await expect(page.locator('h1:has-text("Data Stores")')).toBeVisible();
    await expect(page.locator('[data-tour-id="data-stores-table"]')).toBeVisible();
  });

  test('can create a data store', async ({ page }) => {
    const storeName = generateTestName('test-datastore');

    // Click create button
    await clickCreateButton(page, 'Create Data Store');
    
    // Wait for dialog to open
    await expect(page.locator('.q-dialog')).toBeVisible();

    // Fill in the form
    await fillInput(page, 'Name', storeName);
    await fillInput(page, 'Kind', 'SQL Server');
    await fillInput(page, 'Connection URI', 'server=localhost;database=testdb');

    // Submit the form
    await clickSave(page);

    // Verify success
    await expectSuccessNotification(page, 'created');
    await waitForTableLoad(page);
    await expectRowInTable(page, storeName);
  });

  test('can edit a data store', async ({ page }) => {
    const storeName = generateTestName('edit-datastore');

    // First create a data store
    await clickCreateButton(page, 'Create Data Store');
    await expect(page.locator('.q-dialog')).toBeVisible();
    await fillInput(page, 'Name', storeName);
    await fillInput(page, 'Kind', 'PostgreSQL');
    await clickSave(page);
    await expectSuccessNotification(page, 'created');
    await waitForTableLoad(page);

    // Now edit it
    await clickEditInRow(page, storeName);
    await expect(page.locator('.q-dialog')).toBeVisible();

    // Update the connection URI
    await fillInput(page, 'Connection URI', 'host=localhost;port=5432');
    await clickSave(page);

    // Verify success
    await expectSuccessNotification(page, 'updated');
    await waitForTableLoad(page);
    
    // Verify the data store still exists
    await expectRowInTable(page, storeName);
  });

  test('can delete a data store', async ({ page }) => {
    const storeName = generateTestName('delete-datastore');

    // First create a data store
    await clickCreateButton(page, 'Create Data Store');
    await expect(page.locator('.q-dialog')).toBeVisible();
    await fillInput(page, 'Name', storeName);
    await clickSave(page);
    await expectSuccessNotification(page, 'created');
    await waitForTableLoad(page);

    // Now delete it
    await clickDeleteInRow(page, storeName);
    await expect(page.locator('.q-dialog')).toBeVisible();
    await confirmDelete(page);

    // Verify success
    await expectSuccessNotification(page, 'deleted');
    await waitForTableLoad(page);
    
    // Verify it's gone
    await expectNoRowInTable(page, storeName);
  });
});
