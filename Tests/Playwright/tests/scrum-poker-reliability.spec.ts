import type { APIResponse, BrowserContext, Page, Route } from '@playwright/test';
import { test as base, expect } from '../fixtures/auth';

// Real server and independent anonymous participants; only the failure under
// investigation (network/lifecycle) is injected by the individual test.
const test = base.extend<{ poker: { host: Page; guest: Page } }>({
  poker: async ({ browser, page: host, baseURL, adminApi }, use) => {
    const settings = await (await adminApi.get('/api/AppSettings')).json();
    expect((await adminApi.put('/api/AppSettings', {
      data: { ...settings, scrumPokerEnabled: true },
    })).ok()).toBeTruthy();
    const context = await browser.newContext({ baseURL });
    const guest = await context.newPage();
    try {
      await host.goto('/scrum-poker');
      await enter(host, 'Facilitator', 'Create room');
      await expect(host).toHaveURL(/\/scrum-poker\/[A-Za-z0-9]+$/);
      await guest.goto(host.url());
      await enter(guest, 'Participant', 'Join room');
      await expect(host.getByText('2 in room', { exact: true })).toBeVisible();
      await use({ host, guest });
    } finally {
      await context.close();
      expect((await adminApi.put('/api/AppSettings', { data: settings })).ok()).toBeTruthy();
    }
  },
});

async function enter(page: Page, name: string, action: string) {
  await page.getByLabel('Your display name').fill(name);
  await page.getByRole('button', { name: 'Choose avatar 1', exact: true }).click();
  await page.getByRole('button', { name: action, exact: true }).click();
  await expect(page.getByRole('heading', { name: 'Vote when ready' })).toBeVisible();
}

const stateUrl = /\/api\/scrum-poker\/rooms\/[^/]+\/state\?/;
const card = (page: Page, value: string) => page.getByRole('button', { name: value, exact: true });

test('a delayed poll cannot roll back a completed reset', async ({ poker }) => {
  const { host, guest } = poker;
  await card(guest, '5').click();
  await expect(host.getByText('1 of 2 voted', { exact: true })).toBeVisible();
  let capture = true;
  let captured: { route: Route; response: APIResponse } | undefined;
  await guest.route(stateUrl, async route => {
    if (capture) {
      capture = false;
      captured = { route, response: await route.fetch() };
    }
    // Hold later polls too, representing a slow connection. Mutation requests
    // still reach the real server and the host continues polling normally.
  });
  await expect.poll(() => !!captured).toBe(true);
  expect((await captured!.response.json()).round).toBe(1);
  await guest.getByRole('button', { name: 'Reset', exact: true }).click();
  await expect(guest.getByText('Round 2', { exact: true })).toBeVisible();
  await expect(host.getByText('Round 2', { exact: true })).toBeVisible();
  const delivered = guest.waitForResponse(stateUrl);
  await captured!.route.fulfill({ response: captured!.response });
  await (await delivered).finished();
  // Let fetch handlers and Vue render the delivered snapshot before asserting.
  await guest.evaluate(() => new Promise<void>(resolve => {
    requestAnimationFrame(() => requestAnimationFrame(() => resolve()));
  }));
  test.fail(true, 'SP-003: stale response ordering is deferred; see Tests/UI_TEST_FINDINGS.md.');
  await expect(guest.getByText('Round 2', { exact: true })).toBeVisible();
  await expect(guest.locator('.poker-card--selected')).toHaveCount(0);
});

test('frozen background tab catches up with reveal and reset, then votes again', async ({ poker }) => {
  const { host, guest } = poker;
  await card(guest, '5').click();
  await expect(host.getByText('1 of 2 voted', { exact: true })).toBeVisible();
  const cdp = await guest.context().newCDPSession(guest);
  try {
    // Freeze the renderer, not just dispatch a synthetic visibility event.
    await cdp.send('Page.setWebLifecycleState', { state: 'frozen' });
    await card(host, '3').click();
    await host.getByRole('button', { name: 'Reveal', exact: true }).click();
    await expect(host.getByText('Revealed', { exact: true })).toBeVisible();
    await host.getByRole('button', { name: 'Reset', exact: true }).click();
    await expect(host.getByText('Round 2', { exact: true })).toBeVisible();
    await cdp.send('Page.setWebLifecycleState', { state: 'active' });
    await guest.bringToFront();
    await expect(guest.getByText('Round 2', { exact: true })).toBeVisible();
    await expect(guest.getByText('0 of 2 voted', { exact: true })).toBeVisible();
    await expect(guest.locator('.poker-card--selected')).toHaveCount(0);
    await card(guest, '8').click();
    await expect(host.getByText('1 of 2 voted', { exact: true })).toBeVisible();
  } finally {
    await cdp.send('Page.setWebLifecycleState', { state: 'active' });
    await cdp.detach();
  }
});

