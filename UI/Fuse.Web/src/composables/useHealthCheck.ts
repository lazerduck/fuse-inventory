import { useQuery } from '@tanstack/vue-query'
import type { HealthStatusResponse } from '../types/health'

export function useHealthCheck(appId: string, instanceId: string, enabled: boolean = true) {
  
  return useQuery({
    queryKey: ['health', appId, instanceId],
    queryFn: async (): Promise<HealthStatusResponse> => {
      const response = await fetch(`/api/application/${appId}/instances/${instanceId}/health`)
      if (!response.ok) {
        throw new Error('Health check not available')
      }
      return response.json()
    },
    enabled,
    refetchInterval: 60000, // Refetch every minute
    retry: false // Don't retry if health check is not available
  })
}
