import { computed } from 'vue'
import { useQuery } from '@tanstack/vue-query'
import { useFuseClient } from './useFuseClient'
import { useFuseStore } from '../stores/FuseStore'
import { Permission } from 'permissions'

export function useEntraStatus() {
  const client = useFuseClient()

  return useQuery({
    queryKey: ['entraStatus'],
    queryFn: () => client.entraStatus()
  })
}

export function useEntraSettings() {
  const client = useFuseClient()
  const fuseStore = useFuseStore()

  return useQuery({
    queryKey: computed(() => ['entraSettings', fuseStore.currentUser?.id]),
    queryFn: () => client.entraSettings(),
    enabled: computed(() => fuseStore.hasPermission(Permission.SecuritySettingsUpdate))
  })
}
