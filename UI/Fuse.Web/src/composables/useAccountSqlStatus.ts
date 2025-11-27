import { computed, toValue, type MaybeRef } from 'vue'
import { useQuery } from '@tanstack/vue-query'
import { useFuseClient } from './useFuseClient'

export function useAccountSqlStatus(accountId: MaybeRef<string | null | undefined>) {
  const client = useFuseClient()

  const query = useQuery({
    queryKey: computed(() => ['account-sql-status', toValue(accountId)]),
    queryFn: () => client.sqlStatus(toValue(accountId)!),
    enabled: computed(() => !!toValue(accountId)),
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
