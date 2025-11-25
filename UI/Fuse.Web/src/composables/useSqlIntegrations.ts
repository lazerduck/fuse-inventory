import { useQuery } from '@tanstack/vue-query'
import { useFuseClient } from './useFuseClient'

export function useSqlIntegrations() {
  const client = useFuseClient()
  return useQuery({
    queryKey: ['sqlIntegrations'],
    queryFn: () => client.sqlIntegrationAll()
  })
}
