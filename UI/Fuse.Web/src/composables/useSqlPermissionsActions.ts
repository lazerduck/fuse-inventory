import { computed, ref, toValue, type MaybeRef } from 'vue'
import { useQueryClient } from '@tanstack/vue-query'
import { useResolveDrift } from './useResolveDrift'
import { useCreateSqlAccount } from './useCreateSqlAccount'
import { useBulkResolve } from './useBulkResolve'
import { useImportPermissions } from './useImportPermissions'
import { useImportOrphanPrincipal } from './useImportOrphanPrincipal'
import { useAccounts } from './useAccounts'
import { useFuseClient } from './useFuseClient'
import {
  AuthKind,
  BulkPasswordSource,
  BulkResolveResponse,
  CreateSqlAccountResponse,
  ImportOrphanPrincipalRequest,
  ImportOrphanPrincipalResponse,
  ImportPermissionsResponse,
  PasswordSource,
  ResolveDriftResponse,
  SecretBinding,
  SecretBindingKind,
  type SqlAccountPermissionsStatus,
  type SqlOrphanPrincipal
} from '../api/client'

interface CreateAccountOptions {
  passwordSource: PasswordSource
  password?: string
}

interface ImportOrphanOptions {
  authKind: AuthKind
  secretBindingKind: SecretBindingKind
  plainReference?: string
}

