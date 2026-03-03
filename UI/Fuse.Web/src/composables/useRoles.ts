import { useQuery } from '@tanstack/vue-query'
import { useFuseClient } from './useFuseClient'
import { useFuseStore } from '../stores/FuseStore'
import { Permission, type RoleInfo } from '../api/client'

export function useRoles() {
  const client = useFuseClient()
  const fuseStore = useFuseStore()

  return useQuery({
    queryKey: ['roles', fuseStore.currentUser?.id, fuseStore.currentUser?.roleIds ?? [], fuseStore.hasPermission(Permission.RolesRead)],
    queryFn: async (): Promise<RoleInfo[]> => {
      if (fuseStore.hasPermission(Permission.RolesRead)) {
        return client.roleAll()
      }

      const roleIds = [...new Set(fuseStore.currentUser?.roleIds ?? [])]
      if (roleIds.length === 0) {
        return []
      }

      const roleResults = await Promise.allSettled(roleIds.map((id) => client.roleGET(id)))
      return roleResults
        .filter((result): result is PromiseFulfilledResult<RoleInfo> => result.status === 'fulfilled')
        .map((result) => result.value)
    }
  })
}
