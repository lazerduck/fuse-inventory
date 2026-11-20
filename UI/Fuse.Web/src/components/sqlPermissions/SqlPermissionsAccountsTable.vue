<template>
  <q-card class="content-card">
    <q-card-section>
      <h2 class="text-h6 q-ma-none">Account permissions</h2>
      <div class="filters q-my-md" role="group" aria-label="Filter by database grant status">
        <button v-for="option in filterOptions" :key="option.value" type="button"
          :aria-pressed="statusFilter === option.value" @click="emit('update:statusFilter', option.value)">
          {{ option.label }}
        </button>
      </div>
      <q-table
        flat
        bordered
        :rows="filteredAccounts"
        :columns="accountColumns"
        row-key="accountId"
        :pagination="{ rowsPerPage: 15 }"
      >
        <template #body-cell-accountName="props">
          <q-td :props="props">
            <div class="account-name">
              <router-link
                :to="{ name: 'accountEdit', params: { id: props.row.accountId } }"
                class="account-link"
              >
                {{ props.row.accountName || props.row.principalName || '—' }}
              </router-link>
              <small v-if="props.row.principalName && props.row.accountName && props.row.principalName !== props.row.accountName && !props.row.accountName.startsWith(props.row.principalName + ' @ ')">{{ props.row.principalName }}</small>
              <q-badge
                v-if="isIntegrationAccount(props.row.accountId)"
                color="blue"
                label="Integration Account"
                class="q-ml-sm"
              >
                <q-tooltip>
                  This account is used by the SQL integration for authentication.
                </q-tooltip>
              </q-badge>
            </div>
          </q-td>
        </template>
        <template #body-cell-status="props">
          <q-td :props="props">
            <StatusBadge :status="props.row.status" />
            <PermissionDiffSummary v-if="props.row.status === SyncStatus.DriftDetected"
              :missing-count="countMissingPermissions(props.row)" :extra-count="countExtraPermissions(props.row)" />
            <small v-if="needsReview(props.row)" class="muted">Manual review</small>
          </q-td>
        </template>
        <template #body-cell-permissions="props">
          <q-td :props="props">
            <div v-if="props.row.errorMessage && props.row.errorMessage !== props.row.coverageWarning" class="text-negative" role="alert">
              {{ props.row.errorMessage }}
            </div>
            <SqlAccessCoverage v-if="props.row.coverage?.length || props.row.coverageWarning || props.row.status === SyncStatus.InSync || props.row.status === SyncStatus.DriftDetected"
              :coverage="props.row.coverage" :warning="props.row.coverageWarning" :principal="props.row.principalName" />
            <span v-else-if="!props.row.errorMessage" class="muted">—</span>
          </q-td>
        </template>
        <template #body-cell-actions="props">
          <q-td :props="props" class="text-right">
            <q-btn
              v-if="props.row.status === SyncStatus.MissingPrincipal && hasCreatePermission && canResolve"
              flat
              dense
              no-caps
              class="table-action"
              color="positive"
              label="Create Account"
              :loading="isCreatingAccountId === props.row.accountId"
              @click="emit('create-account', props.row)"
            >
              <q-tooltip>Create SQL login and user</q-tooltip>
            </q-btn>
            <q-btn
              v-if="props.row.status === SyncStatus.DriftDetected && hasWritePermission && canResolve"
              flat
              dense
              no-caps
              class="table-action"
              color="primary"
              label="Resolve"
              :disable="!hasResolvableDrift(props.row)"
              :loading="isResolvingAccountId === props.row.accountId"
              @click="emit('resolve-account', props.row)"
            >
              <q-tooltip>{{ hasResolvableDrift(props.row) ? 'Apply whole-database grants in SQL' : 'These grants require manual review in SQL Server' }}</q-tooltip>
            </q-btn>
            <q-btn
              v-if="props.row.status === SyncStatus.DriftDetected && canResolve && hasResolvableDrift(props.row)"
              flat
              dense
              no-caps
              class="table-action"
              color="secondary"
              label="Import"
              :loading="isImportingAccountId === props.row.accountId"
              @click="emit('import-account', props.row)"
            >
              <q-tooltip>Import SQL permissions to Fuse</q-tooltip>
            </q-btn>

          </q-td>
        </template>
        <template #no-data>
          <div class="q-pa-md text-grey-7">
            <template v-if="statusFilter !== 'all'">
              No accounts match the selected filter.
            </template>
            <template v-else>
              No accounts are associated with this SQL integration.
            </template>
          </div>
        </template>
      </q-table>
    </q-card-section>
  </q-card>
</template>

<script setup lang="ts">
import { computed } from 'vue'
import type { QTableColumn } from 'quasar'
import { SyncStatus, type SqlAccountPermissionsStatus } from 'api/client'
import PermissionDiffSummary from './PermissionDiffSummary.vue'
import SqlAccessCoverage from './SqlAccessCoverage.vue'
import StatusBadge from './StatusBadge.vue'

