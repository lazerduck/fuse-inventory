import { test as base, expect, type APIRequestContext, type Page } from '@playwright/test';
import { credentials } from './setup';
export { expect, credentials };
export async function signIn(page: Page, api: APIRequestContext, user = credentials) {
  const response = await api.post('/api/security/login', { data: user });
  expect(response.ok(), await response.text()).toBeTruthy();
  const session = await response.json();
  await page.addInitScript(({ token, expiresAt }) => {
    localStorage.setItem('fuse_auth_token', token);
    localStorage.setItem('fuse_auth_expiry', expiresAt);
    localStorage.setItem('fuse_onboarding_state', JSON.stringify({ hasCompletedTour: true, dismissedBanner: true, activeGuideId: null, guideProgressVersion: 3 }));
  }, session);
  return session;
}
export const test = base.extend<{ adminApi: APIRequestContext; authenticatedPage: Page }>({
  adminApi: async ({ playwright, baseURL }, use) => {
    const anonymous = await playwright.request.newContext({ baseURL });
    const response = await anonymous.post('/api/security/login', { data: credentials });
    expect(response.ok(), await response.text()).toBeTruthy();
    const { token } = await response.json();
    const api = await playwright.request.newContext({ baseURL, extraHTTPHeaders: { Authorization: `Bearer ${token}` } });
    try { await use(api); } finally { await api.dispose(); await anonymous.dispose(); }
  },
  authenticatedPage: async ({ page, request }, use) => {
    await signIn(page, request);
    await use(page);
  },
});
