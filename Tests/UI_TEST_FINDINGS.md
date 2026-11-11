# UI test findings

The initial regression pass preserved product behaviour. UI-001, UI-002 and UI-003 are now fixed.

## UI-001 — Fixed: controls without accessible names

Previously, Tags and several inventory tables exposed edit/delete as unnamed icon buttons, and dependency row inputs and actions were unnamed. Tests relied on button order within each row.

Resolution: icon-only actions now have descriptive accessible names across inventory tables, dependency editors, dialog close controls, navigation, and integration screens. Theme and secret-visibility controls have state-dependent names; navigation and application expansion controls expose their expanded state. Dependency inputs and selectors are explicitly named. Browser journeys now locate row actions and dependency fields by accessible name instead of position, and a keyboard regression covers navigation, theme state, dialog closing and focus restoration.

This resolves the documented missing-name defect; it is not a full accessibility audit of contrast, screen-reader flows or every custom widget.

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

## Scrum poker reliability assessment — 2026-11-11

Follow-up: SP-001 is fixed in the frontend. The user explicitly deferred changes to the 15-minute timeout behaviour (SP-002). Response ordering (SP-003) is also outside this retry fix. For the retry PR, the SP-002 preservation regression is explicitly skipped with its deferred reason. SP-003 remains an expected-failure browser regression, marked only after setup and delivery of the stale response; unexpected success fails the suite. Both desired-behaviour assertions are retained for follow-up work.

The application was built from this worktree with its Node 26 production Dockerfile. Browser tests used Node 26.13.0, Playwright 1.64.1 and Chromium against disposable Compose project `fuse-poker-2d45`, loopback port `52145`, and a dedicated named volume. No normal installation or other Compose project was used. Backend tests ran against the actual in-memory store with supplied timestamps, avoiding a real 16-minute wait.

Verified scenarios:

- Two anonymous participants create/join, vote, reveal, reset and rejoin.
- A Chromium renderer is explicitly frozen while another participant votes, reveals and resets. On resuming, it catches up and can vote again.
- Reload preserves identity and the current vote; subsequent voting works.
- Two separately opened tabs in the same browser have independent votes; leaving one retains the other.
- The existing backend boundary tests retain background participants and votes through 899 seconds, and evict at 15 minutes.
- A returning participant does not restore a previous round's vote after a reset.
- An active participant can rejoin a full 20-person room without duplication; a new participant is rejected.
- Nineteen concurrent joins plus the host, followed by twenty concurrent votes, preserve distinct identities and all votes.

Initial assessment results: 31 of 32 scrum poker backend tests pass; the same-round timeout recovery assertion fails. Four of seven browser scenarios pass; offline recovery, HTTP 503 recovery and delayed-response ordering fail on their intended behavioural assertions. The fresh-install bootstrap also passed in the initial run. Playwright TypeScript checking and the production application build pass.

PR verification: the complete scrum poker browser run succeeds with 11 passing tests and the SP-003 expected failure, including eight independent participants across three rounds. The backend run has 31 passing tests and the explicitly deferred SP-002 skip. Production build and Playwright TypeScript checking pass. The server timeout behaviour is unchanged.

The freeze test models browser suspension, and the offline test models a connection interruption. This does not certify a particular Teams version, physical OS minimisation, laptop sleep, browser memory discard, or Firefox/Safari behaviour. Backend timestamp tests establish the long-absence behaviour separately from browser lifecycle testing.

### SP-001 — Fixed: a temporary polling failure permanently stops updates

Reproduction: join two participants and vote; take the guest browser offline until a poll fails, reset the round from the host, restore connectivity and bring the guest forward. The host is on round 2 but the guest stays on round 1 beyond seven polling intervals. The same failure occurs with an injected HTTP 503 response, followed by restoring successful requests.

Previous cause: `ScrumPokerPage.vue` called `stopPolling()` for every state-fetch error other than 401/404, with no online, focus or visibility recovery handler. A single failed request could therefore leave a participant disconnected from later reveal/reset activity indefinitely.

Resolution: transient network failures, request timeouts, HTTP 408/429 and server errors retry automatically after 2, 4, 8 and then at most 10 seconds. Successful recovery restores normal one-second polling and clears the connection notice. Polls run sequentially with a 10-second request timeout; online/focus/visibility events refresh immediately when no request is active. Leaving or unmounting aborts the request and invalidates late results. Temporary errors during automatic rejoin preserve the local participant session and retry. The server timeout and vote storage are unchanged.

Browser regressions cover offline recovery, HTTP 503 recovery, repeated failures with backoff and no focus event, a stalled request, and a temporary failure during rejoin. An eight-participant scenario uses eight independent browser contexts, joins six guests concurrently with the two initial participants, and runs three rounds of concurrent voting/reveal/reset. Between rounds one participant is frozen and another is offline; all eight must catch up and show every revealed vote correctly.

### SP-002 — Deferred: timeout recovery loses the current vote

Reproduction: join a guest, vote 5 then 8, advance server time by 16 minutes, let the host poll (evicting the guest), then rejoin with the guest's original token. Identity and avatar survive, but the card is null instead of 8, despite the round remaining unchanged.

Cause: `SelectCard` updates `Participants`, but `KnownParticipants` still contains an older immutable participant record. `EvictStaleParticipants` removes the active record without saving its current state. Rejoining restores the stale record. Depending on prior leave/rejoin history this can also restore an older selection.

Required fix: preserve the latest participant state when evicting or keep the recovery registry in sync, while continuing to clear saved votes on reset. Regression: `ReturnAfterTimeout_RestoresIdentityAvatarAndLatestVoteInSameRound` in `API/Fuse.Tests/Areas/ScrumPoker/InMemoryScrumPokerStoreTests.cs`. The companion reset test passes and must remain passing.

### SP-003 — Medium: delayed responses overwrite newer room state

Reproduction: hold a real round-1 poll response, reset through the guest UI and verify both participants see round 2, then deliver the held response while subsequent polls remain delayed. The guest returns to round 1 with the previous vote; the host remains on round 2. This is stale client state, not rollback of server data.

Cause: `refreshRoom` assigns every returned snapshot directly, ignoring `revision`. The retry fix prevents overlapping polls, but an older poll can still overwrite a newer mutation response.

Required fix: reject snapshots older than the currently applied revision, ensure responses still belong to the active session, and limit overlapping polling. Regression: `a delayed poll cannot roll back a completed reset` in `Tests/Playwright/tests/scrum-poker-reliability.spec.ts`.

### Re-running

Use a free port and the normal disposable-server harness (Node 26):

```sh
cd Tests/Playwright
# Includes the documented SP-003 expected failure:
FUSE_TEST_PORT=52145 npm test -- scrum-poker
npm run typecheck
```

Backend tests, from the repository root:

```sh
dotnet test API/Fuse.Tests/Fuse.Tests.csproj --filter FullyQualifiedName~ScrumPoker -p:BuildSpa=false -m:1
```

When fixing SP-002 or SP-003, remove its skip/expected-failure marker and make its existing assertion pass without weakening it. Browser traces, screenshots and the HTML report are generated under the usual ignored Playwright output directories.
