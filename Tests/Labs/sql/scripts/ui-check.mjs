#!/usr/bin/env node
// Read-only browser smoke for a locally seeded Fuse SQL integration lab.
// No mocks, traces, storage-state files, credentials, or auth responses are saved.
import fs from 'node:fs/promises';
import path from 'node:path';
import { createRequire } from 'node:module';
import { fileURLToPath } from 'node:url';

process.umask(0o077);

function options(argv) {
  const result = {};
  for (let i = 0; i < argv.length; i += 2) {
    if (!['--lab-dir', '--output', '--fixtures', '--definition-dir'].includes(argv[i]) || !argv[i + 1]) {
      throw new Error('Usage: ui-check.mjs --lab-dir PATH --output DIRECTORY [--fixtures PATH] [--definition-dir PATH]');
    }
    result[argv[i].slice(2)] = path.resolve(argv[i + 1]);
  }
  if (!result['lab-dir'] || !result.output) throw new Error('Both --lab-dir and --output are required.');
  return result;
}

function parseEnv(text) {
  return Object.fromEntries(text.split(/\r?\n/).flatMap(line => {
    const match = line.match(/^([A-Za-z_][A-Za-z0-9_]*)=(.*)$/);
    if (!match) return [];
    let value = match[2].trim();
    if ((value.startsWith('"') && value.endsWith('"')) || (value.startsWith("'") && value.endsWith("'"))) value = value.slice(1, -1);
    return [[match[1], value]];
  }));
}

function requireCondition(condition, message) {
  if (!condition) throw new Error(message);
}

const statusLabels = { InSync: 'In Sync', DriftDetected: 'Drift', MissingPrincipal: 'Missing Principal', Error: 'Error', NotApplicable: 'N/A' };
const summaryLabels = { totalAccounts: 'Total Accounts', inSyncCount: 'In Sync', driftCount: 'Drift Detected', missingPrincipalCount: 'Missing Principal', errorCount: 'Errors' };

