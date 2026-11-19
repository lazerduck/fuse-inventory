# Connector passwords in Azure Key Vault

A saved Redis, RabbitMQ, PostgreSQL or MongoDB integration can use a secret from a Fuse Key Vault provider. Open the integration list, choose **Key Vault password** beside the saved connection and review the destination and username. Select the vault, enter the secret name and optionally pin a version. Saving verifies the vault can read the secret and removes the previously stored password. **Test connection** verifies the resulting login at the service.

Leaving the version blank follows rotation. Each saved connection test, discovery, index inspection, integration review and PostgreSQL grant repair resolves its credential when it runs. A Redis node comparison resolves each node's configured reference. A failed lookup stops the operation before it connects. Fuse stores the reference and audits its use. The API and audit log omit the password value.

Binding and detaching require an authenticated administrator or all three explicit grants:

- `secretproviders:secrets:bindintegration`
- `secretproviders:secrets:reveal`
- The connector's update permission, for example `redisintegrations:update`

The registered Key Vault provider needs the **Read** capability and permission in Azure to read the selected secret. **Check** enables secret-name suggestions where available. After binding, users can run saved operations with their usual connector or review permissions.

The reference is bound to the integration, inventory target, endpoint and username. It also covers MongoDB's authentication database and TLS setting and RabbitMQ's virtual host. To move a connection, detach its reference, edit the destination and bind again. Detaching clears the credential. The integration's existing password field can then accept a new literal password.

For automation, GET `/api/integration-credentials/{provider}/{id}` to obtain the revision and destination. PUT to the same path with `expectedRevision` and `reference: {providerId, secretName, version}`. Use `reference: null` to detach. A concurrent change causes HTTP 409 and requires a fresh GET.
