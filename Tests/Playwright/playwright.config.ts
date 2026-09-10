import { defineConfig, devices } from '@playwright/test';
export default defineConfig({
  testDir: './tests',
  // Security posture is server-wide. Keep tests serial; contexts still isolate sessions.
  fullyParallel: false,
  workers: 1,
  forbidOnly: !!process.env.CI,
  retries: 0,
  timeout: 30_000,
  expect: { timeout: 7_000 },
  reporter: [['list'], ['html', { open: 'never' }], ['json', { outputFile: 'artifacts/results.json' }]],
  use: {
    baseURL: process.env.PLAYWRIGHT_BASE_URL || 'http://127.0.0.1:5099',
    trace: 'retain-on-failure',
    screenshot: 'only-on-failure',
    video: 'retain-on-failure',
  },
  projects: [
    { name: 'bootstrap', testMatch: 'bootstrap.spec.ts', use: { ...devices['Desktop Chrome'] } },
    { name: 'chromium', testIgnore: 'bootstrap.spec.ts', dependencies: ['bootstrap'], use: { ...devices['Desktop Chrome'] } },
  ],
});
