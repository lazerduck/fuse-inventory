import { computed, type Ref } from 'vue'
import { useQuery } from '@tanstack/vue-query'
import { useApplications } from './useApplications'
import { useKumaIntegrations } from './useKumaIntegrations'
import { useFuseClient } from './useFuseClient'
import { type HealthStatusResponse, MonitorStatus } from '../api/client'

export interface ServiceHealthEntry {
  applicationId: string
  applicationName: string
  instanceId: string
  environmentId: string | null
  platformId: string | null
  healthUri: string | null
  health: HealthStatusResponse | null
}

export interface UseKumaDashboardOptions {
  environmentIds?: Ref<string[]>
  platformIds?: Ref<string[]>
}

export function useKumaDashboard(options?: UseKumaDashboardOptions) {
  const client = useFuseClient()
  const applicationsQuery = useApplications()
  const kumaIntegrationsQuery = useKumaIntegrations()

  const hasKumaIntegration = computed(() => {
    const integrations = kumaIntegrationsQuery.data.value ?? []
    return integrations.length > 0
  })

  const servicesQuery = useQuery({
    queryKey: computed(() => [
      'kumaDashboard',
      options?.environmentIds?.value ?? [],
      options?.platformIds?.value ?? []
    ]),
    queryFn: async (): Promise<ServiceHealthEntry[]> => {
      const applications = applicationsQuery.data.value ?? []
      const filterEnvIds = options?.environmentIds?.value ?? []
      const filterPlatformIds = options?.platformIds?.value ?? []

      const entries: ServiceHealthEntry[] = []

      for (const app of applications) {
        const instances = app.instances ?? []

        for (const instance of instances) {
          // When environment filter is active, skip instances not in the filter (including unassigned)
          if (filterEnvIds.length > 0) {
            if (!instance.environmentId || !filterEnvIds.includes(instance.environmentId)) {
              continue
            }
          }
          // When platform filter is active, skip instances not in the filter (including unassigned)
          if (filterPlatformIds.length > 0) {
            if (!instance.platformId || !filterPlatformIds.includes(instance.platformId)) {
              continue
            }
          }

          // Only include instances that have a health URI
          if (!instance.healthUri) continue

          let health: HealthStatusResponse | null = null
          if (app.id && instance.id) {
            try {
              health = await client.health(app.id, instance.id)
            } catch {
              // Health data not yet available
            }
          }

          entries.push({
            applicationId: app.id ?? '',
            applicationName: app.name ?? '',
            instanceId: instance.id ?? '',
            environmentId: instance.environmentId ?? null,
            platformId: instance.platformId ?? null,
            healthUri: instance.healthUri ?? null,
            health
          })
        }
      }

      return entries
    },
    enabled: computed(
      () =>
        !applicationsQuery.isLoading.value &&
        !kumaIntegrationsQuery.isLoading.value &&
        hasKumaIntegration.value
    ),
    refetchInterval: 20000,
    retry: false
  })

  const statusCounts = computed(() => {
    const entries = servicesQuery.data.value ?? []
    return {
      up: entries.filter(e => e.health?.status === MonitorStatus.Up).length,
      down: entries.filter(e => e.health?.status === MonitorStatus.Down).length,
      pending: entries.filter(e => e.health?.status === MonitorStatus.Pending).length,
      maintenance: entries.filter(e => e.health?.status === MonitorStatus.Maintenance).length,
      unknown: entries.filter(e => e.health === null).length
    }
  })

  return {
    data: servicesQuery.data,
    isLoading: servicesQuery.isLoading,
    isFetching: servicesQuery.isFetching,
    refetch: servicesQuery.refetch,
    hasKumaIntegration,
    statusCounts
  }
}
