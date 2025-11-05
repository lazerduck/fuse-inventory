import { useQuery } from '@tanstack/vue-query'
import { useFuseClient } from './useFuseClient'

export function useApplications() {
  const client = useFuseClient()

  return useQuery({
    queryKey: ['applications'],
    queryFn: () => client.applicationAll()
  })
}
