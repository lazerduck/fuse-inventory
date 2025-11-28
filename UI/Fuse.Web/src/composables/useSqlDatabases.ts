import { useQuery } from '@tanstack/vue-query'
import type { Ref, ComputedRef } from 'vue'
import { SqlDatabasesResponse } from '../api/client'
import { useFuseClient } from './useFuseClient'

export function useSqlDatabases(integrationId: Ref<string | null> | ComputedRef<string | null>) {
  const client = useFuseClient()
  
  return useQuery({
    queryKey: ['sqlDatabases', integrationId],
    queryFn: () => {
      if (!integrationId.value) {
        return new SqlDatabasesResponse({ databases: [] })
      }
      return client.databases(integrationId.value)
    },
    enabled: () => !!integrationId.value,
    staleTime: 60000, // Cache for 1 minute
    retry: 1
  })
}
