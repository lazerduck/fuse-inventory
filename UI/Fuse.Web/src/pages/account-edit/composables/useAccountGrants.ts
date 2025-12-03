import { computed } from 'vue'
import { useMutation, useQueryClient } from '@tanstack/vue-query'
import { Notify } from 'quasar'
import {
  Account,
  CreateAccountGrant,
  Privilege,
  TargetKind,
  UpdateAccountGrant,
  type SqlIntegrationResponse
} from '../../../api/client'
import { useFuseClient } from '../../../composables/useFuseClient'
import { getErrorMessage } from '../../../utils/error'
import type { ComputedRef, Ref } from 'vue'

interface UseAccountGrantsOptions {
  accountId: ComputedRef<string | undefined>
  account: Ref<Account | null | undefined>
  currentSqlIntegrationId: ComputedRef<string | null>
  sqlIntegrations: Ref<SqlIntegrationResponse[] | undefined>
}

export interface GrantFormInput {
  database: string
  schema: string
  privileges: Privilege[]
}

export function useAccountGrants(options: UseAccountGrantsOptions) {
  const client = useFuseClient()
  const queryClient = useQueryClient()

  const createGrantMutation = useMutation({
    mutationFn: (payload: CreateAccountGrant) => {
      if (!options.accountId.value) {
        throw new Error('Account ID is required to create a grant')
      }
      return client.grantPOST(options.accountId.value, payload)
    },
    onError: (err) => {
      Notify.create({ type: 'negative', message: getErrorMessage(err, 'Unable to create grant') })
    }
  })

  const updateGrantMutation = useMutation({
    mutationFn: ({ grantId, payload }: { grantId: string; payload: UpdateAccountGrant }) => {
      if (!options.accountId.value) {
        throw new Error('Account ID is required to update a grant')
      }
      return client.grantPUT(options.accountId.value, grantId, payload)
    },
    onError: (err) => {
      Notify.create({ type: 'negative', message: getErrorMessage(err, 'Unable to update grant') })
    }
  })

  const deleteGrantMutation = useMutation({
    mutationFn: (grantId: string) => {
      if (!options.accountId.value) {
        throw new Error('Account ID is required to delete a grant')
      }
      return client.grantDELETE(options.accountId.value, grantId)
    },
    onError: (err) => {
      Notify.create({ type: 'negative', message: getErrorMessage(err, 'Unable to delete grant') })
    }
  })

  const grantMutationPending = computed(
    () => createGrantMutation.isPending.value || updateGrantMutation.isPending.value
  )
  const grantDialogLoading = computed(() => grantMutationPending.value)

  function buildCreatePayload(input: GrantFormInput) {
    return Object.assign(new CreateAccountGrant(), {
      accountId: options.accountId.value,
      database: input.database || undefined,
      schema: input.schema || undefined,
      privileges: input.privileges.length ? [...input.privileges] : undefined
    })
  }

  function buildUpdatePayload(input: GrantFormInput) {
    return Object.assign(new UpdateAccountGrant(), {
      database: input.database || undefined,
      schema: input.schema || undefined,
      privileges: input.privileges.length ? [...input.privileges] : undefined
    })
  }

  function getLinkedIntegrationIds(): string[] {
    const ids = new Set<string>()
    if (options.currentSqlIntegrationId.value) {
      ids.add(options.currentSqlIntegrationId.value)
    }

    const existingTargetId =
      options.account.value?.targetKind === TargetKind.DataStore ? options.account.value.targetId ?? null : null
    if (existingTargetId) {
      const integration = (options.sqlIntegrations.value ?? []).find(
        (si) => si.dataStoreId === existingTargetId
      )
      if (integration?.id) {
        ids.add(integration.id)
      }
    }

    return Array.from(ids)
  }

  async function markSqlQueriesStale() {
    const invalidations: Promise<unknown>[] = []
    if (options.accountId.value) {
      invalidations.push(
        queryClient.invalidateQueries({ queryKey: ['account-sql-status', options.accountId.value] })
      )
    }
    for (const integrationId of getLinkedIntegrationIds()) {
      invalidations.push(
        queryClient.invalidateQueries({ queryKey: ['sql-permissions-overview', integrationId] })
      )
    }
    if (invalidations.length) {
      await Promise.all(invalidations)
    }
  }

  async function refreshAccountSqlStatusCache() {
    if (!options.accountId.value) return
    try {
      await client.refresh(options.accountId.value)
    } catch (err) {
      console.warn('Failed to refresh SQL status for account', options.accountId.value, err)
    } finally {
      await markSqlQueriesStale()
    }
  }

  async function handleGrantMutationSuccess(message: string) {
    const invalidations: Promise<unknown>[] = [
      queryClient.invalidateQueries({ queryKey: ['accounts'] })
    ]
    if (options.accountId.value) {
      invalidations.push(
        queryClient.invalidateQueries({ queryKey: ['account', options.accountId.value] })
      )
    }
    await Promise.all(invalidations)
    await refreshAccountSqlStatusCache()
    Notify.create({ type: 'positive', message })
  }

  function createGrant(input: GrantFormInput, onSuccess?: () => void) {
    const payload = buildCreatePayload(input)
    createGrantMutation.mutate(payload, {
      onSuccess: async () => {
        await handleGrantMutationSuccess('Grant created')
        onSuccess?.()
      }
    })
  }

  function updateGrant(grantId: string, input: GrantFormInput, onSuccess?: () => void) {
    const payload = buildUpdatePayload(input)
    updateGrantMutation.mutate(
      { grantId, payload },
      {
        onSuccess: async () => {
          await handleGrantMutationSuccess('Grant updated')
          onSuccess?.()
        }
      }
    )
  }

  function deleteGrant(grantId: string) {
    deleteGrantMutation.mutate(grantId, {
      onSuccess: async () => {
        await handleGrantMutationSuccess('Grant removed')
      }
    })
  }

  return {
    grantMutationPending,
    grantDialogLoading,
    createGrant,
    updateGrant,
    deleteGrant
  }
}
