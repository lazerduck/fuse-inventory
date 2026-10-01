import { test, expect } from '../fixtures/auth';
test('two participants vote, reveal, reset and rejoin', async ({ browser, page, baseURL, adminApi }) => {
  const settings = await (await adminApi.get('/api/AppSettings')).json();
  expect((await adminApi.put('/api/AppSettings', { data: { ...settings, scrumPokerEnabled: true } })).ok()).toBeTruthy();
  const guestContext = await browser.newContext({ baseURL });
  const guest = await guestContext.newPage();
  try {
    await page.goto('/scrum-poker');
    await page.getByLabel('Your display name').fill('Facilitator');
    await page.getByRole('button', { name: 'Choose avatar 1', exact: true }).click();
    await page.getByRole('button', { name: 'Create room', exact: true }).click();
    await expect(page).toHaveURL(/\/scrum-poker\/[A-Za-z0-9]+$/);
    await expect(page.getByRole('heading', { name: 'Vote when ready' })).toBeVisible();
    await guest.goto(page.url());
    await guest.getByLabel('Your display name').fill('Participant');
    await guest.getByRole('button', { name: 'Choose avatar 2', exact: true }).click();
    await guest.getByRole('button', { name: 'Join room', exact: true }).click();
    await expect(page.getByText('2 in room', { exact: true })).toBeVisible();
    await page.getByRole('button', { name: '3', exact: true }).click();
    await guest.getByRole('button', { name: '5', exact: true }).click();
    await expect(page.getByText('2 of 2 voted', { exact: true })).toBeVisible();
    await page.getByRole('button', { name: 'Reveal', exact: true }).click();
    await expect(guest.getByText('Revealed', { exact: true })).toBeVisible();
    await page.getByRole('button', { name: 'Reset', exact: true }).click();
    await expect(guest.getByText('Round 2', { exact: true })).toBeVisible();
    await expect(guest.getByText('0 of 2 voted', { exact: true })).toBeVisible();
    await guest.reload();
    await expect(guest.getByRole('heading', { name: 'Vote when ready' })).toBeVisible();
    await expect(page.getByText('2 in room', { exact: true })).toBeVisible();
  } finally { await guestContext.close(); await adminApi.put('/api/AppSettings', { data: settings }); }
});
