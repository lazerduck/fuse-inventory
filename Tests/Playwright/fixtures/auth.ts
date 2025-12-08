import { test as baseTest, expect } from '@playwright/test';

interface AuthFixtures {
  authenticatedPage: void;
  adminToken: string;
}

const test = baseTest.extend<AuthFixtures>({
  adminToken: async ({}, use) => {
    const baseURL = 'http://localhost:8080';
    const username = 'initialAdmin';
    const password = 'InitialPassword123!';

    // Check security state to see if setup is needed
    const stateResponse = await fetch(`${baseURL}/api/security/state`);
    const state = await stateResponse.json();

    let token = '';

    // If setup is required, create the initial admin account
    if (state.RequiresSetup) {
      const createResponse = await fetch(`${baseURL}/api/security/accounts`, {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify({
          userName: username,
          password: password,
          role: 'Admin',
        }),
      });

      if (!createResponse.ok) {
        throw new Error(`Failed to create admin account: ${createResponse.statusText}`);
      }
    }

    // Login to get the token
    const loginResponse = await fetch(`${baseURL}/api/security/login`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
      },
      body: JSON.stringify({
        userName: username,
        password: password,
      }),
    });

    if (!loginResponse.ok) {
      throw new Error(`Failed to login: ${loginResponse.statusText}`);
    }

    const loginData = await loginResponse.json();
    token = loginData.Token;

    // Use the fixture
    await use(token);
  },

  authenticatedPage: async ({ page, adminToken }, use) => {
    // Set the auth token in local storage for authenticated requests
    await page.goto('/'); // Navigate to app first
    await page.evaluate((token) => {
      localStorage.setItem('authToken', token);
    }, adminToken);

    // Add auth header to all requests
    await page.setExtraHTTPHeaders({
      'Authorization': `Bearer ${adminToken}`,
    });

    await use();
  },
});

export { test };