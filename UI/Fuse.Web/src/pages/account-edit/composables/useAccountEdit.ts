import { computed, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useMutation, useQuery, useQueryClient } from '@tanstack/vue-query'
import { Dialog, Notify } from 'quasar'
import {
  Account,
  AuthKind,
  AzureKeyVaultBinding,
  CreateAccount,
  DependencyAuthKind,
  Grant,
  Privilege,
  SecretBinding,
  SecretBindingKind,
  TargetKind,
  UpdateAccount
} from '../../../api/client'
import type {
  AccountFormModel,
  AccountSecretFields,
  KeyValuePair,
  SelectOption,
  TargetOption
} from '../../../components/accounts/types'
import { useFuseClient } from '../../../composables/useFuseClient'
import { useApplications } from '../../../composables/useApplications'
import { useDataStores } from '../../../composables/useDataStores'
import { useEnvironments } from '../../../composables/useEnvironments'
import { useExternalResources } from '../../../composables/useExternalResources'
import { useSecretProviders } from '../../../composables/useSecretProviders'
import { useSecretProviderSecrets } from '../../../composables/useSecretProviderSecrets'
import { useSqlIntegrations } from '../../../composables/useSqlIntegrations'
import { useSqlDatabases } from '../../../composables/useSqlDatabases'
import { getErrorMessage } from '../../../utils/error'
import { useFuseStore } from '../../../stores/FuseStore'

function emptySecretFields(): AccountSecretFields {
  return {
    providerId: null,
    secretName: null,
    plainReference: ''
  }
}

function emptyAccountForm(): AccountFormModel {
  return {
    targetKind: TargetKind.Application,
    targetId: null,
    authKind: AuthKind.None,
    userName: '',
    secret: emptySecretFields(),
    parameters: [],
    tagIds: [],
    grants: []
  }
}

