import { computed } from 'vue'
import { useQuery } from '@tanstack/vue-query'
import { useFuseClient } from './useFuseClient'

export function usePositions() {
  const client = useFuseClient()

  const query = useQuery({
    queryKey: ['positions'],
    queryFn: () => client.positionAll()
  })

  const lookup = computed<Record<string, string>>(() => {
    const map: Record<string, string> = {}
    for (const position of query.data.value ?? []) {
      if (position.id) {
        map[position.id] = position.name ?? position.id
      }
    }
    return map
  })

  const options = computed(() =>
    (query.data.value ?? [])
      .filter((position) => !!position.id)
      .map((position) => ({ label: position.name ?? position.id!, value: position.id! }))
  )

  return {
    ...query,
    lookup,
    options
  }
}
