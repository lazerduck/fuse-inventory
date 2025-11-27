import { computed, toValue, type MaybeRef } from 'vue'
import { useQuery } from '@tanstack/vue-query'
import { useFuseClient } from './useFuseClient'

export function useSqlPermissionsOverview(sqlIntegrationId: MaybeRef<string | null | undefined>) {
  const client = useFuseClient()

  const query = useQuery({
    queryKey: computed(() => ['sql-permissions-overview', toValue(sqlIntegrationId)]),
    queryFn: () => client.permissionsOverview(toValue(sqlIntegrationId)!),
    enabled: computed(() => !!toValue(sqlIntegrationId)),
    retry: false,
    staleTime: 30000 // Cache for 30 seconds
  })

  return {
    data: query.data,
    isLoading: query.isLoading,
    isFetching: query.isFetching,
    error: query.error,
    refetch: query.refetch
  }
}
