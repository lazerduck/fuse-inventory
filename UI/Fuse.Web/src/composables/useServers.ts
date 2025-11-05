import { computed } from 'vue'
import { useQuery } from '@tanstack/vue-query'
import { useFuseClient } from './useFuseClient'

export function useServers() {
  const client = useFuseClient()

  const query = useQuery({
    queryKey: ['servers'],
    queryFn: () => client.serverAll()
  })

  const options = computed(() =>
    (query.data.value ?? [])
      .filter((server) => !!server.id)
      .map((server) => ({
        label: server.name ?? server.hostname ?? server.id!,
        value: server.id!
      }))
  )

  const lookup = computed<Record<string, string>>(() => {
    const map: Record<string, string> = {}
    for (const server of query.data.value ?? []) {
      if (server.id) {
        map[server.id] = server.name ?? server.hostname ?? server.id
      }
    }
    return map
  })

  return {
    ...query,
    options,
    lookup
  }
}
