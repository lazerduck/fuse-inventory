import { test, expect } from '@playwright/test';
import { 
  navigateTo, 
  NavDestinations, 
  waitForPageLoad,
  dismissInitialDialogs
} from './helpers';

test.describe('Accounts', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('/');
    await waitForPageLoad(page);
    await dismissInitialDialogs(page);
    await navigateTo(page, NavDestinations.accounts);
  });

  test('can view accounts page', async ({ page }) => {
    await expect(page.locator('h1:has-text("Accounts")')).toBeVisible();
    await expect(page.locator('button:has-text("Create Account")')).toBeVisible();
  });

  test('can navigate to create account page', async ({ page }) => {
    // Click create button - accounts use page navigation
    await page.click('button:has-text("Create Account")');
    
    // Wait for navigation to account create page
    await expect(page).toHaveURL(/\/accounts\/create/);
    
    // Verify the page loaded with account form
    await expect(page.locator('text=Account Details')).toBeVisible();
    await expect(page.locator('text=Grants & Permissions')).toBeVisible();
  });

  test('can cancel account creation', async ({ page }) => {
    // Navigate to create page
    await page.click('button:has-text("Create Account")');
    await expect(page).toHaveURL(/\/accounts\/create/);
    
    // Click cancel button
    await page.click('button:has-text("Cancel")');
    
    // Should navigate back to accounts list
    await expect(page).toHaveURL(/\/accounts$/);
    await expect(page.locator('h1:has-text("Accounts")')).toBeVisible();
  });
});
