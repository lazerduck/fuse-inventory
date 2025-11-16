import { computed } from 'vue'
import { useQuery } from '@tanstack/vue-query'
import { useFuseClient } from './useFuseClient'

export function useAccounts() {
  const client = useFuseClient()

  const query = useQuery({
    queryKey: ['accounts'],
    queryFn: () => client.accountAll()
  })

  const lookup = computed<Record<string, string>>(() => {
    const map: Record<string, string> = {}
    for (const acc of query.data.value ?? []) {
      if (acc.id) {
        map[acc.id] = acc.targetId ?? acc.id
      }
    }
    return map
  })

  return {
    ...query,
    lookup
  }
}
