import { test as baseTest, expect, Page } from '@playwright/test';

// Shared admin credentials - created once via API, used via UI
const ADMIN_USERNAME = 'initialAdmin';
const ADMIN_PASSWORD = 'InitialPassword123!';

interface AuthFixtures {
  authenticatedPage: Page;
  adminCredentials: { username: string; password: string };
}

/**
 * Helper to ensure admin account exists via API
 * This runs once per worker to set up the initial admin account
 */
async function ensureAdminAccountExists() {
  const baseURL = 'http://localhost:8080';

  // Check security state to see if setup is needed
  const stateResponse = await fetch(`${baseURL}/api/security/state`);
  const state = await stateResponse.json();

  // If setup is required, create the initial admin account
  if (state.RequiresSetup) {
    const createResponse = await fetch(`${baseURL}/api/security/accounts`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
      },
      body: JSON.stringify({
        userName: ADMIN_USERNAME,
        password: ADMIN_PASSWORD,
        role: 'Admin',
      }),
    });

    if (!createResponse.ok) {
      const error = await createResponse.text();
      throw new Error(`Failed to create admin account: ${createResponse.statusText} - ${error}`);
    }
  }
}

const test = baseTest.extend<AuthFixtures>({
  // Provide admin credentials to tests (without logging in)
  adminCredentials: async ({}, use) => {
    await ensureAdminAccountExists();
    await use({ username: ADMIN_USERNAME, password: ADMIN_PASSWORD });
  },

  // Pre-authenticated page - logs in via UI and provides authenticated page
  authenticatedPage: async ({ page, adminCredentials }, use) => {
    await ensureAdminAccountExists();

    // Navigate to login page
    await page.goto('/login');

    // Fill in login form
    await page.fill('input[name="username"]', adminCredentials.username);
    await page.fill('input[name="password"]', adminCredentials.password);

    // Submit login form
    await page.click('button[type="submit"]');

    // Wait for navigation to complete (adjust selector based on your app)
    await page.waitForURL(/\/(dashboard|home|\w+)/, { timeout: 10000 });

    // Provide the authenticated page to the test
    await use(page);
  },
});

export { test, ADMIN_USERNAME, ADMIN_PASSWORD };