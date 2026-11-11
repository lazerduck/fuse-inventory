# Microsoft Entra sign-in and role membership

Fuse uses Microsoft Entra to authenticate people and obtain security-group IDs. Permissions remain in Fuse roles. A role can be granted directly in Fuse, through one or more Entra groups, or both. The account screen shows each source and its group labels. Removing one source leaves the others intact.

## Configure Entra

1. Create a local Fuse administrator first and retain its password for recovery.
2. In Entra **App registrations**, register a single-tenant application. Add a **Web** redirect URI of `https://YOUR-FUSE-HOST/auth/entra/callback`. For local development use `http://localhost:5173/auth/entra/callback`.
3. Create a client secret. Copy its **value**, and arrange rotation before its expiry.
4. Under **Token configuration → Add groups claim**, select **Security groups**, with **Group ID** for ID tokens. Leave **Emit groups as role claims** unchecked. Do not use display names as identity keys.
5. In Fuse **Security → Microsoft Entra**, enter the tenant ID, application/client ID, secret value and public origin. Set the sign-in lifetime (5–120 minutes; default 60), enable Microsoft sign-in and save. The public origin must match the browser-facing host; HTTPS is required except for loopback development.
6. Use **Test Microsoft sign-in**. Consent is governed by your tenant's policies. Fuse requests `openid profile`; it does not require directory-wide Graph access or retain Microsoft access/refresh tokens.
7. In Fuse **Roles**, edit or create a role and add the Entra group's **object ID** and a descriptive label. Members inherit that role when signing in with Microsoft. Direct Fuse assignments remain available in **Security → User Accounts**.

