import { test, expect } from '@playwright/test';
import { 
  navigateTo, 
  NavDestinations, 
  waitForPageLoad,
  clickCreateButton,
  waitForTableLoad,
  dismissInitialDialogs
} from './helpers';

test.describe('Data Stores', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('/');
    await waitForPageLoad(page);
    await dismissInitialDialogs(page);
    await navigateTo(page, NavDestinations.dataStores);
    await waitForTableLoad(page);
  });

  test('can view data stores page', async ({ page }) => {
    await expect(page.locator('h1:has-text("Data Stores")')).toBeVisible();
    await expect(page.locator('[data-tour-id="data-stores-table"]')).toBeVisible();
  });

  test('can open create data store dialog', async ({ page }) => {
    // Click create button
    await clickCreateButton(page, 'Create Data Store');
    
    // Wait for dialog to open
    await expect(page.locator('.q-dialog')).toBeVisible();
    
    // Verify form fields are present
    await expect(page.locator('text=Name')).toBeVisible();
    await expect(page.locator('text=Kind')).toBeVisible();
    await expect(page.locator('text=Environment')).toBeVisible();
    
    // Close the dialog by clicking close button or Cancel
    const cancelBtn = page.locator('.q-dialog .q-card-actions button:has-text("Cancel")');
    await cancelBtn.click();
    await expect(page.locator('.q-dialog .q-card')).not.toBeVisible({ timeout: 5000 });
  });
});
