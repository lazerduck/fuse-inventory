import { useQuery } from '@tanstack/vue-query'
import { useFuseClient } from './useFuseClient'

export function useDataStores() {
  const client = useFuseClient()

  return useQuery({
    queryKey: ['dataStores'],
    queryFn: () => client.dataStoreAll()
  })
}
