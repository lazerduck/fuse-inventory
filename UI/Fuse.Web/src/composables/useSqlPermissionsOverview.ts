import { computed, toValue, type MaybeRef } from 'vue'
import { useQuery } from '@tanstack/vue-query'
import { Notify } from 'quasar'
import { useFuseClient } from './useFuseClient'

export function useSqlPermissionsOverview(sqlIntegrationId: MaybeRef<string | null | undefined>) {
  const client = useFuseClient()
  const integrationId = computed(() => toValue(sqlIntegrationId))

  const query = useQuery({
    queryKey: computed(() => ['sql-permissions-overview', integrationId.value]),
    queryFn: () => client.permissionsOverview(integrationId.value!),
    enabled: computed(() => !!integrationId.value),
    retry: false,
    staleTime: 30000 // Cache for 30 seconds
  })

  async function refresh() {
    if (!integrationId.value) {
      return query.refetch()
    }

    try {
      const refreshed = await client.refreshPOST(integrationId.value)
      query.data.value = refreshed
      Notify.create({ type: 'positive', message: 'SQL permissions overview refreshed' })
      return refreshed
    } catch (error) {
      Notify.create({ type: 'negative', message: 'Failed to refresh SQL permissions overview' })
      throw error
    }
  }

  return {
    data: query.data,
    isLoading: query.isLoading,
    isFetching: query.isFetching,
    error: query.error,
    refetch: query.refetch,
    refresh
  }
}
