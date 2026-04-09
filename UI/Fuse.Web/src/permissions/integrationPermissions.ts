export const IntegrationPermissions = {
  KumaIntegrationsCreate: 'kumaintegrations:create',
  KumaIntegrationsDelete: 'kumaintegrations:delete',
  SqlConnectionsCreate: 'sqlintegrations:create',
  SqlConnectionsDelete: 'sqlintegrations:delete',
  SqlGrantsApply: 'sqlintegrations:grants:apply',
  AzureKeyVaultConnectionsCreate: 'secretproviders:create',
  AzureKeyVaultConnectionsDelete: 'secretproviders:delete',
} as const
