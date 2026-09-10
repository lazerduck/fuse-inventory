# UI test expansion plan

## Objective

Build dependable UI regression coverage while preserving application behaviour. Record product defects with reproduction steps for a separate fix pass. Test infrastructure and fixtures can be corrected as part of this work; do not silently change application behaviour to make tests pass.

## Baseline explored on 2026-09-10

- Existing suite: two Chromium Playwright tests; no component test framework configured.
- Isolated application built from the current Dockerfile and started at http://localhost:5088.
- Docker project/container: `fuse-ui-exploration`; data volume: `fuse-ui-exploration_exploration-data`. No ordinary application data is mounted.
- Local override: `/tmp/fuse-ui-exploration/compose.override.yaml`. Docker Desktop rejected a bind mount from `/tmp`, so the environment uses a dedicated Docker volume.
- Existing tests were copied to `/tmp/fuse-ui-exploration/playwright` with only the base URL and report destination adjusted. Run with one Chromium worker and a 10-second per-test timeout. Both failed on fresh data.
- Baseline log: `/tmp/fuse-ui-exploration/baseline.log`; failure contexts: `/tmp/fuse-ui-exploration/results`.
- Browser exploration successfully exercised login in locked-down mode, the first-run environment wizard, tag creation, navigation, and application creation. Application creation navigates directly to the new application's details page.
- This is initial reconnaissance, not a comprehensive acceptance pass.

### Existing test infrastructure findings

1. The README describes automatic Docker startup, but `playwright.config.ts` has no `webServer` or equivalent startup hook.
2. The fixture reads `RequiresSetup`; the API responds with camel-case `requiresSetup`. Initial account creation also needs `isAdmin: true` and role IDs, rather than the old `role: 'Admin'` payload.
3. One test unconditionally waits for “Maybe later”; on fresh data the application redirects to administrator setup instead. This was the first test's observed failure.
4. The authenticated-page fixture navigates to `/login` and uses old input selectors. The current UI uses a toolbar login dialog. This was the second test's observed failure.
5. The API test assumes `page.request` inherits the UI's bearer-token authentication. The application stores a bearer token in local storage; API requests need an explicit authorization fixture. In unrestricted mode a 200 response would not establish that authentication worked. This is a code-review finding; the baseline failed earlier in setup.
6. Setup currently runs per test and can race between workers. URLs and credentials are hardcoded, and the Docker container name can conflict with a normal instance.
7. Documentation lists Firefox/WebKit and an example spec that are not present/enabled. CI does not run the browser suite.

## Goals and completion criteria

| Goal | Coverage | Done when |
| --- | --- | --- |
| 1. Reproducible harness | Isolated server/data, configurable URL, readiness checks, deterministic accounts, onboarding state, cleanup and failure artifacts | A documented command starts from fresh data and runs a green baseline locally and in CI; repeated runs do not depend on prior data |
| 2. Authentication and permissions | Login/logout/failure, anonymous access, all security postures, admin, read-only and action-specific roles, unrelated permissions, revoked permissions | UI controls and attempted requests agree with the expected access matrix; protected API checks prove actual authentication |
| 3. Core inventory journeys | Tags, environments, applications, instances, pipelines, dependencies, accounts/grants, identities/assignments, datastores, platforms, external resources, brokers | Create/edit/delete and persistence after reload are covered, including linked-record behaviour and separate entities surviving each operation |
| 4. Forms and resilience | Required/invalid/duplicate values, cancel, failed requests, slow requests, empty lists, search, pagination, direct links, unavailable records | Component tests cover field/action combinations; selected browser tests verify visible errors, recovery and absence of unintended writes |
| 5. Supporting workflows | Positions/responsibilities, risks, graph and blast radius, documentation completeness, activity/history/undo, configuration import/export | Each workflow has explicit assertions about observable behaviour; complex data operations use isolated fixtures |
| 6. Integration and collaboration UI | SQL/Kuma/Azure screens, health, feature/license states, Scrum Poker participants/voting/reveal/reset/rejoin | Deterministic integration responses cover UI states without requiring live credentials; multi-context browser tests cover Scrum Poker |
| 7. Sustainable CI | Fast checks on PRs, Chromium end-to-end checks, traces/screenshots/logs on failure, broader scheduled coverage where justified | Failures are diagnosable, retries do not conceal instability, and the coverage map lists remaining gaps |

