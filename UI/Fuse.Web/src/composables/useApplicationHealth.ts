import { computed, type Ref } from 'vue'
import { useQuery } from '@tanstack/vue-query'
import { useApplications } from './useApplications'
import { useKumaIntegrations } from './useKumaIntegrations'
import type { HealthStatusResponse } from '../types/health'
import { MonitorStatus } from '../types/health'

export interface ApplicationHealthStats {
  total: number
  healthy: number
  unhealthy: number
  hasKumaIntegration: boolean
}

export interface UseApplicationHealthOptions {
  environmentIds?: Ref<string[]>
}

export function useApplicationHealth(options?: UseApplicationHealthOptions) {
  const applicationsQuery = useApplications()
  const kumaIntegrationsQuery = useKumaIntegrations()

  // Check if Kuma integration is available and configured
  const hasKumaIntegration = computed(() => {
    const integrations = kumaIntegrationsQuery.data.value ?? []
    return integrations.length > 0
  })

  // Get health status for application instances, optionally filtered by environment
  const healthStatsQuery = useQuery({
    queryKey: computed(() => ['applicationHealthStats', options?.environmentIds?.value ?? []]),
    queryFn: async (): Promise<ApplicationHealthStats> => {
      const applications = applicationsQuery.data.value ?? []
      const filterEnvIds = options?.environmentIds?.value ?? []
      
      // If no Kuma integration, return basic stats
      if (!hasKumaIntegration.value) {
        return {
          total: applications.length,
          healthy: 0,
          unhealthy: 0,
          hasKumaIntegration: false
        }
      }

      let healthyCount = 0
      let unhealthyCount = 0
      
      // Check each application to see if it has any instances with health status
      for (const app of applications) {
        let instances = app.instances ?? []
        
        // Filter instances by environment if filter is provided
        if (filterEnvIds.length > 0) {
          instances = instances.filter(inst => filterEnvIds.includes(inst.environmentId ?? ''))
        }
        
        // Skip applications with no matching instances
        if (instances.length === 0) continue
        
        let appHasHealthyInstance = false
        let appHasUnhealthyInstance = false
        
        // Check each instance's health status
        for (const instance of instances) {
          // Skip instances without health URIs
          if (!instance.healthUri) continue
          
          try {
            const response = await fetch(`/api/application/${app.id}/instances/${instance.id}/health`)
            if (response.ok) {
              const healthStatus: HealthStatusResponse = await response.json()
              
              // Consider "Up" as healthy, all others as unhealthy
              if (healthStatus.Status === MonitorStatus.Up) {
                appHasHealthyInstance = true
              } else {
                appHasUnhealthyInstance = true
              }
            }
          } catch (error) {
            // If health check fails, don't count it
            continue
          }
        }
        
        // Count application as healthy if it has at least one healthy instance and no unhealthy ones
        // Count as unhealthy if it has any unhealthy instances
        if (appHasUnhealthyInstance) {
          unhealthyCount++
        } else if (appHasHealthyInstance) {
          healthyCount++
        }
      }
      
      return {
        total: applications.length,
        healthy: healthyCount,
        unhealthy: unhealthyCount,
        hasKumaIntegration: true
      }
    },
    enabled: computed(() => !applicationsQuery.isLoading.value && !kumaIntegrationsQuery.isLoading.value),
    refetchInterval: 60000, // Refetch every minute to keep health status current
    retry: false
  })

  return {
    data: healthStatsQuery.data,
    isLoading: healthStatsQuery.isLoading,
    isFetching: healthStatsQuery.isFetching,
    hasKumaIntegration
  }
}
