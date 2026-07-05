import { computed } from 'vue'
import { useHealthMonitoring } from './useHealthMonitoring'

export function useHealthCheck(appId: string, instanceId: string, enabled: boolean = true) {
  
  const query = useHealthMonitoring()
  const data = computed(() => enabled ? query.data.value?.results?.find(result => result.applicationId === appId && result.instanceId === instanceId) : undefined)
  return { ...query, data }
}
