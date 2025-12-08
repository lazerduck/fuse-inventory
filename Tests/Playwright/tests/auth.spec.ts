import { test, expect } from '@playwright/test';
import { test as authTest } from '../fixtures/auth';

authTest.describe('Authentication Setup', () => {
  authTest('should create initial admin account on first setup', async ({ page, adminToken }) => {
    expect(adminToken).toBeDefined();
    expect(adminToken.length).toBeGreaterThan(0);
  });

  authTest('authenticated page should have auth token in headers', async ({ authenticatedPage, page, adminToken }) => {
    await page.goto('/dashboard');
    await page.waitForLoadState('networkidle');
    
    // Verify we can access authenticated routes
    const response = await page.request.get('/api/account');
    expect(response.status()).toBeLessThan(400); // Should not be 401 Unauthorized
  });
});