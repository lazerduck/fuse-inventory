import { useQuery } from '@tanstack/vue-query'
import { useFuseClient } from './useFuseClient'

export function useKumaIntegrations() {
  const client = useFuseClient()
  return useQuery({
    queryKey: ['kumaIntegrations'],
    queryFn: () => client.kumaIntegrationAll()
  })
}
