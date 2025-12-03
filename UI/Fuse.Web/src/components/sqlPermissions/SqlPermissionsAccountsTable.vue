<template>
  <q-card class="content-card">
    <q-card-section>
      <div class="text-h6 q-mb-sm">Account Permissions</div>
      <div class="row q-gutter-sm q-mb-md">
        <q-btn-toggle
          :model-value="statusFilter"
          flat
          dense
          toggle-color="primary"
          :options="filterOptions"
          @update:model-value="(value: string) => emit('update:statusFilter', value)"
        />
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
            <div class="row items-center no-wrap">
              <router-link
                :to="{ name: 'accountEdit', params: { id: props.row.accountId } }"
                class="account-link"
              >
                {{ props.row.accountName ?? '—' }}
              </router-link>
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
          </q-td>
        </template>
        <template #body-cell-permissions="props">
          <q-td :props="props">
            <PermissionDiffSummary
              v-if="props.row.permissionComparisons?.length"
              :missing-count="countMissingPermissions(props.row)"
              :extra-count="countExtraPermissions(props.row)"
              :show-all-in-sync="props.row.status === SyncStatus.InSync"
            />
            <span v-else class="text-grey">—</span>
          </q-td>
        </template>
        <template #body-cell-actions="props">
          <q-td :props="props" class="text-right">
            <q-btn
              v-if="props.row.status === SyncStatus.MissingPrincipal && hasCreatePermission && canResolve"
              flat
              dense
              size="sm"
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
              size="sm"
              color="primary"
              label="Resolve"
              :loading="isResolvingAccountId === props.row.accountId"
              @click="emit('resolve-account', props.row)"
            >
              <q-tooltip>Apply Fuse configuration to SQL</q-tooltip>
            </q-btn>
            <q-btn
              v-if="props.row.status === SyncStatus.DriftDetected && canResolve"
              flat
              dense
              size="sm"
              color="secondary"
              label="Import"
              :loading="isImportingAccountId === props.row.accountId"
              @click="emit('import-account', props.row)"
            >
              <q-tooltip>Import SQL permissions to Fuse</q-tooltip>
            </q-btn>
            <q-btn
              flat
              dense
              round
              icon="visibility"
              color="primary"
              @click="emit('view-account', props.row)"
            >
              <q-tooltip>View account details</q-tooltip>
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
import { SyncStatus, type SqlAccountPermissionsStatus } from '../../api/client'
import PermissionDiffSummary from './PermissionDiffSummary.vue'
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
  (e: 'view-account', account: SqlAccountPermissionsStatus): void
  (e: 'create-account', account: SqlAccountPermissionsStatus): void
  (e: 'resolve-account', account: SqlAccountPermissionsStatus): void
  (e: 'import-account', account: SqlAccountPermissionsStatus): void
}>()

const filterOptions = [
  { label: 'All', value: 'all' },
  { label: 'In Sync', value: 'InSync' },
  { label: 'Drift', value: 'DriftDetected' },
  { label: 'Missing', value: 'MissingPrincipal' },
  { label: 'Error', value: 'Error' }
]

const accountColumns: QTableColumn<SqlAccountPermissionsStatus>[] = [
  { name: 'accountName', label: 'Account', field: 'accountName', align: 'left', sortable: true },
  { name: 'principalName', label: 'SQL Principal', field: 'principalName', align: 'left', sortable: true },
  { name: 'status', label: 'Status', field: 'status', align: 'left', sortable: true },
  { name: 'permissions', label: 'Permission Summary', field: (row) => row.accountId, align: 'left' },
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

function countMissingPermissions(account: SqlAccountPermissionsStatus): number {
  return account.permissionComparisons?.reduce((sum, c) => sum + (c.missingPrivileges?.length ?? 0), 0) ?? 0
}

function countExtraPermissions(account: SqlAccountPermissionsStatus): number {
  return account.permissionComparisons?.reduce((sum, c) => sum + (c.extraPrivileges?.length ?? 0), 0) ?? 0
}
</script>

<style scoped>
.account-link {
  color: var(--q-primary);
  text-decoration: none;
}

.account-link:hover {
  text-decoration: underline;
}
</style>
