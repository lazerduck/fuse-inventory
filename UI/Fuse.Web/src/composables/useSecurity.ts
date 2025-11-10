import { useQuery } from '@tanstack/vue-query'
import { useFuseClient } from './useFuseClient'

export function useSecurities() {
  const client = useFuseClient()

  return useQuery({
    queryKey: ['securityUsers'],
    queryFn: () => client.users()
  })
}
