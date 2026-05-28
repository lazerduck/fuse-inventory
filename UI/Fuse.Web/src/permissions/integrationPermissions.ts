export const IntegrationPermissions = {
  KumaIntegrationsCreate: 'kumaintegrations:create',
  KumaIntegrationsDelete: 'kumaintegrations:delete',
  SqlConnectionsCreate: 'sqlintegrations:create',
  SqlConnectionsDelete: 'sqlintegrations:delete',
  SqlGrantsApply: 'sqlintegrations:grants:apply',
  AzureKeyVaultConnectionsCreate: 'secretproviders:create',
  AzureKeyVaultConnectionsDelete: 'secretproviders:delete',
  AzureKeyVaultSecretsCreate: 'secretproviders:secrets:create',
  AzureKeyVaultSecretsRotate: 'secretproviders:secrets:rotate',
  AzureKeyVaultSecretsReveal: 'secretproviders:secrets:reveal',
  AppConfigCreate: 'secretproviders:appconfig:create',
  AppConfigUpdate: 'secretproviders:appconfig:update',
} as const
