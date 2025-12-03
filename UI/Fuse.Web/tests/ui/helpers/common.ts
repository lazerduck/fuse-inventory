import { Page, Locator, expect } from '@playwright/test';

/**
 * Common UI selectors and helpers for Quasar components.
 */

/**
 * Click the "Create" or "Add" button on a page
 */
export async function clickCreateButton(page: Page, buttonText: string = 'Create'): Promise<void> {
  await page.click(`button:has-text("${buttonText}")`);
}

/**
 * Fill a Quasar input field by its label
 */
export async function fillInput(page: Page, label: string, value: string): Promise<void> {
  const input = page.locator(`.q-field:has(.q-field__label:has-text("${label}")) input, .q-input:has(.q-field__label:has-text("${label}")) input`);
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
  await page.click('button:has-text("Save"), button:has-text("Create")');
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
  await row.locator('button:has(i.q-icon:has-text("edit"))').click();
}

/**
 * Click the delete button for a row in a table
 */
export async function clickDeleteInRow(page: Page, rowText: string): Promise<void> {
  const row = page.locator(`tr:has-text("${rowText}")`).first();
  await row.locator('button:has(i.q-icon:has-text("delete"))').click();
}

/**
 * Wait for and verify a success notification
 */
export async function expectSuccessNotification(page: Page, message?: string): Promise<void> {
  const notification = page.locator('.q-notification--positive, .q-notification.bg-positive');
  await expect(notification).toBeVisible({ timeout: 5000 });
  if (message) {
    await expect(notification).toContainText(message);
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
