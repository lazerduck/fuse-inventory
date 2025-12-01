import { useMutation, useQueryClient } from '@tanstack/vue-query'
import { useFuseClient } from './useFuseClient'
import { ImportPermissionsResponse } from '../api/client'

export function useImportPermissions() {
  const client = useFuseClient()
  const queryClient = useQueryClient()

  const mutation = useMutation({
    mutationFn: (params: { integrationId: string; accountId: string }): Promise<ImportPermissionsResponse> =>
      client.importAccountPermissions(params.integrationId, params.accountId),
    onSuccess: (_data, variables) => {
      // Invalidate the permissions overview to refresh the data
      queryClient.invalidateQueries({
        queryKey: ['sql-permissions-overview', variables.integrationId]
      })
      // Also invalidate accounts as the grants may have changed
      queryClient.invalidateQueries({
        queryKey: ['accounts']
      })
    }
  })

  return {
    mutate: mutation.mutate,
    mutateAsync: mutation.mutateAsync,
    isPending: mutation.isPending,
    isSuccess: mutation.isSuccess,
    isError: mutation.isError,
    error: mutation.error,
    data: mutation.data,
    reset: mutation.reset
  }
}
