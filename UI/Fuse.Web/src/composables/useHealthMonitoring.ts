import { computed } from 'vue'
import { useQuery } from '@tanstack/vue-query'
import { HealthCheckProvider } from 'api/client'
import { useFuseClient } from './useFuseClient'
import { useFuseStore } from '../stores/FuseStore'

export function useHealthMonitoring() {
  const client = useFuseClient()
  const store = useFuseStore()
  const provider = computed(() => store.appSettings?.healthCheckProvider ?? HealthCheckProvider.None)
  const enabled = computed(() => !!store.appSettings && provider.value !== HealthCheckProvider.None)
  return useQuery({
    queryKey: computed(() => ['health-monitoring', provider.value]),
    queryFn: () => client.healthMonitoringOverview(),
    enabled,
    refetchInterval: 60_000,
    retry: false
  })
}
