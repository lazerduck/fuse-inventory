import { useQuery } from '@tanstack/vue-query'
import { useFuseClient } from './useFuseClient'
import type { IPermissionAreaCatalog } from 'api/client'

export function usePermissionCatalog() {
  const client = useFuseClient()

  return useQuery({
    queryKey: ['security-permission-catalog'],
    queryFn: async (): Promise<IPermissionAreaCatalog[]> => {
      const catalog = await client.permissionsCatalog()
      return catalog
        .map((entry) => ({
          areaName: entry.areaName ?? 'Other',
          permissions: [...new Set(entry.permissions ?? [])].sort((a, b) => a.localeCompare(b))
        }))
        .sort((a, b) => (a.areaName ?? '').localeCompare(b.areaName ?? ''))
    }
  })
}
