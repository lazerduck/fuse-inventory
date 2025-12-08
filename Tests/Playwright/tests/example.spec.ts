import { test, expect } from '@playwright/test';

test.describe('Fuse Inventory App', () => {
  test('should load the home page', async ({ page }) => {
    await page.goto('/');
    
    // Wait for the page to load
    await page.waitForLoadState('networkidle');
    
    // Basic check that the page loaded successfully
    expect(page.url()).toContain('localhost:8080');
  });

  test('should have a title', async ({ page }) => {
    await page.goto('/');
    
    // Check that the page has a title (adjust based on your app)
    await expect(page).toHaveTitle(/Fuse/i);
  });
});