export function useAccountEdit() {
  const route = useRoute()
  const router = useRouter()
  const client = useFuseClient()
  const fuseStore = useFuseStore()
  const queryClient = useQueryClient()

  const applicationsQuery = useApplications()
  const dataStoresQuery = useDataStores()
  const externalResourcesQuery = useExternalResources()
  const environmentsQuery = useEnvironments()
  const secretProvidersQuery = useSecretProviders()
  const sqlIntegrationsQuery = useSqlIntegrations()

  const accountId = computed(() => route.params.id as string | undefined)
  const isEditMode = computed(() => !!accountId.value)

  const pageTitle = computed(() => (isEditMode.value ? 'Edit Account' : 'Create Account'))
  const pageSubtitle = computed(() =>
    isEditMode.value
      ? 'Update account credentials and permissions'
      : 'Create a new account with credentials and grants'
  )

  const { data: account, error: loadError } = useQuery({
    queryKey: computed(() => ['account', accountId.value]),
    queryFn: () => client.accountGET(accountId.value!),
    enabled: computed(() => !!accountId.value),
    retry: false
  })

  const accountProviderId = computed(() => {
    const binding = account.value?.secretBinding
    if (binding?.kind === SecretBindingKind.AzureKeyVault) {
      return binding.azureKeyVault?.providerId ?? null
    }
    return null
  })

  const accountSecretsQuery = useSecretProviderSecrets(accountProviderId)

  const isLoadingInitialData = computed(() => {
    if (!isEditMode.value) return false
    if (!account.value) return true
    if (accountProviderId.value && accountSecretsQuery.isLoading.value) {
      return true
    }
    return false
  })

  const form = ref<AccountFormModel>(emptyAccountForm())

  const targetKindOptions: SelectOption<TargetKind>[] = Object.values(TargetKind).map((value) => ({
    label: value,
    value
  }))
  const authKindOptions: SelectOption<AuthKind>[] = Object.values(AuthKind).map((value) => ({
    label: value,
    value
  }))
  const privilegeOptions = Object.values(Privilege).map((value) => ({
    label: value,
    value
  }))

  const targetOptions = computed<TargetOption[]>(() => {
    const kind = form.value.targetKind ?? TargetKind.Application
    if (kind === TargetKind.Application) {
      const apps = applicationsQuery.data.value ?? []
      const envLookup = environmentsQuery.lookup.value
      const options: TargetOption[] = []
      for (const app of apps) {
        const appName = app.name ?? app.id ?? 'Application'
        for (const inst of app.instances ?? []) {
          if (!inst?.id) continue
          const envName = inst.environmentId ? envLookup[inst.environmentId] ?? inst.environmentId : '—'
          options.push({ label: `${appName} — ${envName}`, value: inst.id })
        }
      }
      return options
    }
    if (kind === TargetKind.DataStore) {
      return (dataStoresQuery.data.value ?? [])
        .filter((item) => !!item.id)
        .map((item) => ({ label: item.name ?? item.id!, value: item.id! }))
    }
    return (externalResourcesQuery.data.value ?? [])
      .filter((item) => !!item.id)
      .map((item) => ({ label: item.name ?? item.id!, value: item.id! }))
  })

  const currentSqlIntegrationId = computed<string | null>(() => {
    const formValue = form.value
    if (formValue.targetKind !== TargetKind.DataStore || !formValue.targetId) {
      return null
    }
    const integration = (sqlIntegrationsQuery.data.value ?? []).find((si) => si.dataStoreId === formValue.targetId)
    return integration?.id ?? null
  })

  const databasesQuery = useSqlDatabases(currentSqlIntegrationId)
  const databaseOptions = computed(() => {
    const databases = databasesQuery.data.value?.databases ?? []
    return databases.map((db) => ({ label: db, value: db }))
  })
  const isDatabasesLoading = computed(
    () => databasesQuery.isLoading.value || databasesQuery.isFetching.value
  )
  const hasSqlIntegration = computed(() => !!currentSqlIntegrationId.value)

  const selectedProvider = computed(() =>
    secretProvidersQuery.data.value?.find((provider) => provider.id === form.value.secret.providerId) ?? null
  )

  const showSqlStatus = computed(
    () => isEditMode.value && !!accountId.value && form.value.targetKind === TargetKind.DataStore
  )

  watch(
    [account, isLoadingInitialData],
    ([acc, loading]) => {
      if (acc && !loading) {
        Object.assign(form.value, {
          targetKind: acc.targetKind ?? TargetKind.Application,
          targetId: acc.targetId ?? null,
          authKind: acc.authKind ?? AuthKind.None,
          userName: acc.userName ?? '',
          secret: mapSecretBindingToForm(acc),
          parameters: convertParametersToPairs(acc.parameters),
          tagIds: [...(acc.tagIds ?? [])],
          grants: (acc.grants ?? []).map(cloneGrant)
        })
        ensureTarget()
      }
    },
    { immediate: true }
  )

  watch(
    () => form.value.targetKind,
    () => ensureTarget()
  )

  watch(
    () => [
      applicationsQuery.data.value,
      dataStoresQuery.data.value,
      externalResourcesQuery.data.value,
      environmentsQuery.data.value
    ],
    () => ensureTarget()
  )

  function ensureTarget() {
    const options = targetOptions.value
    if (!form.value.targetId || !options.some((option) => option.value === form.value.targetId)) {
      form.value.targetId = options[0]?.value ?? null
    }
  }

  function convertParametersToPairs(parameters?: Record<string, string>): KeyValuePair[] {
    if (!parameters) return []
    return Object.entries(parameters).map(([key, value]) => ({ key, value }))
  }

  function buildParameters(list: KeyValuePair[]) {
    const result: Record<string, string> = {}
    for (const pair of list) {
      if (pair.key) {
        result[pair.key] = pair.value
      }
    }
    return Object.keys(result).length ? result : undefined
  }

  function mapSecretBindingToForm(currentAccount: Account | null): AccountSecretFields {
    if (!currentAccount?.secretBinding) {
      return {
        providerId: null,
        secretName: null,
        plainReference: currentAccount?.secretRef ?? ''
      }
    }

    const binding = currentAccount.secretBinding
    if (binding.kind === SecretBindingKind.AzureKeyVault) {
      return {
        providerId: binding.azureKeyVault?.providerId ?? null,
        secretName: binding.azureKeyVault?.secretName ?? null,
        plainReference: ''
      }
    }

    if (binding.kind === SecretBindingKind.PlainReference) {
      return {
        providerId: null,
        secretName: null,
        plainReference: binding.plainReference ?? currentAccount.secretRef ?? ''
      }
    }

    return emptySecretFields()
  }

  function buildSecretBindingPayload(secret: AccountSecretFields) {
    if (secret.providerId && secret.secretName) {
      return Object.assign(new SecretBinding(), {
        kind: SecretBindingKind.AzureKeyVault,
        azureKeyVault: Object.assign(new AzureKeyVaultBinding(), {
          providerId: secret.providerId,
          secretName: secret.secretName
        })
      })
    }

    if (!secret.plainReference?.trim()) {
      return Object.assign(new SecretBinding(), {
        kind: SecretBindingKind.None
      })
    }

    return Object.assign(new SecretBinding(), {
      kind: SecretBindingKind.PlainReference,
      plainReference: secret.plainReference.trim()
    })
  }

  function cloneGrant(grant: Grant): Grant {
    return Object.assign(new Grant(), {
      id: grant.id ?? undefined,
      database: grant.database ?? undefined,
      schema: grant.schema ?? undefined,
      privileges: grant.privileges ? [...grant.privileges] : undefined
    })
  }

  function buildGrantPayload(grant: Grant): Grant {
    return Object.assign(new Grant(), {
      id: grant.id ?? undefined,
      database: grant.database || undefined,
      schema: grant.schema || undefined,
      privileges: grant.privileges && grant.privileges.length ? [...grant.privileges] : undefined
    })
  }

  const createMutation = useMutation({
    mutationFn: (payload: CreateAccount) => client.accountPOST(payload),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['accounts'] })
      Notify.create({ type: 'positive', message: 'Account created' })
      router.push('/accounts')
    },
    onError: (err) => {
      Notify.create({ type: 'negative', message: getErrorMessage(err, 'Unable to create account') })
    }
  })

  const updateMutation = useMutation({
    mutationFn: ({ id, payload }: { id: string; payload: UpdateAccount }) => client.accountPUT(id, payload),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['accounts'] })
      queryClient.invalidateQueries({ queryKey: ['account', accountId.value] })
      Notify.create({ type: 'positive', message: 'Account updated' })
      router.push('/accounts')
    },
    onError: (err) => {
      Notify.create({ type: 'negative', message: getErrorMessage(err, 'Unable to update account') })
    }
  })

  const isSaving = computed(() => createMutation.isPending.value || updateMutation.isPending.value)

  function handleCancel() {
    router.push('/accounts')
  }

  function getAffectedDependencyCount(): number {
    if (!account.value || !accountId.value) return 0

    const originalTargetId = account.value.targetId
    const originalTargetKind = account.value.targetKind
    const newTargetId = form.value.targetId
    const newTargetKind = form.value.targetKind

    if (originalTargetId === newTargetId && originalTargetKind === newTargetKind) {
      return 0
    }

    let count = 0
    for (const app of applicationsQuery.data.value ?? []) {
      for (const inst of app.instances ?? []) {
        for (const dep of inst.dependencies ?? []) {
          if (dep.authKind === DependencyAuthKind.Account && dep.accountId === accountId.value) {
            count++
          }
        }
      }
    }
    return count
  }

  function performSave() {
    if (isEditMode.value && accountId.value) {
      const payload = Object.assign(new UpdateAccount(), {
        targetKind: form.value.targetKind,
        targetId: form.value.targetId ?? undefined,
        authKind: form.value.authKind,
        userName: form.value.userName || undefined,
        secretBinding: buildSecretBindingPayload(form.value.secret),
        parameters: buildParameters(form.value.parameters),
        grants: (account.value?.grants ?? []).map((grant) => buildGrantPayload(grant)),
        tagIds: form.value.tagIds.length ? [...form.value.tagIds] : undefined
      })
      updateMutation.mutate({ id: accountId.value, payload })
    } else {
      const payload = Object.assign(new CreateAccount(), {
        targetKind: form.value.targetKind,
        targetId: form.value.targetId ?? undefined,
        authKind: form.value.authKind,
        userName: form.value.userName || undefined,
        secretBinding: buildSecretBindingPayload(form.value.secret),
        parameters: buildParameters(form.value.parameters),
        grants: form.value.grants.map((grant) => buildGrantPayload(grant)),
        tagIds: form.value.tagIds.length ? [...form.value.tagIds] : undefined
      })
      createMutation.mutate(payload)
    }
  }

  function handleSave() {
    if (form.value.secret.providerId && !form.value.secret.secretName) {
      Notify.create({ type: 'warning', message: 'Select a secret name before saving.' })
      return
    }

    const affectedCount = getAffectedDependencyCount()
    if (affectedCount > 0) {
      Dialog.create({
        title: 'Target Change Warning',
        message: `Changing this account's target will remove it from ${affectedCount} ${affectedCount === 1 ? 'dependency' : 'dependencies'}. The affected dependencies will have their account reference cleared. Are you sure you want to continue?`,
        cancel: true,
        persistent: true
      }).onOk(() => {
        performSave()
      })
    } else {
      performSave()
    }
  }

  const canModify = computed(() => fuseStore.canModify)

  return {
    accountId,
    isEditMode,
    pageTitle,
    pageSubtitle,
    account,
    loadError,
    form,
    isLoadingInitialData,
    targetKindOptions,
    authKindOptions,
    privilegeOptions,
    targetOptions,
    hasSqlIntegration,
    databaseOptions,
    isDatabasesLoading,
    showSqlStatus,
    accountSecretsQuery,
    accountProviderId,
    selectedProvider,
    fuseStore,
    canModify,
    isSaving,
    handleSave,
    handleCancel,
    sqlIntegrationsQuery,
    currentSqlIntegrationId,
    secretProvidersQuery
  }
}
