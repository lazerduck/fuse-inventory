import { useQuery } from '@tanstack/vue-query'
import { useFuseClient } from './useFuseClient'

export function useMessageBrokers() {
  const client = useFuseClient()

  return useQuery({
    queryKey: ['messageBrokers'],
    queryFn: () => client.messageBrokerAll()
  })
}
