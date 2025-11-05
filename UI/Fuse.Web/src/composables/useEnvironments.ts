import { computed } from 'vue'
import { useQuery } from '@tanstack/vue-query'
import { useFuseClient } from './useFuseClient'

export function useEnvironments() {
  const client = useFuseClient()

  const query = useQuery({
    queryKey: ['environments'],
    queryFn: () => client.environmentAll()
  })

  const options = computed(() =>
    (query.data.value ?? [])
      .filter((env) => !!env.id)
      .map((env) => ({ label: env.name ?? env.id!, value: env.id! }))
  )

  const lookup = computed<Record<string, string>>(() => {
    const map: Record<string, string> = {}
    for (const env of query.data.value ?? []) {
      if (env.id) {
        map[env.id] = env.name ?? env.id
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
