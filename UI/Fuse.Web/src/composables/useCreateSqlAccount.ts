import { useMutation, useQueryClient } from '@tanstack/vue-query'
import { useFuseClient } from './useFuseClient'
import { CreateSqlAccountRequest, CreateSqlAccountResponse, PasswordSource } from '../api/client'

export function useCreateSqlAccount() {
  const client = useFuseClient()
  const queryClient = useQueryClient()

  const mutation = useMutation({
    mutationFn: (params: { 
      integrationId: string
      accountId: string
      passwordSource: PasswordSource
      password?: string
    }): Promise<CreateSqlAccountResponse> => {
      const request = new CreateSqlAccountRequest()
      request.passwordSource = params.passwordSource
      request.password = params.password
      return client.create(params.integrationId, params.accountId, request)
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
