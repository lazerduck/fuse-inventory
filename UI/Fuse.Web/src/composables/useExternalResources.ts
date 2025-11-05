import { useQuery } from '@tanstack/vue-query'
import { useFuseClient } from './useFuseClient'

export function useExternalResources() {
  const client = useFuseClient()

  return useQuery({
    queryKey: ['externalResources'],
    queryFn: () => client.externalResourceAll()
  })
}
