import { computed } from 'vue'
import { useQuery } from '@tanstack/vue-query'
import { useFuseClient } from './useFuseClient'

export function usePlatforms() {
  const client = useFuseClient()

  const query = useQuery({
    queryKey: ['platforms'],
    queryFn: () => client.platformAll()
  })

  const options = computed(() =>
    (query.data.value ?? [])
      .filter((platform) => !!platform.id)
      .map((platform) => ({
        label: platform.displayName ?? platform.dnsName ?? platform.id!,
        value: platform.id!
      }))
  )

  const lookup = computed<Record<string, string>>(() => {
    const map: Record<string, string> = {}
    for (const platform of query.data.value ?? []) {
      if (platform.id) {
        map[platform.id] = platform.displayName ?? platform.dnsName ?? platform.id
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