This works with Entra Free by consuming security-group claims. Group assignment to an enterprise application is a separate premium feature and is not required here. See [Microsoft's group-claims and app-roles guidance](https://learn.microsoft.com/en-us/security/zero-trust/develop/configure-tokens-group-claims-app-roles).

Enabling Entra sets Fuse to Fully Restricted. Disable Entra before selecting an anonymous-access posture. Saving connection settings ends existing Entra sessions, including when rotating the secret.

## Account association

First Microsoft sign-in creates a passwordless, non-administrator Fuse account identified by the immutable tenant ID and object ID. Matching email addresses or display names never links accounts automatically. Entra directory administrators do not automatically become Fuse administrators.

To associate an existing local account, sign into Fuse with its password, choose **Link Microsoft account**, confirm the local password, then complete Microsoft sign-in. An identity already attached to another Fuse account cannot be claimed. Local password sign-in continues to work, but only a Microsoft-authenticated session receives Entra-derived roles.

An account without a direct or mapped role can sign in and see its account status, but cannot access protected inventory. Assign its role in Fuse or add it to a mapped Entra group.

## Membership and revocation

| Change | Effect |
| --- | --- |
| Remove a direct role | Takes effect on the next request; group-derived grants remain. |
| Remove a group mapping from a Fuse role | Takes effect on the next request; direct grants and other mappings remain. |
| Remove a person from an Entra group | Reflected on their next Microsoft sign-in, subject to Entra propagation. Existing Fuse sessions retain their verified group snapshot until expiry or revocation. |
| Force sign-in refresh | Immediately removes all that user's Fuse sessions and increments their session version. Their next Microsoft sign-in obtains fresh group claims. |
| Disable a Fuse user | Ends sessions and blocks password sign-in, Microsoft sign-in and their API keys. Re-enabling allows fresh sign-in and restores API-key usability. |
| Change the Entra connection | Ends all Entra sessions and invalidates in-flight callbacks for the previous configuration. |

Entra sessions expire at the earlier of the configured lifetime and the verified ID token's expiry. The ordinary Fuse session-refresh endpoint cannot extend them. A new Microsoft sign-in is required; Microsoft SSO may complete it without asking for a password again. There is no background directory polling or Microsoft Continuous Access Evaluation integration.

API keys retain their explicit Fuse role assignments. Force-refresh and Entra group removal do not revoke those separate credentials. Disable the Fuse user to block all their credentials, or revoke individual API keys.

User-account lists show the most recently verified group membership and verification/expiry timestamps in the API. This is a cached administrative view, not a real-time Entra directory lookup. Authorization uses the requesting session's validated groups, never that cached user summary.

## Operation and limits

- The backend uses the standard ASP.NET Core OpenID Connect handler with authorization code, PKCE, state, nonce, and issuer/audience/signature/lifetime validation. Only the configured tenant is accepted.
- Fuse exchanges a short-lived, single-use HttpOnly cookie for its own session token. Microsoft tokens and Fuse session tokens are not placed in redirect URLs. Fuse's existing browser session storage mechanism remains in use.
- The client secret is protected with ASP.NET Data Protection. Persist and restrict access to the entire `FUSE_DATA_DIR`, including `protection-keys`; back it up together with the configuration. The colocated keys do not protect against someone who can read the whole data directory. After losing the keys, re-enter the client secret.
- Login/link handoff tickets are held in process memory. Restarting during sign-in requires retrying. Multiple replicas need shared ticket storage or session affinity, as well as shared Data Protection keys; the current implementation targets Fuse's single-instance deployment.
- Entra omits group lists beyond its token group limit (200 for JWT). Fuse rejects group-overage sign-ins rather than retaining stale permissions or silently dropping groups. Directory resolution through Graph is not implemented. See [Microsoft's group-claim configuration and limits](https://learn.microsoft.com/en-us/entra/identity/hybrid/connect/how-to-connect-fed-group-claims).
- The last enabled local administrator cannot be deleted or disabled through account management. Keep a recovery password independently of Microsoft sign-in.

## Validation on 11 November 2026

After the style and architecture refactor, final checks passed: **855 backend tests** including Docker-backed SQL integrations, **142 frontend tests** across 23 files, frontend test typechecking, and the production UI build. The backend build reported existing nullable warnings; Vite reported its bundle-size advisory. `git diff --check` passed.

```sh
dotnet test API/Fuse.Tests/Fuse.Tests.csproj -m:1 --no-restore --verbosity minimal
cd UI/Fuse.Web
npm test -- --reporter=dot
npm run test:typecheck
npm run build
```

The implementation was exercised against an actual Entra Free tenant with account-only Microsoft consent, using a dedicated test app registration and security group.

Live checks confirmed:

- Real Microsoft sign-in creates a non-admin account with the mapped role and permits an authorized API request.
- Adding a direct grant displays **Direct + Via Entra**; removing it preserves inherited access.
- Removing Entra group membership, revoking Fuse sessions and signing in again removes the inherited source while preserving a direct grant.
- Removing the final direct grant produces HTTP 403 for protected inventory.
- Revoking sessions produces HTTP 401 for old tokens.
- Disabling the Fuse account blocks a fresh Microsoft sign-in.
- The recovery administrator can still sign in locally.
- Test group membership was restored, the Fuse test account re-enabled, and a fresh Microsoft sign-in confirmed inherited-only access again.

Account-linking collision/proof checks, group-overage handling, persistence/reload, settings validation, API-key blocking and the OIDC event lifecycle are also covered by automated tests. The live exercise did not simulate an overage-sized directory or migrate an existing local account.

The refactor was rechecked against the same real tenant: the settings form saved through the generated client and Core settings service, Microsoft sign-in obtained inherited permissions, adding/removing a direct grant preserved the inherited source, and session revocation rejected the old token. A fresh Microsoft sign-in restored inherited-only access. Automated regressions additionally cover the configured API base URL, shared 401 handling, typed validation messages, and the custom handoff header.

## Maintaining the API clients

Entra management requests use the generated TypeScript client through `useFuseClient`, with shared query composables and TanStack mutations. Core settings and identity services expose interfaces; request and response contracts live in the normal Core folders. The HTTP scenarios are independent tests and exercise the generated C# client as well as raw HTTP boundary checks.

Restore the pinned generator from the repository root with `dotnet tool restore`. With the API running on its default development port (5071), run `npm run generate:api` in `UI/Fuse.Web`. This regenerates both complete clients and formats the TypeScript output with the pinned formatter. For a different API port, run from the repository root:

```sh
dotnet tool run nswag run nswag.json /variables:ApiBaseUrl=http://localhost:5072
./UI/Fuse.Web/node_modules/.bin/prettier --write UI/Fuse.Web/src/api/client.ts
```

Do not splice generated DTOs or methods by hand. Stable operation IDs on the Scrum Poker endpoints prevent their names from colliding with security and secret-provider operations during regeneration.