test('reload keeps the participant identity and their vote', async ({ poker }) => {
  const { host, guest } = poker;
  await card(guest, '5').click();
  await expect(host.getByText('1 of 2 voted', { exact: true })).toBeVisible();
  const identity = await guest.evaluate(() => sessionStorage.getItem(`fuse:scrum-poker:${location.pathname.split('/').pop()}`));
  await guest.reload();
  await expect(card(guest, '5')).toHaveClass(/poker-card--selected/);
  await expect(host.getByText('2 in room', { exact: true })).toBeVisible();
  expect(await guest.evaluate(() => sessionStorage.getItem(`fuse:scrum-poker:${location.pathname.split('/').pop()}`))).toBe(identity);
  await card(guest, '8').click();
  await host.getByRole('button', { name: 'Reveal', exact: true }).click();
  await expect(host.locator('.q-item').filter({ has: host.locator('.participant-name-label', { hasText: /^Participant/ }) }).locator('.participant-card')).toHaveText('8');
});

test('separate tabs in one browser keep independent votes and leaving one keeps the other', async ({ poker }) => {
  const { host, guest } = poker;
  const anotherTab = await guest.context().newPage();
  await anotherTab.goto(host.url());
  await enter(anotherTab, 'Another tab', 'Join room');
  await expect(host.getByText('3 in room', { exact: true })).toBeVisible();
  await card(guest, '5').click();
  await card(anotherTab, '8').click();
  await expect(host.getByText('2 of 3 voted', { exact: true })).toBeVisible();
  await anotherTab.getByRole('button', { name: 'Leave room', exact: true }).click();
  await expect(host.getByText('2 in room', { exact: true })).toBeVisible();
  await expect(card(guest, '5')).toHaveClass(/poker-card--selected/);
  await card(guest, '3').click();
  await host.getByRole('button', { name: 'Reveal', exact: true }).click();
  await expect(host.locator('.q-item').filter({ has: host.locator('.participant-name-label', { hasText: /^Participant/ }) }).locator('.participant-card')).toHaveText('3');
});

for (const interruption of ['offline', '503'] as const) {
  test(`recovers automatically after a temporary ${interruption} polling failure`, async ({ poker }) => {
    const { host, guest } = poker;
    await card(guest, '5').click();
    await expect(host.getByText('1 of 2 voted', { exact: true })).toBeVisible();
    if (interruption === 'offline') {
      await guest.context().setOffline(true);
    } else {
      await guest.route(stateUrl, route => route.fulfill({
        status: 503, contentType: 'application/json', body: JSON.stringify({ error: 'Temporary test outage' }),
      }));
    }
    await expect(guest.locator('.banner-error')).toBeVisible();
    await host.getByRole('button', { name: 'Reset', exact: true }).click();
    await expect(host.getByText('Round 2', { exact: true })).toBeVisible();
    if (interruption === 'offline') await guest.context().setOffline(false);
    else await guest.unroute(stateUrl);
    await guest.bringToFront();
    // SP-001: recovery must not require a reload or another card action.
    await expect(guest.getByText('Round 2', { exact: true })).toBeVisible();
    await expect(guest.locator('.banner-error')).toBeHidden();
    await card(guest, '8').click();
    await expect(host.getByText('1 of 2 voted', { exact: true })).toBeVisible();
  });
}


test('eight participants join and vote through three rounds with suspension and reconnects', async ({ poker, browser, baseURL }) => {
  test.setTimeout(90_000);
  const { host, guest } = poker;
  const contexts: BrowserContext[] = [];
  try {
    const others = await Promise.all(Array.from({ length: 6 }, async (_, index) => {
      const context = await browser.newContext({ baseURL });
      contexts.push(context);
      const page = await context.newPage();
      await page.goto(host.url());
      await enter(page, `Colleague ${index + 1}`, 'Join room');
      return page;
    }));
    const pages = [host, guest, ...others];
    await Promise.all(pages.map(page => expect(page.getByText('8 in room', { exact: true })).toBeVisible()));
    const votes = ['1', '2', '3', '5', '8', '13', '20', '40'];
    for (let round = 1; round <= 3; round++) {
      await Promise.all(pages.map((page, i) => card(page, votes[i]).click()));
      await Promise.all(pages.map(page => expect(page.getByText('8 of 8 voted', { exact: true })).toBeVisible()));
      await host.getByRole('button', { name: 'Reveal', exact: true }).click();
      await Promise.all(pages.map(async page => {
        await expect(page.getByText('Revealed', { exact: true })).toBeVisible();
        await expect.poll(async () => (await page.locator('.participant-list .participant-card').allTextContents())
          .map(value => value.trim()).sort()).toEqual([...votes].sort());
      }));
      if (round === 3) break;
      const frozen = pages[6];
      const offline = pages[7];
      const cdp = await frozen.context().newCDPSession(frozen);
      try {
        await cdp.send('Page.setWebLifecycleState', { state: 'frozen' });
        await offline.context().setOffline(true);
        await expect(offline.locator('.banner-error')).toContainText('Reconnecting automatically');
        await host.getByRole('button', { name: 'Reset', exact: true }).click();
        await expect(host.getByText(`Round ${round + 1}`, { exact: true })).toBeVisible();
        await cdp.send('Page.setWebLifecycleState', { state: 'active' });
        await offline.context().setOffline(false);
        await Promise.all(pages.map(async page => {
          await expect(page.getByText(`Round ${round + 1}`, { exact: true })).toBeVisible();
          await expect(page.getByText('0 of 8 voted', { exact: true })).toBeVisible();
          await expect(page.locator('.banner-error')).toBeHidden();
        }));
      } finally {
        await cdp.send('Page.setWebLifecycleState', { state: 'active' });
        await cdp.detach();
        await offline.context().setOffline(false);
      }
    }
  } finally {
    await Promise.all(contexts.map(context => context.close()));
  }
});