async function main() {
  const args = options(process.argv.slice(2));
  await fs.mkdir(args.output, { recursive: true, mode: 0o700 });
  const report = { startedAt: new Date().toISOString(), status: 'failed', checks: [], screenshots: [], runtimeErrors: [], apiErrors: [], unexpectedMutations: [] };
  let browser;
  let page;
  let signedIn = false;
  let stage = 'configuration';
  let secretValues = [];
  const redact = value => {
    let text = String(value);
    for (const secret of secretValues) if (secret.length >= 4) text = text.replaceAll(secret, '[redacted]');
    return text.replace(/eyJ[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+/g, '[redacted-token]').replace(/(?:password|pwd)\s*[=:]\s*[^;\s]+/gi, 'password=[redacted]').slice(0, 1800);
  };
  const pass = (name, detail) => report.checks.push({ name, status: 'passed', ...(detail === undefined ? {} : { detail }) });
  const saveScreenshot = async filename => {
    // Never capture a login dialog or any other visible password form.
    if (!signedIn || !page || await page.locator('input[type="password"]:visible').count()) return;
    await page.screenshot({ path: path.join(args.output, filename), fullPage: true, animations: 'disabled', mask: [page.locator('.q-banner:visible')] });
    await fs.chmod(path.join(args.output, filename), 0o600);
    report.screenshots.push(filename);
  };

  try {
    const env = parseEnv(await fs.readFile(path.join(args['lab-dir'], '.env'), 'utf8'));
    secretValues = Object.entries(env).filter(([key]) => /PASSWORD|SECRET|TOKEN/i.test(key)).map(([, value]) => value).filter(Boolean);
    requireCondition(env.FUSE_ADMIN_USERNAME && env.FUSE_ADMIN_PASSWORD, 'Lab administrator credentials are missing from .env.');
    const instance = JSON.parse(await fs.readFile(path.join(args['lab-dir'], 'instance.json'), 'utf8'));
    requireCondition(instance.managed === true && instance.scenario === 'sql', 'Browser validation requires a managed SQL instance.');
    requireCondition(typeof instance.project_name === 'string' && /^[a-z0-9][a-z0-9_-]*$/.test(instance.project_name)
      && env.COMPOSE_PROJECT_NAME === instance.project_name, 'Instance metadata and environment disagree on Compose project name.');
    requireCondition(env.FUSE_COMMIT === instance.source_commit, 'Instance metadata and environment disagree on the source commit.');
    const port = instance.ports?.http;
    requireCondition(Number.isInteger(port) && port > 0 && port <= 65535 && env.FUSE_PORT === String(port), 'Invalid or mismatched HTTP port.');
    report.instance = instance.name;
    report.projectName = instance.project_name;
    const baseURL = `http://127.0.0.1:${port}`;
    report.baseURL = baseURL;
    report.commit = env.FUSE_COMMIT || null;
    const fixtures = args.fixtures ? JSON.parse(await fs.readFile(args.fixtures, 'utf8')) : null;
    const definitionDir = args['definition-dir'] || path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
    const require = createRequire(path.join(definitionDir, 'package.json'));
    const { chromium, expect } = require('@playwright/test');
    stage = 'browser launch';
    browser = await chromium.launch({ headless: true });
    const context = await browser.newContext({ baseURL, viewport: { width: 1600, height: 1100 } });
    page = await context.newPage();
    page.setDefaultTimeout(20000);
    page.setDefaultNavigationTimeout(30000);
    page.on('pageerror', error => report.runtimeErrors.push(redact(error.message)));
    page.on('response', response => {
      const pathname = new URL(response.url()).pathname;
      if (signedIn && pathname.startsWith('/api/') && response.status() >= 400) report.apiErrors.push({ method: response.request().method(), path: pathname, status: response.status() });
    });
    page.on('request', request => {
      const pathname = new URL(request.url()).pathname;
      if (signedIn && pathname.startsWith('/api/') && !['GET', 'HEAD', 'OPTIONS'].includes(request.method())) report.unexpectedMutations.push({ method: request.method(), path: pathname });
    });
    await page.addInitScript(() => localStorage.setItem('fuse_onboarding_state', JSON.stringify({ hasCompletedTour: true, dismissedBanner: true, activeGuideId: null, guideProgressVersion: 3 })));

    stage = 'actual UI login';
    await page.goto('/sql-integrations');
    await page.getByTestId('auth-button').click();
    const dialog = page.getByRole('dialog');
    await dialog.getByLabel('Username', { exact: true }).fill(env.FUSE_ADMIN_USERNAME);
    await dialog.getByLabel('Password', { exact: true }).fill(env.FUSE_ADMIN_PASSWORD);
    await dialog.getByRole('button', { name: 'Login', exact: true }).click();
    await expect(dialog).toBeHidden();
    signedIn = true;
    pass('Administrator signs in through the actual login form');

    stage = 'SQL integration list';
    const [response] = await Promise.all([
      page.waitForResponse(response => new URL(response.url()).pathname.toLowerCase() === '/api/sqlintegration' && response.request().method() === 'GET'),
      page.goto('/sql-integrations')
    ]);
    requireCondition(response.ok(), `Integration list returned HTTP ${response.status()}.`);
    const integrations = await response.json();
    requireCondition(Array.isArray(integrations) && integrations.length > 0, 'No live SQL integrations were returned. Run the API validator first.');
    const expectedIds = fixtures?.integrations ? Object.values(fixtures.integrations).filter(Boolean) : integrations.map(item => item.id);
    requireCondition(expectedIds.length > 0, 'Fixture manifest contains no SQL integrations.');
    const expectedIntegrations = expectedIds.map(id => {
      const integration = integrations.find(item => item.id === id);
      requireCondition(integration, `Seeded integration ${id} was not returned by the live API.`);
      return integration;
    });
    await expect(page.getByRole('heading', { name: 'SQL Integrations', exact: true })).toBeVisible();
    await expect(page.getByRole('button', { name: 'Add Integration', exact: true })).toBeEnabled();
    for (const integration of expectedIntegrations) {
      const row = page.getByRole('row').filter({ has: page.getByRole('cell', { name: integration.name, exact: true }) });
      await expect(row).toBeVisible();
      for (const permission of String(integration.permissions || '').split(',').map(value => value.trim()).filter(Boolean)) await expect(row.locator('.q-badge').filter({ hasText: new RegExp(`^${permission}$`) })).toBeVisible();
      await expect(row.getByRole('button', { name: 'View SQL permissions', exact: true })).toBeEnabled();
    }
    report.integrations = expectedIntegrations.map(({ id, name, permissions }) => ({ id, name, permissions }));
    pass('Seeded integrations and capability badges render', { count: expectedIntegrations.length });
    await saveScreenshot('sql-integrations.png');

    stage = 'manager permissions overview';
    const manager = fixtures?.integrations?.manager ? integrations.find(item => item.id === fixtures.integrations.manager) : integrations.find(item => String(item.permissions).includes('Write') && String(item.permissions).includes('Create'));
    requireCondition(manager, 'No manager integration with Write/Create capabilities was found.');
    const managerRow = page.getByRole('row').filter({ has: page.getByRole('cell', { name: manager.name, exact: true }) });
    const [overviewHttp] = await Promise.all([
      page.waitForResponse(result => new URL(result.url()).pathname.toLowerCase() === `/api/sqlintegration/${manager.id}/permissions-overview`.toLowerCase() && result.request().method() === 'GET'),
      managerRow.getByRole('button', { name: 'View SQL permissions', exact: true }).click()
    ]);
    requireCondition(overviewHttp.ok(), `Permissions overview returned HTTP ${overviewHttp.status()}.`);
    const payload = await overviewHttp.json();
    const overview = payload.overview;
    requireCondition(overview && Array.isArray(overview.accounts), 'Live overview response has no account collection.');
    requireCondition(!overview.errorMessage, 'Live overview contains an integration error; inspect API validation results.');
    await expect(page).toHaveURL(`${baseURL}/sql-integrations/${manager.id}/permissions`);
    await expect(page.getByRole('heading', { name: manager.name, exact: true })).toBeVisible();
    await expect(page.getByText('Account Permissions', { exact: true })).toBeVisible();
    const expectedAccountIds = fixtures?.accounts ? Object.entries(fixtures.accounts).filter(([key]) => key.startsWith('manager/')).map(([, id]) => id) : overview.accounts.map(account => account.accountId);
    requireCondition(expectedAccountIds.length > 0, 'No seeded manager accounts are available to check.');
    const expectedAccounts = expectedAccountIds.map(id => {
      const account = overview.accounts.find(item => item.accountId === id);
      requireCondition(account, `Seeded account ${id} is missing from the live overview.`);
      return account;
    });
    for (const account of expectedAccounts) {
      const row = page.getByRole('row').filter({ has: page.locator(`a[href="/accounts/${account.accountId}/edit"]`) });
      await expect(row).toBeVisible();
      await expect(row.getByRole('cell').nth(1)).toHaveText(account.principalName);
      const label = statusLabels[account.status];
      requireCondition(label, `Unknown sync status ${account.status}.`);
      await expect(row.locator('.q-badge').filter({ hasText: new RegExp(`^${label}$`) })).toBeVisible();
    }
    for (const [key, label] of Object.entries(summaryLabels)) {
      const item = page.locator('.summary-item').filter({ has: page.getByText(label, { exact: true }) });
      await expect(item.locator('.summary-value')).toHaveText(String(overview.summary?.[key] ?? 0));
    }
    report.overview = { integrationId: manager.id, integrationName: manager.name, cachedAt: payload.cachedAt, summary: overview.summary, accounts: expectedAccounts.map(({ accountId, principalName, status }) => ({ accountId, principalName, status })) };
    pass('Seeded account rows and status badges match the live API', { count: expectedAccounts.length });
    pass('Permission summary counts match the live API');
    await saveScreenshot('sql-permissions.png');
    stage = 'browser health';
    requireCondition(report.runtimeErrors.length === 0, 'Browser JavaScript errors occurred.');
    requireCondition(report.apiErrors.length === 0, 'Authenticated browser API requests failed.');
    requireCondition(report.unexpectedMutations.length === 0, 'The browser issued an unexpected API mutation.');
    pass('No JavaScript errors, failed authenticated API calls, or data mutations');
    report.status = 'passed';
  } catch (error) {
    report.failure = { stage, message: redact(error.message || error) };
    report.checks.push({ name: stage, status: 'failed' });
    try { await saveScreenshot('ui-failure.png'); } catch { /* Preserve original failure. */ }
  } finally {
    try { await browser?.close(); } catch { /* Summary remains useful even after browser termination. */ }
    report.finishedAt = new Date().toISOString();
    const reportPath = path.join(args.output, 'ui-summary.json');
    await fs.writeFile(reportPath, `${JSON.stringify(report, null, 2)}\n`, { mode: 0o600 });
    await fs.chmod(reportPath, 0o600);
    console.log(`UI smoke ${report.status}; ${report.checks.filter(check => check.status === 'passed').length} checks passed. Report: ${reportPath}`);
    if (report.failure) console.log(`Failure stage: ${report.failure.stage}`);
    process.exitCode = report.status === 'passed' ? 0 : 1;
  }
}

main().catch(error => {
  console.error(`UI smoke configuration error: ${error.message}`);
  process.exitCode = 1;
});
