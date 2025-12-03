import { Page, Locator, expect } from '@playwright/test';

/**
 * Common UI selectors and helpers for Quasar components.
 */

/**
 * Dismiss any onboarding or initial dialogs that may appear on page load
 */
export async function dismissInitialDialogs(page: Page): Promise<void> {
  // Try to close onboarding dialog if present (click "Maybe later" button)
  const maybeLaterButton = page.locator('.q-dialog button:has-text("Maybe later")');
  if (await maybeLaterButton.isVisible({ timeout: 2000 }).catch(() => false)) {
    await maybeLaterButton.click();
    await page.waitForTimeout(500);
  }
  
  // Try to close any other dialogs with close button
  const closeButton = page.locator('.q-dialog button:has(i.q-icon:has-text("close"))');
  if (await closeButton.isVisible({ timeout: 500 }).catch(() => false)) {
    await closeButton.click();
    await page.waitForTimeout(300);
  }
}

/**
 * Click the "Create" or "Add" button on a page
 */
export async function clickCreateButton(page: Page, buttonText: string = 'Create'): Promise<void> {
  await page.click(`button:has-text("${buttonText}")`);
}

/**
 * Fill a Quasar input field by its label
 * Uses aria-label for more precise matching
 */
export async function fillInput(page: Page, label: string, value: string): Promise<void> {
  // Try to find by aria-label first for more precision
  const inputByAriaLabel = page.locator(`input[aria-label="${label}"], input[aria-label="${label}*"]`);
  if (await inputByAriaLabel.count() === 1) {
    await inputByAriaLabel.fill(value);
    return;
  }
  
  // Fall back to label-based selection
  const input = page.locator(`.q-field:has(.q-field__label:has-text("${label}")) input, .q-input:has(.q-field__label:has-text("${label}")) input`).first();
  await input.fill(value);
}

/**
 * Fill a Quasar textarea by its label
 */
export async function fillTextarea(page: Page, label: string, value: string): Promise<void> {
  const textarea = page.locator(`.q-field:has(.q-field__label:has-text("${label}")) textarea`);
  await textarea.fill(value);
}

/**
 * Click the Save button in a dialog or form
 */
export async function clickSave(page: Page): Promise<void> {
  // First try to click the dialog button (more specific)
  const dialogButton = page.locator('.q-dialog button:has-text("Save"), .q-dialog button:has-text("Create"), .q-dialog button:has-text("CREATE")');
  if (await dialogButton.isVisible({ timeout: 1000 }).catch(() => false)) {
    await dialogButton.click();
  } else {
    // Fall back to any save/create button
    await page.click('button:has-text("Save"), button:has-text("Create")');
  }
}

/**
 * Click the Cancel button in a dialog
 */
export async function clickCancel(page: Page): Promise<void> {
  await page.click('button:has-text("Cancel")');
}

/**
 * Click the Delete button 
 */
export async function clickDelete(page: Page): Promise<void> {
  await page.click('button:has(i.q-icon:has-text("delete"))');
}

/**
 * Confirm a delete action in a Quasar dialog
 */
export async function confirmDelete(page: Page): Promise<void> {
  await page.click('.q-dialog button:has-text("OK")');
}

/**
 * Cancel a delete action in a Quasar dialog
 */
export async function cancelDelete(page: Page): Promise<void> {
  await page.click('.q-dialog button:has-text("Cancel")');
}

/**
 * Click the edit button for a row in a table
 */
export async function clickEditInRow(page: Page, rowText: string): Promise<void> {
  const row = page.locator(`tr:has-text("${rowText}")`).first();
  // Find the edit button - it has color="primary" and contains edit icon
  await row.locator('button.text-primary').first().click();
}

/**
 * Click the delete button for a row in a table
 */
export async function clickDeleteInRow(page: Page, rowText: string): Promise<void> {
  const row = page.locator(`tr:has-text("${rowText}")`).first();
  // Find the delete button - it has color="negative" and contains delete icon
  await row.locator('button.text-negative').first().click();
}

/**
 * Wait for and verify a success notification
 */
export async function expectSuccessNotification(page: Page, message?: string): Promise<void> {
  if (message) {
    // Look for notification containing the specific message
    const notification = page.locator(`.q-notification--positive:has-text("${message}"), .q-notification.bg-positive:has-text("${message}")`).first();
    await expect(notification).toBeVisible({ timeout: 5000 });
  } else {
    // Just check any success notification is visible
    const notification = page.locator('.q-notification--positive, .q-notification.bg-positive').first();
    await expect(notification).toBeVisible({ timeout: 5000 });
  }
}

/**
 * Wait for a table to finish loading
 */
export async function waitForTableLoad(page: Page): Promise<void> {
  // Wait for the loading indicator to disappear
  const loadingIndicator = page.locator('.q-table__loading');
  await loadingIndicator.waitFor({ state: 'hidden', timeout: 10000 }).catch(() => {});
}

/**
 * Check if a row exists in a table
 */
export async function expectRowInTable(page: Page, text: string): Promise<void> {
  await expect(page.locator(`tr:has-text("${text}")`).first()).toBeVisible();
}

/**
 * Check that a row does not exist in a table
 */
export async function expectNoRowInTable(page: Page, text: string): Promise<void> {
  await expect(page.locator(`tr:has-text("${text}")`).first()).not.toBeVisible();
}

/**
 * Close any open dialogs
 */
export async function closeDialog(page: Page): Promise<void> {
  const closeButton = page.locator('.q-dialog button:has(i.q-icon:has-text("close"))');
  if (await closeButton.isVisible()) {
    await closeButton.click();
  }
}

/**
 * Generate a unique test name to avoid conflicts
 */
export function generateTestName(prefix: string): string {
  return `${prefix}-${Date.now()}`;
}
