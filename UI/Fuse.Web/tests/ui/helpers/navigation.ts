import { Page, expect } from '@playwright/test';

/**
 * Navigate to a page in the Fuse-Inventory application.
 * Uses the data-tour-id attributes for reliable navigation.
 */
export async function navigateTo(page: Page, navItemId: string): Promise<void> {
  await page.click(`[data-tour-id="${navItemId}"]`);
  await page.waitForLoadState('networkidle');
}

/**
 * Common navigation destinations
 */
export const NavDestinations = {
  home: 'nav-home',
  applications: 'nav-applications',
  accounts: 'nav-accounts',
  identities: 'nav-identities',
  dataStores: 'nav-data-stores',
  platforms: 'nav-platforms',
  environments: 'nav-environments',
  externalResources: 'nav-external-resources',
  tags: 'nav-tags',
  graph: 'nav-tags', // Note: Graph uses nav-tags in the sidebar, but it has insights icon
  config: 'nav-config',
  security: 'nav-security',
  auditLogs: 'nav-audit-logs',
} as const;

/**
 * Wait for the page to be fully loaded
 */
export async function waitForPageLoad(page: Page): Promise<void> {
  await page.waitForLoadState('networkidle');
}

/**
 * Ensure the sidebar is visible/open
 */
export async function ensureSidebarOpen(page: Page): Promise<void> {
  // Check if sidebar is visible by looking for a nav item
  const sidebarItem = page.locator('[data-tour-id="nav-home"]');
  const isVisible = await sidebarItem.isVisible().catch(() => false);
  
  if (!isVisible) {
    // Click the menu button to open the sidebar
    await page.click('button[aria-label="Main menu"], .q-btn:has(i.q-icon:has-text("menu"))');
    await expect(sidebarItem).toBeVisible();
  }
}
