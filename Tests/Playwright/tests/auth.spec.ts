import { expect } from '@playwright/test';
import { test, ADMIN_USERNAME } from '../fixtures/auth';

test.describe('Authentication', () => {
  test('should login successfully via UI', async ({ page, adminCredentials }) => {
    // Navigate to the app
    await page.goto('/');
    
    await page.getByRole('button', {name: /maybe later/i}).click();
    // Click the auth button to open login dialog
    const authBtn = page.getByTestId('auth-button');
    await expect(authBtn.locator('.q-icon')).toHaveText('lock_open');
    await authBtn.click();

    await page.getByRole('textbox', { name: 'Username' }).fill(adminCredentials.username);;
    await page.getByRole('textbox', { name: 'Password' }).fill(adminCredentials.password);

    // Submit login
    await page.click('button[type="submit"]');

    // Verify successful login (adjust based on your app's behavior)
    await expect(authBtn.locator('.q-icon')).toHaveText('lock');
  });

  test('should be able to access protected API endpoints', async ({ authenticatedPage }) => {
    // Make API call from the authenticated page context
    const response = await authenticatedPage.request.get('/api/account');
    
    // Should not be unauthorized
    expect(response.status()).toBe(200);
  });
});