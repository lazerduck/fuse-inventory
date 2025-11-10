import { useQuery } from '@tanstack/vue-query'
import { useFuseClient } from './useFuseClient'
import { useFuseStore } from '../stores/FuseStore'
import { computed } from 'vue'

export function useSecurities() {
  const client = useFuseClient()
  const fuseStore = useFuseStore()
  
  // Make the query key depend on the current user so it refetches when login state changes
  const queryKey = computed(() => ['securityUsers', fuseStore.currentUser?.id])

  return useQuery({
    queryKey,
    queryFn: () => client.accountsAll(),
    // Only fetch if user is logged in and is an admin
    enabled: computed(() => fuseStore.isLoggedIn)
  })
}
