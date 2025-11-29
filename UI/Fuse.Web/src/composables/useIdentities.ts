import { useQuery } from '@tanstack/vue-query'
import { useFuseClient } from './useFuseClient'

export function useIdentities() {
  const client = useFuseClient()

  return useQuery({
    queryKey: ['identities'],
    queryFn: () => client.identityAll()
  })
}
