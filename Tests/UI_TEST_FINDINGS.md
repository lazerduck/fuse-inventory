# UI test findings

The initial regression pass preserved product behaviour. UI-002 and UI-003 are now fixed; UI-001 remains a separate accessibility improvement.

## UI-001 — controls without accessible names

Tags and several inventory tables expose edit/delete as unnamed icon buttons. Dependency row inputs and actions are also unnamed. Tests currently scope these controls to a uniquely identified row and use their documented order. Add descriptive accessible names in a separate accessibility pass.

## UI-002 — Fixed: UI permission resolution requires an unrelated permission

Create a role with only `tags:read`, assign a non-admin account, sign in, and open `/tags` under FullyRestricted security. An authenticated GET `/api/Tag` is allowed, but the UI displays its permission-denied banner and hides the table.

Before the fix, `FuseStore.resolveUserPermissions` loaded each assigned role through GET `/api/Role/{id}`. That endpoint requires `roles:read`, so resolution yielded an empty permission list for these users. Expected: a user can exercise their assigned inventory permissions without receiving permission to inspect all roles.

Resolution: the security-state response supplies only the signed-in user’s assigned permission keys. The UI consumes that list directly instead of fetching role definitions. Role-management endpoints still require `roles:read`. The full role matrix now omits the workaround permission, and the former expected failure is a normal regression test that also checks role endpoints return 403. Backend and component tests cover anonymous access, failed resolution, deduplication, refresh and revoked permissions.

## Observations, not confirmed defects

- Security copy describing FullyRestricted as “Read account required to read, Admin to edit” predates granular permissions.
- A completely empty data directory reports readiness 503 until initial data is written. The harness waits for the setup API and verifies readiness after onboarding.

## UI-003 — Fixed: Tags pagination is not restored after reload

Create at least 12 tags, filter to that group, move to page 2, then reload. The filter is retained but the table returns to page 1. Expected: page 2 is restored, as the page's persisted-table-state integration intends.

Before the fix, `TagsPage` supplied `:pagination="pagination"` without handling the table's pagination updates. The persistence composable watched an object that was not updated when the user changes pages. Resolution: all 11 pages using persisted table state now copy table pagination updates into the existing reactive pagination object. This preserves the composable’s reference and allows it to save changes. The composable also restores the saved filter/page during setup, before QTable mounts; restoring the filter later caused QTable to reset the page again. A mounted-table regression covers that initialisation sequence. The Tags browser regression checks next/previous navigation and restoration after reload without an expected-failure annotation.
