import { useMutation, useQuery, useQueryClient } from '@tanstack/vue-query'
import { useAuthToken } from './useAuthToken'

export interface AzureIntegrationManagerResponse {
  hasClientSecretCredentials: boolean
  tenantId?: string | null
  clientId?: string | null
  updatedAt?: string | null
}

interface UpdateAzureIntegrationManagerCommand {
  credentials: {
    tenantId: string
    clientId: string
    clientSecret: string
  }
}

function getBaseUrl() {
  return import.meta.env.VITE_API_BASE_URL ?? ''
}

async function request<T>(url: string, init?: RequestInit): Promise<T> {
  const { getToken, clearToken } = useAuthToken()
  const token = getToken()

  const headers = new Headers(init?.headers ?? {})
  headers.set('Content-Type', 'application/json')

  if (token) {
    headers.set('Authorization', `Bearer ${token}`)
  }

  const response = await window.fetch(url, {
    ...init,
    headers
  })

  if (response.status === 401) {
    clearToken()
    window.dispatchEvent(new Event('fuse-auth-invalid'))
  }

  if (!response.ok) {
    let payload: unknown = undefined
    try {
      payload = await response.json()
    } catch {
      // ignore malformed JSON payloads
    }

    throw payload ?? new Error(`Request failed with status ${response.status}`)
  }

  return response.json() as Promise<T>
}

async function getAzureIntegrationManager(): Promise<AzureIntegrationManagerResponse> {
  return request<AzureIntegrationManagerResponse>(`${getBaseUrl()}/api/SecretProvider/azure-manager`)
}

async function updateAzureIntegrationManager(
  command: UpdateAzureIntegrationManagerCommand
): Promise<AzureIntegrationManagerResponse> {
  return request<AzureIntegrationManagerResponse>(`${getBaseUrl()}/api/SecretProvider/azure-manager`, {
    method: 'PUT',
    body: JSON.stringify(command)
  })
}

export function useAzureIntegrationManager() {
  const queryClient = useQueryClient()

  const query = useQuery({
    queryKey: ['azureIntegrationManager'],
    queryFn: getAzureIntegrationManager
  })

  const updateMutation = useMutation({
    mutationFn: updateAzureIntegrationManager,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['azureIntegrationManager'] })
    }
  })

  return {
    data: query.data,
    isLoading: query.isLoading,
    error: query.error,
    refetch: query.refetch,
    updateMutation
  }
}
