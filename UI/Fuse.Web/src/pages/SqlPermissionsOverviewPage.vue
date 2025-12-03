<template>
  <div class="page-container">
    <SqlPermissionsHeader
      :integration-name="data?.overview?.integrationName"
      :is-fetching="isFetching"
      @back="router.push({ name: 'sqlIntegrations' })"
      @refresh="() => refetch()"
    />

    <div v-if="isLoading" class="q-pa-xl text-center">
      <q-spinner color="primary" size="3em" />
      <div class="text-grey-7 q-mt-md">Loading permissions overview...</div>
    </div>

    <q-banner v-else-if="error" dense class="bg-red-1 text-negative q-mb-md">
      <template #avatar>
        <q-icon name="error" color="negative" />
      </template>
      Unable to load permissions overview. Please try again.
      <template #action>
        <q-btn flat label="Retry" @click="() => refetch()" />
      </template>
    </q-banner>

    <template v-else-if="data">
      <q-banner v-if="data.overview?.errorMessage" dense class="bg-orange-1 text-orange-9 q-mb-md">
        <template #avatar>
          <q-icon name="warning" color="orange" />
        </template>
        {{ data.overview.errorMessage }}
      </q-banner>

      <SqlPermissionsSummaryCard class="q-mb-md" :summary="data.overview?.summary" />

      <div v-if="hasResolvableAccounts && canBulkResolve" class="q-mb-md">
        <SqlPermissionsBulkActions
          :resolvable-missing-count="resolvableMissingCount"
          :resolvable-drift-count="resolvableDriftCount"
          :skipped-accounts-count="skippedAccountsCount"
          :is-bulk-resolving="permissionStates.isBulkResolving?.value ?? false"
          @bulk-resolve="showBulkResolveDialog = true"
        />
      </div>

      <SqlPermissionsAccountsTable
        :accounts="data.overview?.accounts ?? []"
        :status-filter="statusFilter"
        :integration-account-id="integrationAccountId"
        :has-create-permission="hasCreatePermission"
        :has-write-permission="hasWritePermission"
        :can-resolve="canResolve"
        :is-creating-account-id="permissionStates.creatingAccountId?.value ?? null"
        :is-resolving-account-id="permissionStates.resolvingAccountId?.value ?? null"
        :is-importing-account-id="permissionStates.importingAccountId?.value ?? null"
        @update:statusFilter="(value: string) => (statusFilter = value)"
        @view-account="(account) => router.push({ name: 'accountEdit', params: { id: account.accountId } })"
        @create-account="openCreateDialog"
        @resolve-account="openResolveDialog"
        @import-account="openImportDialog"
      />

      <SqlPermissionsOrphanTable
        :principals="data.overview?.orphanPrincipals ?? []"
        :can-resolve="canResolve"
        :is-importing-principal="permissionStates.importingOrphanName?.value ?? null"
        @import-orphan="openImportOrphanDialog"
      />
    </template>

    <ResolveDriftDialog
      v-model="showResolveDialog"
      :account="resolveDialogAccount"
      :is-resolving="isResolvePending"
      @confirm="handleResolveConfirm"
    />
    <ResolveDriftResultDialog
      v-model="showResolveResultDialog"
      :result="resolveResult"
    />

    <CreateSqlAccountDialog
      v-model="showCreateAccountDialog"
      :account="createDialogAccount"
      :default-password-source="createDialogDefaultPasswordSource"
      :has-secret-provider="createDialogHasSecretProvider"
      :is-creating="isCreatePending"
      @submit="handleCreateSubmit"
    />
    <CreateSqlAccountResultDialog
      v-model="showCreateResultDialog"
      :result="createAccountResult"
    />

    <BulkResolveDialog
      v-model="showBulkResolveDialog"
      :resolvable-missing-count="resolvableMissingCount"
      :resolvable-drift-count="resolvableDriftCount"
      :skipped-accounts-count="skippedAccountsCount"
      :is-bulk-resolving="permissionStates.isBulkResolving?.value ?? false"
      @confirm="handleBulkResolveConfirm"
    />
    <BulkResolveResultDialog
      v-model="showBulkResolveResultDialog"
      :result="bulkResolveResult"
    />

    <ImportPermissionsDialog
      v-model="showImportDialog"
      :account="importDialogAccount"
      :is-importing="isImportPending"
      @confirm="handleImportConfirm"
    />
    <ImportPermissionsResultDialog
      v-model="showImportResultDialog"
      :result="importResult"
    />

    <ImportOrphanDialog
      v-model="showImportOrphanDialog"
      :orphan="importSelectedOrphan"
      :is-importing="isImportOrphanPending"
      @submit="handleImportOrphanSubmit"
    />
    <ImportOrphanResultDialog
      v-model="showImportOrphanResultDialog"
      :result="importOrphanResult"
      @view-account="handleViewAccount"
    />
  </div>
