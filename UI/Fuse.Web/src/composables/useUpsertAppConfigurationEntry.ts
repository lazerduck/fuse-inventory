import { useMutation, useQueryClient } from '@tanstack/vue-query'
import { useAuthToken } from './useAuthToken'
import type { AppConfigurationEntry } from './useAppConfigurationEntries'

export interface SetAppConfigurationValueRequest {
  providerId: string
  key: string
  label?: string | null
  value: string
  contentType?: string | null
}

export function useUpsertAppConfigurationEntry() {
  const { getToken, clearToken } = useAuthToken()
  const queryClient = useQueryClient()
  const baseUrl = import.meta.env.VITE_API_BASE_URL ?? ''

  return useMutation({
    mutationFn: async (request: SetAppConfigurationValueRequest): Promise<AppConfigurationEntry> => {
      const { providerId, ...body } = request
      const url = `${baseUrl}/api/SecretProvider/${encodeURIComponent(providerId)}/app-configuration`
      const headers = new Headers({ 'Content-Type': 'application/json' })
      const token = getToken()
      if (token) headers.set('Authorization', 'Bearer ' + token)

      const response = await fetch(url, {
        method: 'PUT',
        headers,
        body: JSON.stringify(body)
      })

      if (response.status === 401) {
        clearToken()
        window.dispatchEvent(new Event('fuse-auth-invalid'))
      }

      if (!response.ok) {
        const payload = await response.json().catch(() => null)
        throw new Error(payload?.error ?? `Failed to save App Configuration entry (${response.status})`)
      }

      return response.json() as Promise<AppConfigurationEntry>
    },
    onSuccess: (_data, variables) => {
      queryClient.invalidateQueries({ queryKey: ['app-configuration-entries', variables.providerId] })
    }
  })
}
