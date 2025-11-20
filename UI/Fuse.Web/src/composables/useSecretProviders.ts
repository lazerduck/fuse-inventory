import { useQuery } from '@tanstack/vue-query'
import { useFuseClient } from './useFuseClient'

export function useSecretProviders() {
  const client = useFuseClient()

  return useQuery({
    queryKey: ['secretProviders'],
    queryFn: () => client.secretProviderAll()
  })
}
