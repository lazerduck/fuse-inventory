# Azure secret reference health

In **Azure integrations**, open a Key Vault or App Configuration explorer and choose **Version expiry and applications** on a secret or Key Vault reference. The same button is available beside a saved API key or a visible App Configuration reference on an application instance.

The view lists observed Key Vault versions, enabled state and expiry dates. The application list identifies pinned versions, references that follow the latest version, instance API keys and dependency accounts that use the secret. For an App Configuration entry, it also lists instances associated with that store whose documented key filter matches the selected key. These associations describe inventory and configuration, without making an assertion about which configuration paths an application exercised at runtime.

Fuse uses Key Vault's version metadata operation. The vault integration requires the **Check** capability and Azure metadata access. Operators need `secretproviders:read`. Looking up an App Configuration reference also requires `secretproviders:appconfig:read` and a configured vault that matches the reference URI. The application and dependency-account results require their respective inventory read permissions. Hidden versions and restricted listings retain their visibility status.

[Key Vault secret versions](https://learn.microsoft.com/en-us/rest/api/keyvault/secrets/get-secret-versions/get-secret-versions) · [App Configuration Key Vault references](https://learn.microsoft.com/en-us/azure/azure-app-configuration/use-key-vault-references-dotnet-core)