</template>

<script setup lang="ts">
import { computed, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useSqlPermissionsOverview } from '../composables/useSqlPermissionsOverview'
import { useSqlIntegrations } from '../composables/useSqlIntegrations'
import { useSqlPermissionsActions } from '../composables/useSqlPermissionsActions'
import { useFuseStore } from '../stores/FuseStore'
import { PasswordSource, SecurityLevel, SyncStatus } from '../api/client'
import type {
  AuthKind,
  SecretBindingKind,
  BulkResolveResponse,
  CreateSqlAccountResponse,
  ImportOrphanPrincipalResponse,
  ImportPermissionsResponse,
  ResolveDriftResponse,
  SqlAccountPermissionsStatus,
  SqlOrphanPrincipal
} from '../api/client'
import SqlPermissionsHeader from '../components/sqlPermissions/SqlPermissionsHeader.vue'
import SqlPermissionsSummaryCard from '../components/sqlPermissions/SqlPermissionsSummaryCard.vue'
import SqlPermissionsBulkActions from '../components/sqlPermissions/SqlPermissionsBulkActions.vue'
import SqlPermissionsAccountsTable from '../components/sqlPermissions/SqlPermissionsAccountsTable.vue'
import SqlPermissionsOrphanTable from '../components/sqlPermissions/SqlPermissionsOrphanTable.vue'
import ResolveDriftDialog from '../components/sqlPermissions/dialogs/ResolveDriftDialog.vue'
import ResolveDriftResultDialog from '../components/sqlPermissions/dialogs/ResolveDriftResultDialog.vue'
import CreateSqlAccountDialog from '../components/sqlPermissions/dialogs/CreateSqlAccountDialog.vue'
import CreateSqlAccountResultDialog from '../components/sqlPermissions/dialogs/CreateSqlAccountResultDialog.vue'
import BulkResolveDialog from '../components/sqlPermissions/dialogs/BulkResolveDialog.vue'
import BulkResolveResultDialog from '../components/sqlPermissions/dialogs/BulkResolveResultDialog.vue'
import ImportPermissionsDialog from '../components/sqlPermissions/dialogs/ImportPermissionsDialog.vue'
import ImportPermissionsResultDialog from '../components/sqlPermissions/dialogs/ImportPermissionsResultDialog.vue'
import ImportOrphanDialog from '../components/sqlPermissions/dialogs/ImportOrphanDialog.vue'
import ImportOrphanResultDialog from '../components/sqlPermissions/dialogs/ImportOrphanResultDialog.vue'

const route = useRoute()
const router = useRouter()
const fuseStore = useFuseStore()

const integrationId = computed(() => route.params.id as string)
const { data, isLoading, isFetching, error, refetch } = useSqlPermissionsOverview(integrationId)
const { data: sqlIntegrations } = useSqlIntegrations()

const {
  resolveAccount,
  createAccount,
  bulkResolveAll,
  importAccountPermissions,
  importOrphanPrincipal,
  hasSecretProvider,
  states: permissionStates
} = useSqlPermissionsActions(integrationId)

const statusFilter = ref('all')

const showResolveDialog = ref(false)
const showResolveResultDialog = ref(false)
const resolveDialogAccount = ref<SqlAccountPermissionsStatus | null>(null)
const resolveResult = ref<ResolveDriftResponse | null>(null)

const showCreateAccountDialog = ref(false)
const showCreateResultDialog = ref(false)
const createDialogAccount = ref<SqlAccountPermissionsStatus | null>(null)
const createAccountResult = ref<CreateSqlAccountResponse | null>(null)

const showBulkResolveDialog = ref(false)
const showBulkResolveResultDialog = ref(false)
const bulkResolveResult = ref<BulkResolveResponse | null>(null)

const showImportDialog = ref(false)
const showImportResultDialog = ref(false)
const importDialogAccount = ref<SqlAccountPermissionsStatus | null>(null)
const importResult = ref<ImportPermissionsResponse | null>(null)

const showImportOrphanDialog = ref(false)
const showImportOrphanResultDialog = ref(false)
const importSelectedOrphan = ref<SqlOrphanPrincipal | null>(null)
const importOrphanResult = ref<ImportOrphanPrincipalResponse | null>(null)

