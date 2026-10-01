import { computed, toValue, type MaybeRefOrGetter } from 'vue'
import { useQuery } from '@tanstack/vue-query'
import { useFuseClient } from './useFuseClient'
import type { TagColor } from 'api/client'

export interface TagInfo {
  name: string
  color: TagColor | undefined
}

export function useTags(enabled: MaybeRefOrGetter<boolean> = true) {
  const client = useFuseClient()

  const query = useQuery({
    queryKey: ['tags'],
    enabled: computed(() => toValue(enabled)),
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

  const tagInfoLookup = computed<Record<string, TagInfo>>(() => {
    const map: Record<string, TagInfo> = {}
    for (const tag of query.data.value ?? []) {
      if (tag.id) {
        map[tag.id] = {
          name: tag.name ?? tag.id,
          color: tag.color
        }
      }
    }
    return map
  })

  return {
    ...query,
    options,
    lookup,
    tagInfoLookup
  }
}
