import { test, expect } from '../fixtures/auth';

test('named toolbar and dialog controls support keyboard activation', async ({ authenticatedPage: page }) => {
  await page.goto('/tags');
  const navigation = page.getByRole('button', { name: 'Toggle navigation', exact: true });
  await expect(navigation).toHaveAttribute('aria-expanded', 'true');
  await navigation.focus();
  await page.keyboard.press('Enter');
  await expect(navigation).toHaveAttribute('aria-expanded', 'false');
  await page.keyboard.press('Enter');
  await expect(navigation).toHaveAttribute('aria-expanded', 'true');

  const light = page.getByRole('button', { name: 'Switch to light theme', exact: true });
  const dark = page.getByRole('button', { name: 'Switch to dark theme', exact: true });
  const wasDark = await light.isVisible();
  await (wasDark ? light : dark).click();
  await expect(wasDark ? dark : light).toBeVisible();
  await (wasDark ? dark : light).click();
  await expect(page.getByRole('button', { name: 'Log out', exact: true })).toBeVisible();
  await expect(page.getByRole('button', { name: 'Help', exact: true })).toBeVisible();

  await page.getByRole('button', { name: 'Create Tag', exact: true }).focus();
  await page.keyboard.press('Enter');
  const dialog = page.getByRole('dialog');
  const close = dialog.getByRole('button', { name: 'Close dialog', exact: true });
  await close.focus();
  await page.keyboard.press('Enter');
  await expect(dialog).toBeHidden();
  await expect(page.getByRole('button', { name: 'Create Tag', exact: true })).toBeFocused();
});