export function useSqlPermissionsActions(integrationId: MaybeRef<string | null | undefined>) {
  const client = useFuseClient()
  const queryClient = useQueryClient()
  const accountsQuery = useAccounts()

  const { mutateAsync: resolveDrift } = useResolveDrift()
  const { mutateAsync: createSqlAccount } = useCreateSqlAccount()
  const { mutateAsync: bulkResolve } = useBulkResolve()
  const { mutateAsync: importPermissions } = useImportPermissions()
  const { mutateAsync: importOrphan } = useImportOrphanPrincipal()

  const resolvingAccountId = ref<string | null>(null)
  const creatingAccountId = ref<string | null>(null)
  const importingAccountId = ref<string | null>(null)
  const importingOrphanName = ref<string | null>(null)
  const isBulkResolving = ref(false)

  const integrationKey = computed(() => toValue(integrationId))

  function getUniqueAccountIds(ids: Array<string | null | undefined>): string[] {
    return Array.from(new Set(ids.filter((id): id is string => Boolean(id))))
  }

  async function refreshSqlOverviewCache(accountIds: Array<string | null | undefined> = []) {
    if (!integrationKey.value) return

    const uniqueAccountIds = getUniqueAccountIds(accountIds)
    if (uniqueAccountIds.length) {
      await Promise.all(
        uniqueAccountIds.map((id) =>
          client
            .refresh(id)
            .catch((err) => console.warn('Failed to refresh SQL status for account', id, err))
        )
      )
    }

    const invalidations: Promise<unknown>[] = [
      queryClient.invalidateQueries({ queryKey: ['sql-permissions-overview', integrationKey.value] })
    ]

    if (uniqueAccountIds.length) {
      invalidations.push(queryClient.invalidateQueries({ queryKey: ['accounts'] }))
      for (const id of uniqueAccountIds) {
        invalidations.push(
          queryClient.invalidateQueries({ queryKey: ['account-sql-status', id] }),
          queryClient.invalidateQueries({ queryKey: ['account', id] })
        )
      }
    }

    await Promise.all(invalidations)
  }

  function getAccountSecretBinding(accountId: string) {
    const account = accountsQuery.data.value?.find((a) => a.id === accountId)
    return account?.secretBinding
  }

  function hasSecretProvider(accountId: string): boolean {
    const binding = getAccountSecretBinding(accountId)
    return binding?.kind === SecretBindingKind.AzureKeyVault && !!binding.azureKeyVault
  }

  async function resolveAccount(account: SqlAccountPermissionsStatus): Promise<ResolveDriftResponse> {
    if (!integrationKey.value || !account.accountId) {
      throw new Error('Integration or account ID missing')
    }

    resolvingAccountId.value = account.accountId
    try {
      const result = await resolveDrift({
        integrationId: integrationKey.value,
        accountId: account.accountId
      })
      await refreshSqlOverviewCache([account.accountId])
      return result
    } catch (err: any) {
      const errorResponse = new ResolveDriftResponse()
      errorResponse.accountId = account.accountId
      errorResponse.principalName = account.principalName
      errorResponse.success = false
      errorResponse.operations = []
      errorResponse.errorMessage = err?.status === 401
        ? 'Authentication required. Please log in as an admin to resolve permission drift.'
        : err?.message || 'An error occurred while resolving drift.'
      return errorResponse
    } finally {
      resolvingAccountId.value = null
    }
  }

  async function createAccount(account: SqlAccountPermissionsStatus, options: CreateAccountOptions): Promise<CreateSqlAccountResponse> {
    if (!integrationKey.value || !account.accountId) {
      throw new Error('Integration or account ID missing')
    }

    creatingAccountId.value = account.accountId
    try {
      const result = await createSqlAccount({
        integrationId: integrationKey.value,
        accountId: account.accountId,
        passwordSource: options.passwordSource,
        password: options.password
      })
      await refreshSqlOverviewCache([account.accountId])
      return result
    } catch (err: any) {
      const errorResponse = new CreateSqlAccountResponse()
      errorResponse.accountId = account.accountId
      errorResponse.principalName = account.principalName
      errorResponse.success = false
      errorResponse.operations = []
      errorResponse.errorMessage = err?.status === 401
        ? 'Authentication required. Please log in as an admin to create SQL accounts.'
        : err?.message || 'An error occurred while creating the account.'
      return errorResponse
    } finally {
      creatingAccountId.value = null
    }
  }

  async function bulkResolveAll(): Promise<BulkResolveResponse> {
    if (!integrationKey.value) {
      throw new Error('Integration ID missing')
    }

    isBulkResolving.value = true
    try {
      const result = await bulkResolve({
        integrationId: integrationKey.value,
        passwordSource: BulkPasswordSource.SecretProvider
      })
      const affectedAccountIds = (result.results ?? []).map((r) => r.accountId)
      await refreshSqlOverviewCache(affectedAccountIds)
      return result
    } catch (err: any) {
      return new BulkResolveResponse({
        integrationId: integrationKey.value,
        success: false,
        results: [],
        errorMessage:
          err?.status === 401
            ? 'Authentication required. Please log in as an admin to perform bulk resolve.'
            : err?.message || 'An error occurred during bulk resolve.'
      })
    } finally {
      isBulkResolving.value = false
    }
  }

  async function importAccountPermissions(account: SqlAccountPermissionsStatus): Promise<ImportPermissionsResponse> {
    if (!integrationKey.value || !account.accountId) {
      throw new Error('Integration or account ID missing')
    }

    importingAccountId.value = account.accountId
    try {
      const result = await importPermissions({
        integrationId: integrationKey.value,
        accountId: account.accountId
      })
      await refreshSqlOverviewCache([account.accountId])
      return result
    } catch (err: any) {
      const errorResponse = new ImportPermissionsResponse()
      errorResponse.accountId = account.accountId
      errorResponse.principalName = account.principalName
      errorResponse.success = false
      errorResponse.importedGrants = []
      errorResponse.errorMessage = err?.status === 401
        ? 'Authentication required. Please log in as an admin to import permissions.'
        : err?.message || 'An error occurred while importing permissions.'
      return errorResponse
    } finally {
      importingAccountId.value = null
    }
  }

  async function importOrphanPrincipal(orphan: SqlOrphanPrincipal, options: ImportOrphanOptions): Promise<ImportOrphanPrincipalResponse> {
    if (!integrationKey.value || !orphan.principalName) {
      throw new Error('Integration ID or principal missing')
    }

    importingOrphanName.value = orphan.principalName
    try {
      const secretBinding = new SecretBinding()
      secretBinding.kind = options.secretBindingKind
      if (options.secretBindingKind === SecretBindingKind.PlainReference) {
        secretBinding.plainReference = options.plainReference
      }

      const request = new ImportOrphanPrincipalRequest()
      request.principalName = orphan.principalName
      request.authKind = options.authKind
      request.secretBinding = secretBinding

      const result = await importOrphan({
        integrationId: integrationKey.value,
        request
      })

      await refreshSqlOverviewCache([result.accountId])
      return result
    } catch (err: any) {
      const errorResponse = new ImportOrphanPrincipalResponse()
      errorResponse.accountId = ''
      errorResponse.principalName = orphan.principalName
      errorResponse.success = false
      errorResponse.importedGrants = []
      if (err?.status === 401) {
        errorResponse.errorMessage = 'Authentication required. Please log in as an admin to import accounts.'
      } else if (err?.status === 409) {
        errorResponse.errorMessage = 'This principal is already managed by a Fuse account.'
      } else {
        errorResponse.errorMessage = err?.message || 'An error occurred while importing the account.'
      }
      return errorResponse
    } finally {
      importingOrphanName.value = null
    }
  }

  return {
    resolveAccount,
    createAccount,
    bulkResolveAll,
    importAccountPermissions,
    importOrphanPrincipal,
    refreshSqlOverviewCache,
    hasSecretProvider,
    states: {
      resolvingAccountId,
      creatingAccountId,
      importingAccountId,
      importingOrphanName,
      isBulkResolving
    }
  }
}
