# Read-only Keycloak integration

Under **Integrations → Keycloak**, add the realm issuer, the inspection client ID and its secret. The issuer is the URL Keycloak uses in the token’s `iss` claim, for example `https://sso.example.org/auth/realms/inventory`. Fuse must be able to reach that address. Use HTTPS for deployed realms. Local development realms can use HTTP. The integration uses client credentials and supports Keycloak 26.x Admin REST endpoints.

Create a confidential OpenID Connect client with service accounts enabled in each realm you want to inspect. The lab inspector has the realm-management roles `view-clients`, `view-users`, `view-realm`, `query-users` and `query-groups`. A `view-clients`-only inspector can list clients while service-account role inspection is denied. Ensure the roles are also in the client's token scope. On versions requiring an Admin API audience, assign the built-in `kc-admin-api` client scope. Saving a connection is offline. Fuse protects the secret using the server’s persisted data-protection keys. Supply a fresh secret when changing the issuer or inspection client ID.

**Test connection** obtains a token and attempts one client-list read. **Inspect** reads client IDs, UUIDs, enablement, service accounts and their role mappings. Expand a client to see its direct and effective roles. Effective roles include composite grants and are qualified by the owning realm or client. Tokens issued to applications can contain a narrower set of roles due to scope mappings. Ordinary realm users, their credentials and active sessions are outside this inventory.

## Map an account

On a Fuse account with **OAuthClient** authentication, add two entries under **Parameters**.

| Parameter | Example |
| --- | --- |
| `keycloakIssuer` | `https://sso.example.org/auth/realms/inventory` |
| `keycloakClientId` | `inventory-api` |

Fuse compares the issuer and exact case-sensitive client ID. An application or application-instance target links the match to its owning application. Other realms and accounts with no declared issuer are excluded. A match confirms the registered client identity. Its client enablement, availability of a service account, and visibility of that account’s roles are reported separately.

## Visibility

An inspector with realm-wide client visibility can distinguish missing and disabled clients. Filtered lists, denied requests, failed authentication and interrupted pagination leave unseen clients unknown. A client may be visible while reading its service-account roles is denied. Roles shown with incomplete status are the grants Fuse could observe. Discovery has a 30-second deadline, up to 1,000 clients and a 500-read budget per inspection. Fuse issues the token request and uses GET for all admin operations.

The server endpoints are documented in the [Keycloak Admin REST API](https://www.keycloak.org/docs-api/latest/rest-api/index.html).
