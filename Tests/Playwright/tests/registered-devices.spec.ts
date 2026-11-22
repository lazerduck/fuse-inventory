import { test, expect } from '../fixtures/auth';
import { randomUUID } from 'node:crypto';

test('registered devices filters the fleet and opens the selected server or cluster node', async ({ authenticatedPage: page }) => {
  const server = { platformId: randomUUID(), platformName: 'Build server', platformKind: 'Server',
    hostname: 'build-01', state: 'no-contact', pendingCount: 0 };
  const node = { platformId: randomUUID(), platformName: 'Compute cluster', platformKind: 'Cluster',
    nodeId: randomUUID(), nodeName: 'Compute node B', hostname: 'compute-b', state: 'reporting', pendingCount: 7 };
  const overdue = { platformId: randomUUID(), platformName: 'Cache host', platformKind: 'ContainerHost',
    hostname: 'cache-01', state: 'overdue' };
  const now = new Date();
  const minutesAgo = (minutes: number) => new Date(now.getTime() - minutes * 60_000).toISOString();
  const gib = 1024 ** 3;

  await page.route('**/api/platforms/agents', route => route.fulfill({ json: [
    { ...server, nodeId: null, nodeName: null, lastSeenAt: minutesAgo(10), assessmentAt: minutesAgo(12), indexUpdatedAt: minutesAgo(20),
      telemetryAt: minutesAgo(10), cpuAveragePercent: 12, memoryTotalBytes: 8 * gib, memoryAvailableBytes: 6 * gib,
      diskTotalBytes: 512 * gib, diskAvailableBytes: 426 * gib },
    { ...node, lastSeenAt: now.toISOString(), assessmentAt: minutesAgo(3), indexUpdatedAt: minutesAgo(20),
      telemetryAt: now.toISOString(), cpuAveragePercent: 29, memoryTotalBytes: 32 * gib, memoryAvailableBytes: 20 * gib,
      diskTotalBytes: 256 * gib, diskAvailableBytes: 96 * gib },
    { ...overdue, nodeId: null, nodeName: null, lastSeenAt: minutesAgo(2), pendingCount: null, assessmentAt: null },
  ] }));

  const detailRequests: string[] = [];
  await page.route(/\/api\/platforms\/[^/]+\/agent(?:\?|$)/, async route => {
    const url = new URL(route.request().url());
    detailRequests.push(url.pathname + url.search);
    const target = [server, node].find(item => url.pathname === `/api/platforms/${item.platformId}/agent` &&
      url.searchParams.get('nodeId') === ('nodeId' in item ? item.nodeId : null));
    if (!target) return route.fulfill({ status: 404, json: { error: 'Unknown agent target' } });
    return route.fulfill({ json: { state: target.state, hostname: target.hostname, version: '0.0.3',
      firstSeenAt: minutesAgo(60), lastSeenAt: target === node ? now.toISOString() : minutesAgo(10),
      supportsAssessment: true } });
  });

  await page.emulateMedia({ colorScheme: 'dark' });
  await page.goto('/platforms');
  await page.locator('.page-header').getByRole('link', { name: 'Registered devices' }).click();
  await expect(page.getByRole('heading', { name: 'Registered devices' })).toBeVisible();
  const device = (name: string) => page.getByRole('link', { name, exact: true });
  await expect(device(server.platformName)).toBeVisible();
  await expect(device(node.nodeName)).toBeVisible();
  await expect(device(overdue.platformName)).toBeVisible();
  expect(detailRequests).toEqual([]);

  const filter = (name: string) => page.getByRole('button', { name, exact: true });
  await expect(filter('All devices (3)')).toBeVisible();
  await filter('Offline (1)').click();
  await expect(device(node.nodeName)).toHaveCount(0);
  await expect(device(overdue.platformName)).toHaveCount(0);
  await device(server.platformName).click();
  const dialog = page.getByRole('dialog');
  await expect(dialog.getByText(server.hostname, { exact: true })).toBeVisible();
  await dialog.getByRole('button', { name: 'Close agent reporting' }).click();
  await expect(dialog).toBeHidden();

  await filter('Overdue (1)').click();
  await expect(device(overdue.platformName)).toBeVisible();
  await filter('Updates pending (1)').click();
  await expect(device(server.platformName)).toHaveCount(0);
  await expect(device(overdue.platformName)).toHaveCount(0);
  await device(node.nodeName).click();
  await expect(dialog.getByText(node.hostname, { exact: true })).toBeVisible();
  await expect(dialog.getByRole('tab', { name: 'Usage history' })).toBeVisible();
  await dialog.getByRole('button', { name: 'Close agent reporting' }).click();
  await expect(dialog).toBeHidden();
  await filter('All devices (3)').click();
  await page.emulateMedia({ colorScheme: 'light' });
  await page.setViewportSize({ width: 390, height: 844 });
  await expect(device(node.nodeName)).toBeVisible();
  await expect.poll(() => page.evaluate(() => document.documentElement.scrollWidth)).toBeLessThanOrEqual(390);
});
