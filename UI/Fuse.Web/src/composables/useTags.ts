import { computed } from 'vue'
import { useQuery } from '@tanstack/vue-query'
import { useFuseClient } from './useFuseClient'

export function useTags() {
  const client = useFuseClient()

  const query = useQuery({
    queryKey: ['tags'],
    queryFn: () => client.tagAll()
  })

  const options = computed(() =>
    (query.data.value ?? [])
      .filter((tag) => !!tag.id)
      .map((tag) => ({ label: tag.name ?? tag.id!, value: tag.id! }))
  )

  const lookup = computed<Record<string, string>>(() => {
    const map: Record<string, string> = {}
    for (const tag of query.data.value ?? []) {
      if (tag.id) {
        map[tag.id] = tag.name ?? tag.id
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