## Approach

- Use Vitest and Vue Test Utils for component/composable behaviour, especially permission and form matrices. Mount real components where possible; do not just assert that permission strings appear in source.
- Use headless Playwright for real UI/API journeys. Start with Chromium; introduce additional browsers after the harness is stable.
- Keep fresh-install/onboarding scenarios separate from seeded everyday-use scenarios. Seed a normal environment before ordinary CRUD tests and handle guide state explicitly.
- Seed prerequisites via the API, but perform the action under test through the UI. Verify both the visible outcome and persistence where relevant.
- Give tests unique records and isolated browser contexts. Posture/settings tests need isolated server data or a deliberately serial group; browser-context isolation alone does not isolate backend state.
- Use accessible roles and labels, scoped to the relevant dialog or row. Use stable test IDs only where semantic locators are insufficient. Avoid arbitrary sleeps, generated Quasar IDs, icon text assertions and broad URL regexes as success criteria.
- Keep real-backend tests distinct from tests that intercept responses. Mock external integration boundaries, not the persistence/authentication behaviour a real-backend test claims to verify.
- Record known product failures explicitly with expected versus actual behaviour. Any temporary expected-failure annotation must reference the finding and be counted in the report; do not silently skip broken behaviour or weaken assertions.
- Add representative narrow-viewport and keyboard smoke checks once core journeys are established. Treat visual screenshot baselines as a later, selective layer.

## Product observations to investigate separately

- Several icon-only action buttons expose no descriptive accessible name in the browser snapshot. This complicates keyboard/screen-reader use and stable semantic selectors. Inventory and label the affected controls before changing them.
- The Security page still describes locked-down mode as “Read account required to read, Admin to edit.” This wording needs review against the granular role model.
- The guide panel affects the available page area during first-run exploration. Include it in responsive/layout coverage; the initial exploration does not establish a product defect.

## First implementation milestone

Repair the harness and authentication fixtures, establish component testing, and complete Tags coverage as a reference implementation. Then expand across the inventory and supporting workflows using the goals above. This plan defines the larger scope; the first milestone is not the end of the effort.

## Local exploration lifecycle

The exploration instance can remain available between work sessions. Its data is disposable and separate from the application's normal volume.

```bash
# Stop the exploration container, retaining its disposable data.
docker compose -p fuse-ui-exploration \
  -f Tests/Playwright/docker-compose.test.yaml \
  -f /tmp/fuse-ui-exploration/compose.override.yaml down

# Restart after stopping. The /tmp override must still exist.
docker compose -p fuse-ui-exploration \
  -f Tests/Playwright/docker-compose.test.yaml \
  -f /tmp/fuse-ui-exploration/compose.override.yaml up -d
```

The temporary override and exploratory records are not the final automated-test harness.

## Implemented coverage

The suite now has two layers: mounted Vue/Quasar component tests and Chromium tests against a disposable production Docker build. The browser bootstrap project exercises administrator setup and the environment wizard before any seeded journeys. Browser contexts isolate sessions; one worker avoids races on server-wide settings. The initial test-only pass did not modify product source code; the subsequent UI-002/UI-003 fix pass updates permission loading and pagination wiring.

