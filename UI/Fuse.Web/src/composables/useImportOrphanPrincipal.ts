import { useMutation, useQueryClient } from '@tanstack/vue-query'
import { useFuseClient } from './useFuseClient'
import { ImportOrphanPrincipalRequest, ImportOrphanPrincipalResponse } from '../api/client'

export function useImportOrphanPrincipal() {
  const client = useFuseClient()
  const queryClient = useQueryClient()

  const mutation = useMutation({
    mutationFn: (params: { integrationId: string; request: ImportOrphanPrincipalRequest }): Promise<ImportOrphanPrincipalResponse> =>
      client.importOrphanPrincipal(params.integrationId, params.request),
    onSuccess: (_data, variables) => {
      // Invalidate the permissions overview to refresh the data
      queryClient.invalidateQueries({
        queryKey: ['sql-permissions-overview', variables.integrationId]
      })
      // Also invalidate accounts as a new account was created
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
