import { useMutation, useQueryClient } from '@tanstack/vue-query'
import { useFuseClient } from './useFuseClient'
import { BulkResolveRequest, BulkResolveResponse, BulkPasswordSource } from '../api/client'

export function useBulkResolve() {
  const client = useFuseClient()
  const queryClient = useQueryClient()

  const mutation = useMutation({
    mutationFn: (params: { 
      integrationId: string
      passwordSource: BulkPasswordSource
    }): Promise<BulkResolveResponse> => {
      const request = new BulkResolveRequest()
      request.passwordSource = params.passwordSource
      return client.bulkResolve(params.integrationId, request)
    },
    onSuccess: (_data, variables) => {
      // Invalidate the permissions overview to refresh the data
      queryClient.invalidateQueries({
        queryKey: ['sql-permissions-overview', variables.integrationId]
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