test('repeated failures retry with backoff and recover without a focus event', async ({ poker }) => {
  test.setTimeout(40_000);
  const { host, guest } = poker;
  await card(guest, '5').click();
  await expect(host.getByText('1 of 2 voted', { exact: true })).toBeVisible();
  const failures: number[] = [];
  await guest.route(stateUrl, route => {
    failures.push(Date.now());
    return route.fulfill({ status: 503, contentType: 'application/json', body: '{}' });
  });
  await expect.poll(() => failures.length, { timeout: 12_000 }).toBeGreaterThanOrEqual(3);
  expect(failures[1] - failures[0]).toBeGreaterThanOrEqual(1500);
  expect(failures[2] - failures[1]).toBeGreaterThanOrEqual(3500);
  await host.getByRole('button', { name: 'Reset', exact: true }).click();
  await guest.unroute(stateUrl);
  // No bringToFront or online event: the scheduled retry itself must recover.
  await expect(guest.getByText('Round 2', { exact: true })).toBeVisible({ timeout: 12_000 });
  await expect(guest.locator('.banner-error')).toBeHidden();
  await expect(guest.getByText('2 in room', { exact: true })).toBeVisible();
});

test('a stalled poll times out and reconnects instead of accumulating requests', async ({ poker }) => {
  test.setTimeout(30_000);
  const { host, guest } = poker;
  await card(guest, '5').click();
  await expect(host.getByText('1 of 2 voted', { exact: true })).toBeVisible();
  let requests = 0;
  await guest.route(stateUrl, async route => {
    requests++;
    if (requests > 1) await route.continue();
    // The first request deliberately never returns.
  });
  await expect.poll(() => requests).toBe(1);
  await host.getByRole('button', { name: 'Reset', exact: true }).click();
  await expect(guest.locator('.banner-error')).toContainText('Reconnecting automatically', { timeout: 12_000 });
  expect(requests).toBe(1);
  await expect(guest.getByText('Round 2', { exact: true })).toBeVisible();
  await expect(guest.locator('.banner-error')).toBeHidden();
});

test('a temporary rejoin failure retains the participant and retries', async ({ poker, adminApi }) => {
  const { host, guest } = poker;
  const saved = await guest.evaluate(() => JSON.parse(sessionStorage.getItem(`fuse:scrum-poker:${location.pathname.split('/').pop()}`)!));
  const { roomCode, participantToken, participantId } = saved.session;
  let rejoinAttempts = 0;
  await guest.route(/\/api\/scrum-poker\/rooms\/[^/]+\/enter$/, route => {
    rejoinAttempts++;
    if (rejoinAttempts === 1) return route.fulfill({ status: 503, contentType: 'application/json', body: '{}' });
    return route.continue();
  });
  // Simulate removal from active membership without changing server timeouts.
  expect((await adminApi.post(`/api/scrum-poker/rooms/${roomCode}/leave`, { data: { participantToken } })).ok()).toBeTruthy();
  await expect(guest.locator('.banner-error')).toContainText('Reconnecting automatically');
  await expect(guest.getByRole('heading', { name: 'Vote when ready' })).toBeVisible();
  await expect(guest.locator('.banner-error')).toBeHidden();
  await expect(host.getByText('2 in room', { exact: true })).toBeVisible();
  expect(rejoinAttempts).toBeGreaterThanOrEqual(2);
  const room = await (await adminApi.get(`/api/scrum-poker/rooms/${roomCode}/state`, { params: { participantToken } })).json();
  expect(room.participants.filter((participant: { id: string }) => participant.id === participantId)).toHaveLength(1);
  await card(guest, '8').click();
  await expect(host.getByText('1 of 2 voted', { exact: true })).toBeVisible();
});
