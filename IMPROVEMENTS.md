# Fuse-Inventory — Feature backlog

Last reviewed: 2026-10-31

Reviewed against the local application source, tests, and infrastructure repository. “Implemented” means code is present, not that deployment or runtime verification was performed in this review. Pending items are proposals, not scheduled commitments. This is the main backlog; [Scrum Poker](SCRUM_POKER_PLAN.md) and the [UI test plan](Tests/UI_TEST_PLAN.md) track detailed follow-up work.

## Pending features and enhancements

- [ ] **Webhook notifications:** publish inventory/audit events to configured destinations. No outbound notification implementation found.
- [ ] **Metrics export / Grafana integration:** expose Fuse metrics for external monitoring. `KumaMetricsService` consumes Kuma's Prometheus metrics; it does not export Fuse metrics.
- [ ] **Scheduled/off-site backups:** add scheduling, timestamped history, and optional remote destinations. JSON/YAML export and a local pre-import `fuse-data.bak` archive already exist (`ConfigController`, `ConfigService`, `JsonFuseStore`).
- [ ] **Inventory import validation and preview:** show proposed additions/changes and validation results before applying a configuration import. Parsing and pre-import backup exist, but the current import immediately merges data; SQL permission import dialogs are separate.
- [ ] **Entity version comparison:** expose a readable before/after comparison between inventory versions. Application activity now compares fields, instances, and dependencies. Comparison for other entity types remains open.
- [ ] **Bulk inventory edits:** support changes across selected applications/instances and other entities. SQL permission bulk resolution already exists; general bulk editing was not found.
- [ ] **Kubernetes / Helm packaging:** provide a chart and deployment guidance. No chart found in either local repository.
- [ ] **Fuse/integration health indicator in navigation:** service health pages and instance health cards already exist. A global indicator backed by Fuse component health / integration connectivity remains unimplemented; distinguish this from monitored application health.

## Pending repository maintenance

- [ ] Add a human-readable `CHANGELOG.md`.
- [ ] Add `CONTRIBUTING.md`, issue templates, and a pull request template. `.github/workflows/ci.yml` exists, but contributor templates were not found.
- [ ] Add a root `.editorconfig`.

## Implemented from the original backlog

- [x] **Health endpoints:** `HealthController` exposes `/api/health/live`, `/api/health/ready`, and `/api/health/status`; readiness/status can return 503.
- [x] **Correct solution filename:** the root solution is now `fuse-inventory.sln`.
- [x] **Retention and cleanup:** version-history, audit-log, and system-log retention settings exist in `AppSettingsPage.vue`; workers are registered in `FuseCodeModule.cs`, with retention tests.
- [x] **First-run onboarding:** `InitialSetupWizard.vue`, `CheatSheetDialog.vue`, and `useOnboardingTour.ts` are wired into `App.vue`.

## Scrum Poker follow-up

The backend, frontend, and a two-context Playwright journey exist. See [the updated plan](SCRUM_POKER_PLAN.md) for remaining concurrency, lifecycle, privacy, and disabled-state verification. Test presence is not a fresh passing run.

## Roadmap items needing definition

The README previously listed “additional Azure Key Vault tooling” without acceptance criteria. Existing Key Vault secret-management tooling is documented in [features.md](features.md); define the specific additional capability before treating this as actionable work. Guided setup is already implemented; further usability improvements need similarly specific scope.
