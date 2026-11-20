import { computed, ref, toValue, type MaybeRef } from 'vue'
import { useQuery, useQueryClient } from '@tanstack/vue-query'
import { Notify } from 'quasar'
import { useFuseClient } from './useFuseClient'

export function useSqlPermissionsOverview(sqlIntegrationId: MaybeRef<string | null | undefined>) {
  const client = useFuseClient()
  const queryClient = useQueryClient()
  const refreshing = ref(false)
  const integrationId = computed(() => toValue(sqlIntegrationId))

  const query = useQuery({
    queryKey: computed(() => ['sql-permissions-overview', integrationId.value]),
    queryFn: () => client.permissionsOverview(integrationId.value!),
    enabled: computed(() => !!integrationId.value),
    retry: false,
    staleTime: 30000 // Cache for 30 seconds
  })

  async function refresh() {
    const id = integrationId.value
    if (!id) {
      return query.refetch()
    }

    refreshing.value = true
    try {
      const refreshed = await client.refreshPOST(id)
      queryClient.setQueryData(['sql-permissions-overview', id], refreshed)
      Notify.create({ type: 'positive', message: 'SQL permissions overview refreshed' })
      return refreshed
    } catch {
      Notify.create({ type: 'negative', message: 'Failed to refresh SQL permissions overview' })
    } finally {
      refreshing.value = false
    }
  }

  return {
    data: query.data,
    isLoading: query.isLoading,
    isFetching: computed(() => query.isFetching.value || refreshing.value),
    error: query.error,
    refetch: query.refetch,
    refresh
  }
}
