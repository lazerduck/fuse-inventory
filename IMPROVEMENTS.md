# Fuse-Inventory — Potential Improvements

Last reviewed: 2026-06-27

---

## 🔧 Quick wins (high value, low effort)

### 1. Health check endpoints
No `/health` or `/ready` endpoints at all. Docker compose and any container orchestrator would treat this as blind. A simple `/health` that verifies the data directory is readable and the JSON files load.

### 2. Changelog
`CHANGELOG.md` with semver-style entries. The git history has ~200+ commits but no human-readable summary.

### 3. CONTRIBUTING.md + PR template
No `CONTRIBUTING.md`, no `.github/ISSUE_TEMPLATE`, no PR template. New contributors have zero guidance.

### 4. `.editorconfig`
No editor config to enforce style at the file level.

### 5. Fix solution file typo
`fuse-invetory.sln` → `fuse-inventory.sln`. Late-stage, risky, but worth flagging.

---

## 🔌 Integrations worth considering

### 6. Webhook / notification integrations
No push-notifications when something changes. A simple webhook target that fires on audit events would let people pipe into Slack/Discord/Teams.

### 7. Grafana data source / metrics export
With activity feed, audit logs, and health data, you have metrics. Exporting as Prometheus-compatible or at least a JSON metrics endpoint would make self-hosters' dashboards richer.

### 8. Backup integration
Currently it's "mount a volume and hope." A backup endpoint that generates a timestamped full JSON export (secrets redacted or configurable) and optionally pushes to a webhook/S3/GCS.

---

## 📊 Data & storage

### 9. Data rotation / cleanup
Audit logs and version history grow forever. A retention policy setting with a background cleanup to prevent unbounded growth.

### 10. Import validation with preview
Import currently just merges. A "validate and preview" step showing what would change before committing.

---

## 🎯 User experience

### 11. First-run onboarding
No onboarding flow. `OnboardingStore.ts` exists but there's no guided tour. A "create your first application" nudge.

### 12. Entity version diff viewer
Version history stored (EntityVersion model) but the UI doesn't expose a human-readable diff between versions. A before/after comparison view.

### 13. Bulk operations on entities
Bulk SQL permission resolution exists, but no bulk-edit for regular entities like "change owner for all instances of app X across all environments."

---

## 🏗️ Platform / infrastructure

### 14. Kubernetes / Helm chart
Docker and docker-compose covered. A Helm chart for K8s clusters.

### 15. Health status in UI
Once API has `/health`, show a status dot in the navbar (Connected to Uptime-Kuma ✓ / ⚠).

---

## Already done well

- **Blast Radius page** — impact surface of a single application. Clever UX.
- **Documentation Completeness page** — missing critical info at a glance.
- **SQL permission drift detection with bulk resolve** — genuinely hard problem, solved well.
- **Read-only/public documentation mode** — sharing views without login.
- **NSwag → TypeScript client generation** — API/frontend types in sync.
- **Copilot instructions** — thorough, good for AI-assisted contribution.
- **Licensing system** — well-structured with service + UI + validation worker.

---

## Done

### 1. ✅ Health check endpoints
- PR #215 created on `health-check-endpoints` branch → `main`
- `GET /api/health/live` — liveness (always 200)
- `GET /api/health/ready` — readiness (200/503)
- `GET /api/health/status` — full component report
- Components checked: data-directory, json-files, lite-db