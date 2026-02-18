import { useQuery } from '@tanstack/vue-query'
import { useFuseClient } from './useFuseClient'

export function useRoles() {
  const client = useFuseClient()

  return useQuery({
    queryKey: ['roles'],
    queryFn: () => client.roleAll()
  })
}