| Area | Regression assertions |
| --- | --- |
| Authentication | Invalid password, real login, session after reload, logout, protected bearer API access, anonymous access under all three postures |
| Granular access | Tags read/create/update/delete and unrelated-write roles; admin; open-form revocation and delete confirmation recheck; backend revocation during an open form; UI-002 permission-resolution regression |
| Inventory CRUD | Tags, environments, platforms, data stores, brokers, external resources, positions and responsibility types: create, cancelled edit, save, reload, delete and backend persistence |
| Linked inventory | Application details preserve instances, pipelines and dependencies; account details preserve grants; identity details preserve assignments; deletion leaves independently owned target records intact |
| Forms and recovery | Required names, blank/duplicate tag names, cancelled drafts, read-only forms, failed and delayed saves, retry after a failed save, search across reload, pagination navigation and UI-003 reload regression, missing application recovery |
| Supporting workflows | Ownership assignments; risk creation and mitigation; history/undo; dependency blast-radius links; completeness alerts linking to applications; configuration export, malformed import and round-trip import |
| Graph and layout | Canvas/filter smoke checks with JavaScript error capture; keyboard-driven tag creation at a 600px viewport |
| Integration boundaries | Unconfigured SQL/Kuma/Azure screens; SQL list and permissions failures; mocked Kuma Up/Down states and environment filter; Azure Vault/App Configuration failures and retry; valid/expired license display |
| Collaboration | Disabled feature state, two independent Scrum Poker contexts, room creation/join, votes, reveal, reset and reload/rejoin |
| CI | Component tests and their type check, production build, browser fixture type check, Chromium run, HTML report/traces/videos/screenshots and server logs; image publishing waits for UI checks |

### Explicit limits and next extensions

This is a broad regression baseline, not exhaustive coverage of every combination on every screen. The permission matrix is strongest on Tags; other inventory pages have administrator journeys. Graph tests cover rendering/filter controls, not pixel-perfect node/edge placement. Instance/pipeline lifecycle is covered through creation, preservation and parent deletion; independently editing/deleting each nested record, cross-environment cloning, large imports, concurrent edit conflicts, detailed SQL drift resolution, Azure secret rotation/reveal and every Scrum room setting remain useful extensions. External-service responses are deterministic test fixtures and do not prove live connectivity. Firefox/WebKit, phone-sized visual baselines and a full accessibility audit are not enabled.

The original product defects and their subsequent resolutions are tracked in `UI_TEST_FINDINGS.md`.

### Verification on 2026-09-10

- Component suite: 23 passed; component/source type check passed.
- Final fresh-data browser run: 46 passed, 2 documented expected failures (UI-002 and UI-003), zero unexpected failures, zero skips and zero flaky results. Chromium execution took approximately 2 minutes 16 seconds, excluding Docker build/startup.
- Production Docker build and browser-fixture type check passed. Existing dependency versions were preserved; new dependencies are development/test-only.
- The final test project's container, volume, network and image were removed. The separate earlier exploration instance remains available.
- CI configuration is added; a hosted GitHub Actions run has not been triggered from this workspace.
- Local results: `Tests/Playwright/artifacts/results.json`, `Tests/Playwright/artifacts/server.log`, and `Tests/Playwright/playwright-report/index.html` (generated files, ignored by Git).


### Defect fix follow-up

UI-002 and UI-003 are fixed. The browser role matrix no longer grants `roles:read`, and both former expected failures are normal passing-test requirements. The security-state response carries the signed-in user’s assigned permission keys while role-definition access remains protected. Persisted pagination updates are wired on all 11 affected pages, and filter/page restoration runs before the table’s initial render. See `UI_TEST_FINDINGS.md` for the resolutions; the initial verification figures above describe the pre-fix baseline.

Verification after the fixes on 2026-09-10:

- Backend non-integration suite: 601 passed.
- Component suite: 26 passed; component/source type check passed.
- Fresh-data Chromium suite: 48 passed, with zero expected failures, unexpected failures, skips or flaky results.
- Production Docker build and browser-fixture type check passed; the disposable browser-test environment was cleaned up.
