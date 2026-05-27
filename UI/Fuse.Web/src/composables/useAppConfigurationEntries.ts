import { computed, type Ref } from 'vue'
import { useQuery } from '@tanstack/vue-query'
import { useAuthToken } from './useAuthToken'

export interface AppConfigurationEntry {
  key?: string
  value?: string | null
  label?: string | null
  contentType?: string | null
  lastModified?: string | null
  isLocked?: boolean
  isKeyVaultReference?: boolean
  keyVaultReferenceUri?: string | null
}

interface Filters {
  keySearch: Ref<string>
  keyPrefix: Ref<string>
  label: Ref<string>
}

export function useAppConfigurationEntries(providerId: Ref<string | null | undefined>, filters: Filters) {
  const { getToken, clearToken } = useAuthToken()
  const baseUrl = import.meta.env.VITE_API_BASE_URL ?? ''

  return useQuery({
    queryKey: computed(() => [
      'app-configuration-entries',
      providerId.value,
      filters.keySearch.value,
      filters.keyPrefix.value,
      filters.label.value
    ]),
    queryFn: async () => {
      const id = providerId.value
      if (!id) return []

      const params = new URLSearchParams()
      if (filters.keySearch.value.trim()) params.set('keySearch', filters.keySearch.value.trim())
      if (filters.keyPrefix.value.trim()) params.set('keyPrefix', filters.keyPrefix.value.trim())
      if (filters.label.value.trim()) params.set('label', filters.label.value.trim())

      const querySuffix = params.toString() ? `?${params.toString()}` : ''
      const url = `${baseUrl}/api/SecretProvider/${encodeURIComponent(id)}/app-configuration${querySuffix}`
      const headers = new Headers()
      const token = getToken()
      if (token) headers.set('Authorization', `Bearer ${token}`)

      const response = await fetch(url, { headers })

      if (response.status === 401) {
        clearToken()
        window.dispatchEvent(new Event('fuse-auth-invalid'))
      }

      if (!response.ok) {
        const payload = await response.json().catch(() => null)
        throw new Error(payload?.error ?? `Failed to load App Configuration entries (${response.status})`)
      }

      return response.json() as Promise<AppConfigurationEntry[]>
    },
    enabled: computed(() => !!providerId.value),
    staleTime: 15_000
  })
}