const isResolvePending = computed(() => !!permissionStates.resolvingAccountId?.value)
const isCreatePending = computed(() => !!permissionStates.creatingAccountId?.value)
const isImportPending = computed(() => !!permissionStates.importingAccountId?.value)
const isImportOrphanPending = computed(() => !!permissionStates.importingOrphanName?.value)

const integrationAccountId = computed(() => {
  const integration = sqlIntegrations.value?.find((i) => i.id === integrationId.value)
  return integration?.accountId ?? null
})

const hasWritePermission = computed(() => {
  const integration = sqlIntegrations.value?.find((i) => i.id === integrationId.value)
  if (!integration?.permissions) return false
  return String(integration.permissions).includes('Write')
})

const hasCreatePermission = computed(() => {
  const integration = sqlIntegrations.value?.find((i) => i.id === integrationId.value)
  if (!integration?.permissions) return false
  return String(integration.permissions).includes('Create')
})

const canResolve = computed(() => {
  if (fuseStore.securityLevel === SecurityLevel.None) {
    return true
  }
  return fuseStore.canModify
})

const canBulkResolve = computed(() => {
  return canResolve.value && hasWritePermission.value && hasCreatePermission.value
})

const resolvableMissingCount = computed(() => {
  if (!data.value?.overview?.accounts) return 0
  return data.value.overview.accounts.filter(
    (account) =>
      account.status === SyncStatus.MissingPrincipal &&
      account.accountId &&
      hasSecretProvider(account.accountId)
  ).length
})

const resolvableDriftCount = computed(() => data.value?.overview?.summary?.driftCount ?? 0)

const skippedAccountsCount = computed(() => {
  if (!data.value?.overview?.accounts) return 0
  return data.value.overview.accounts.filter(
    (account) =>
      account.status === SyncStatus.MissingPrincipal &&
      account.accountId &&
      !hasSecretProvider(account.accountId)
  ).length
})

const hasResolvableAccounts = computed(() => resolvableMissingCount.value > 0 || resolvableDriftCount.value > 0)

const createDialogDefaultPasswordSource = computed(() => {
  if (createDialogAccount.value?.accountId && hasSecretProvider(createDialogAccount.value.accountId)) {
    return PasswordSource.SecretProvider
  }
  return PasswordSource.Manual
})

const createDialogHasSecretProvider = computed(() => {
  if (!createDialogAccount.value?.accountId) return false
  return hasSecretProvider(createDialogAccount.value.accountId)
})

function openResolveDialog(account: SqlAccountPermissionsStatus) {
  resolveDialogAccount.value = account
  showResolveDialog.value = true
}

async function handleResolveConfirm() {
  if (!resolveDialogAccount.value) return
  const result = await resolveAccount(resolveDialogAccount.value)
  resolveResult.value = result
  showResolveDialog.value = false
  showResolveResultDialog.value = true
}

function openCreateDialog(account: SqlAccountPermissionsStatus) {
  createDialogAccount.value = account
  showCreateAccountDialog.value = true
}

async function handleCreateSubmit(payload: { passwordSource: PasswordSource; password?: string }) {
  if (!createDialogAccount.value) return
  const result = await createAccount(createDialogAccount.value, payload)
  createAccountResult.value = result
  showCreateAccountDialog.value = false
  showCreateResultDialog.value = true
}

function openImportDialog(account: SqlAccountPermissionsStatus) {
  importDialogAccount.value = account
  showImportDialog.value = true
}

async function handleImportConfirm() {
  if (!importDialogAccount.value) return
  const result = await importAccountPermissions(importDialogAccount.value)
  importResult.value = result
  showImportDialog.value = false
  showImportResultDialog.value = true
}

async function handleBulkResolveConfirm() {
  const result = await bulkResolveAll()
  bulkResolveResult.value = result
  showBulkResolveDialog.value = false
  showBulkResolveResultDialog.value = true
}

function openImportOrphanDialog(orphan: SqlOrphanPrincipal) {
  importSelectedOrphan.value = orphan
  showImportOrphanDialog.value = true
}

async function handleImportOrphanSubmit(payload: {
  authKind: AuthKind
  secretBindingKind: SecretBindingKind
  plainReference?: string
}) {
  if (!importSelectedOrphan.value) return
  const result = await importOrphanPrincipal(importSelectedOrphan.value, payload)
  importOrphanResult.value = result
  showImportOrphanDialog.value = false
  showImportOrphanResultDialog.value = true
}

function handleViewAccount(accountId?: string | null) {
  if (!accountId) return
  router.push({ name: 'accountEdit', params: { id: accountId } })
}
</script>

<style scoped>
@import '../styles/pages.css';
</style>
