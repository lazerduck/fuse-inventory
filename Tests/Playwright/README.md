# Fuse UI regression tests

Requires Node 22, Docker Compose v2 and the Playwright Chromium browser.

```bash
cd Tests/Playwright
npm ci
npx playwright install chromium
npm test
```

`npm test` builds the application, starts a unique Compose project with a new named data volume on loopback port 5099, waits for the setup API, runs Chromium, captures server logs, and removes that project's containers, volume and image. No ordinary Fuse data directory is mounted. Set `FUSE_TEST_PORT=5100` when another checkout is running a suite on this machine. Report paths are shared within a checkout, so run one suite at a time in each checkout. The default worker count is one because security posture and settings are server-wide.

The bootstrap project tests initial administrator creation and environment onboarding through the UI. All other tests depend on bootstrap, run in fresh browser contexts, and use unique records. API fixtures seed prerequisites and verify persistence; the tested action runs through the UI. Authentication tests use the login dialog, while ordinary authenticated journeys seed the application's token storage from a real API login.

```bash
npm test -- --grep 'tags:'
npm run test:headed
npm run typecheck
npm run report
```

`npm run test:existing` is an advanced option for a **fresh disposable** server; set `PLAYWRIGHT_BASE_URL`. Bootstrap refuses initialised data. This command does not own or clean up the external server. Do not point it at a normal installation.

Retries are disabled. Failed tests retain screenshots, video and traces in `test-results/`; the HTML report is in `playwright-report/` and server output in `artifacts/server.log`. Reports may contain disposable credentials/tokens from the local test instance; CI retains them for 14 days. SIGKILL or host shutdown cannot run cleanup; remove only the `fuse-e2e-*` project shown in that run's startup log if interrupted.

Component tests live in `UI/Fuse.Web/tests` and run with `npm test` from that directory. They mount real Vue/Quasar controls with isolated Pinia and query caches. Tags dialog teleporting is stubbed; data-service boundaries are mocked. Browser tests separately exercise the real persistence and permission endpoints.

See [the coverage plan](../UI_TEST_PLAN.md) and [product findings](../UI_TEST_FINDINGS.md). Explicit expected failures reference a finding and must fail on their intended behavioural assertion; unexpected success fails CI. Mocked integration states do not establish connectivity to live SQL, Azure or Kuma services.
