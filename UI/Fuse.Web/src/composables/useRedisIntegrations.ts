import { useQuery } from '@tanstack/vue-query'
import { computed, toValue, type MaybeRefOrGetter } from 'vue'
import { listRedisIntegrations } from '../api/redisIntegrations'

export function useRedisIntegrations(enabled: MaybeRefOrGetter<boolean> = true) {
  return useQuery({
    queryKey: ['redisIntegrations'],
    queryFn: ({ signal }) => listRedisIntegrations(signal),
    enabled: computed(() => toValue(enabled)),
  })
}
