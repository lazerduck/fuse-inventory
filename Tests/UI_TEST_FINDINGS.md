# UI test findings

Product behaviour is deliberately unchanged by the regression-test work.

## UI-001 — controls without accessible names

Tags and several inventory tables expose edit/delete as unnamed icon buttons. Dependency row inputs and actions are also unnamed. Tests currently scope these controls to a uniquely identified row and use their documented order. Add descriptive accessible names in a separate accessibility pass.

## UI-002 — UI permission resolution requires an unrelated permission

Create a role with only `tags:read`, assign a non-admin account, sign in, and open `/tags` under FullyRestricted security. An authenticated GET `/api/Tag` is allowed, but the UI displays its permission-denied banner and hides the table.

`FuseStore.resolveUserPermissions` loads each assigned role through GET `/api/Role/{id}`. That endpoint requires `roles:read`, so resolution yields an empty permission list for these users. Expected: a user can exercise their assigned inventory permissions without receiving permission to inspect all roles.

`permissions.spec.ts` contains an explicit expected-failure regression after verifying backend access. The remaining real-role matrix includes `roles:read` to exercise individual Tags controls; that dependency is not treated as the intended permission model. Unexpected success makes the suite fail so the annotation is removed when the defect is fixed.

## Observations, not confirmed defects

- Security copy describing FullyRestricted as “Read account required to read, Admin to edit” predates granular permissions.
- A completely empty data directory reports readiness 503 until initial data is written. The harness waits for the setup API and verifies readiness after onboarding.

## UI-003 — Tags pagination is not restored after reload

Create at least 12 tags, filter to that group, move to page 2, then reload. The filter is retained but the table returns to page 1. Expected: page 2 is restored, as the page's persisted-table-state integration intends.

`TagsPage` supplies `:pagination="pagination"` without handling the table's pagination updates. The persistence composable watches an object that is not updated when the user changes pages. The browser regression first proves next/previous navigation works, then marks only the reload assertion as an expected failure. Other pages using this binding pattern should be reviewed in the fix pass.
