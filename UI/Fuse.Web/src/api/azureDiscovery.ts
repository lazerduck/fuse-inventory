import { SecretProviderAuthMode } from './client'
import { useAuthToken } from '../composables/useAuthToken'

export type AzureIntegrationKind = 'KeyVault' | 'AppConfiguration'

export interface AzureProfile {
  id: string; name: string; resourceId: string; tenantId: string; clientId: string; directoryLookup: boolean
}
export interface AzureSubscription { id: string; name: string }
export interface DiscoveredAzureResource {
  resourceId: string
  name: string
  kind: AzureIntegrationKind | 'ServiceBus' | 'CosmosDb'
  endpoint: string
  subscriptionId: string
  resourceGroup: string
  location: string
  alreadyAdded: boolean
  ignored: boolean
}
export interface AzureDiscoveryScope {
  authMode: SecretProviderAuthMode
  subscriptionIds: string[]
  resourceGroups: string[]
  kinds: (AzureIntegrationKind | 'ServiceBus' | 'CosmosDb')[]
}
export interface AzureImportResult {
  resourceId: string
  name: string
  status: 'Added' | 'AlreadyAdded' | 'Failed'
  error?: string
}
export interface AzureAccessCheck { success: boolean; checkedAt: string; message: string }

// Match the authenticated fetch behavior used by the generated API client.
export async function azureRequest<T>(path: string, body?: unknown, method = 'POST', signal?: AbortSignal): Promise<T> {
  const { getToken, clearToken } = useAuthToken()
  const headers = new Headers({ 'Content-Type': 'application/json' })
  const token = getToken()
  if (token) headers.set('Authorization', `Bearer ${token}`)
  const response = await window.fetch(`${import.meta.env.VITE_API_BASE_URL ?? ''}${path}`, {
    method, headers, signal, body: body === undefined ? undefined : JSON.stringify(body)
  })
  if (response.status === 401) {
    clearToken()
    window.dispatchEvent(new Event('fuse-auth-invalid'))
  }
  const payload = await response.json().catch(() => undefined)
  if (!response.ok) throw payload ?? new Error(`Request failed with status ${response.status}`)
  return payload as T
}

export function listAzureSubscriptions(authMode: SecretProviderAuthMode, signal?: AbortSignal) {
  return azureRequest<{ subscriptions: AzureSubscription[]; warnings: string[] }>('/api/azure-discovery/subscriptions', { authMode }, 'POST', signal)
}
export function discoverAzureResources(scope: AzureDiscoveryScope, signal?: AbortSignal) {
  return azureRequest<{ resources: DiscoveredAzureResource[]; warnings: string[] }>('/api/azure-discovery/resources', { ...scope, includeIgnored: true }, 'POST', signal)
}
export function setAzureDiscoveryExclusions(resourceIds: string[], excluded: boolean) {
  return azureRequest<{ resourceIds: string[] }>('/api/azure-discovery/exclusions', { resourceIds, excluded }, 'PUT')
}
export function importAzureResources(scope: AzureDiscoveryScope, resourceIds: string[]) {
  return azureRequest<{ results: AzureImportResult[]; warnings: string[] }>('/api/azure-discovery/import', { ...scope, resourceIds })
}
export function testSavedIntegration(id: string) {
  return azureRequest<AzureAccessCheck>(`/api/SecretProvider/${encodeURIComponent(id)}/test-connection`)
}
