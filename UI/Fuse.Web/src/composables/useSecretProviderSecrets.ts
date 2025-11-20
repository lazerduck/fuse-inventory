import { computed, type Ref } from 'vue'
import { useQuery } from '@tanstack/vue-query'
import { useFuseClient } from './useFuseClient'

export function useSecretProviderSecrets(providerId: Ref<string | null | undefined>) {
  const client = useFuseClient()

  return useQuery({
    queryKey: computed(() => ['secret-provider-secrets', providerId.value]),
    queryFn: () => client.secretsAll(providerId.value!),
    enabled: computed(() => !!providerId.value),
    staleTime: 30_000
  })
}