interface Props {
  accounts?: SqlAccountPermissionsStatus[] | null
  statusFilter: string
  integrationAccountId?: string | null
  hasCreatePermission: boolean
  hasWritePermission: boolean
  canResolve: boolean
  isCreatingAccountId?: string | null
  isResolvingAccountId?: string | null
  isImportingAccountId?: string | null
}

const props = withDefaults(defineProps<Props>(), {
  accounts: () => [],
  integrationAccountId: null,
  isCreatingAccountId: null,
  isResolvingAccountId: null,
  isImportingAccountId: null
})

const emit = defineEmits<{
  (e: 'update:statusFilter', value: string): void
  (e: 'create-account', account: SqlAccountPermissionsStatus): void
  (e: 'resolve-account', account: SqlAccountPermissionsStatus): void
  (e: 'import-account', account: SqlAccountPermissionsStatus): void
}>()

const filterOptions = [
  { label: 'All', value: 'all' },
  { label: 'In Sync', value: 'InSync' },
  { label: 'Drift', value: 'DriftDetected' },
  { label: 'Missing', value: 'MissingPrincipal' },
  { label: 'Error', value: 'Error' },
  { label: 'Unknown', value: 'Unknown' }
]

const accountColumns: QTableColumn<SqlAccountPermissionsStatus>[] = [
  { name: 'accountName', label: 'Account', field: 'accountName', align: 'left', sortable: true },
  { name: 'permissions', label: 'Observed access', field: (row) => row.accountId, align: 'left' },
  { name: 'status', label: 'Database grants', field: 'status', align: 'left', sortable: true },
  { name: 'actions', label: '', field: (row) => row.accountId, align: 'right' }
]

const filteredAccounts = computed(() => {
  if (props.statusFilter === 'all') {
    return props.accounts ?? []
  }
  return (props.accounts ?? []).filter(a => a.status === props.statusFilter)
})

function isIntegrationAccount(accountId?: string | null): boolean {
  if (!accountId || !props.integrationAccountId) return false
  return accountId === props.integrationAccountId
}

function needsReview(account: SqlAccountPermissionsStatus) {
  return account.permissionComparisons?.some(c => c.blockedPrivileges?.length)
}

function hasResolvableDrift(account: SqlAccountPermissionsStatus) {
  return account.permissionComparisons?.some(c => [...(c.missingPrivileges ?? []), ...(c.extraPrivileges ?? [])]
    .some(privilege => !c.blockedPrivileges?.includes(privilege)))
}

function countMissingPermissions(account: SqlAccountPermissionsStatus): number {
  return account.permissionComparisons?.reduce((sum, c) => sum + (c.missingPrivileges?.length ?? 0), 0) ?? 0
}

function countExtraPermissions(account: SqlAccountPermissionsStatus): number {
  return account.permissionComparisons?.reduce((sum, c) => sum + (c.extraPrivileges?.length ?? 0), 0) ?? 0
}
</script>

<style scoped>
.table-action { min-height: 32px; min-width: 48px; padding: 0 .5rem; font-size: .82rem; }
.table-action + .table-action { margin-inline-start: .25rem; }
.account-name { display: grid; justify-items: start; gap: .2rem; white-space: normal; overflow-wrap: anywhere; }
.account-name small, .muted { color: var(--fuse-text-muted); }
.filters { display: flex; flex-wrap: wrap; gap: .25rem; }
.filters button { border: 1px solid transparent; background: transparent; color: var(--fuse-text-muted); font: inherit; font-size: .82rem; cursor: pointer; border-radius: 4px; padding: .25rem .65rem; }
.filters button:hover, .filters button:focus-visible { color: var(--fuse-text); background: var(--fuse-hover-bg); }
.filters button[aria-pressed="true"] { color: var(--q-primary); border-color: var(--fuse-panel-border); background: var(--fuse-panel-bg); font-weight: 650; }
:deep(td) { padding-top: .65rem; padding-bottom: .65rem; }
:deep(td:nth-child(2)) { min-width: 13rem; max-width: 30rem; }
.account-link {
  color: var(--q-primary);
  text-decoration: none;
}

.account-link:hover {
  text-decoration: underline;
}
@media (max-width: 600px) {
  :deep(.q-table thead) { position: absolute; width: 1px; height: 1px; overflow: hidden; clip-path: inset(50%); }
  :deep(.q-table), :deep(.q-table tbody) { display: block; }
  :deep(.q-table tbody tr) { display: grid; grid-template-columns: minmax(0, 1fr) auto; gap: .6rem; height: auto; padding: .8rem; }
  :deep(.q-table tbody tr + tr) { border-top: 1px solid var(--fuse-panel-border); }
  :deep(.q-table tbody td) { height: auto; min-width: 0; max-width: none; padding: 0; border: 0; }
  :deep(td:nth-child(2)) { grid-column: 1 / -1; grid-row: 2; }
  :deep(td:nth-child(3)) { grid-column: 2; grid-row: 1; text-align: right; }
  :deep(td:last-child) { grid-column: 1 / -1; justify-self: start; }
  :deep(td:last-child:not(:has(button))) { display: none; }
}
</style>
