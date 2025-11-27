<template>
  <q-expansion-item 
    dense 
    expand-icon="expand_more" 
    icon="sync" 
    label="SQL Status" 
    class="q-mt-lg"
    :default-opened="true"
  >
    <template #default>
      <div class="section-header">
        <div>
          <div class="text-subtitle1">SQL Permission Status</div>
          <div class="text-caption text-grey-7">
            Compare configured grants with actual SQL permissions.
          </div>
        </div>
        <q-btn
          flat
          dense
          icon="refresh"
          label="Refresh"
          :loading="isFetching"
          @click="() => refetch()"
        />
      </div>

      <!-- Loading State -->
      <div v-if="isLoading" class="q-pa-md text-center">
        <q-spinner color="primary" size="2em" />
        <div class="text-grey-7 q-mt-sm">Loading SQL status...</div>
      </div>

      <!-- Error State -->
      <q-banner v-else-if="error" dense class="bg-red-1 text-negative q-mt-md">
        <template #avatar>
          <q-icon name="error" color="negative" />
        </template>
        Unable to load SQL status. Please try again.
      </q-banner>

      <!-- Data Display -->
      <template v-else-if="data">
        <!-- Status Summary -->
        <div class="status-summary q-mt-md q-pa-sm rounded-borders" :class="statusClass">
          <div class="row items-center q-gutter-sm">
            <q-icon :name="statusIcon" size="sm" :color="statusColor" />
            <div>
              <div class="text-weight-medium">{{ statusLabel }}</div>
              <div class="text-caption">{{ data.statusSummary }}</div>
            </div>
          </div>
          <q-badge 
            v-if="data.sqlIntegrationName" 
            outline 
            color="primary" 
            class="q-mt-sm"
          >
            {{ data.sqlIntegrationName }}
          </q-badge>
        </div>

        <!-- Error Message from SQL inspection -->
        <q-banner v-if="data.errorMessage" dense class="bg-orange-1 text-orange-9 q-mt-md">
          <template #avatar>
            <q-icon name="warning" color="orange" />
          </template>
          {{ data.errorMessage }}
        </q-banner>

        <!-- Permission Comparison Table -->
        <div v-if="hasComparisons" class="q-mt-md">
          <div class="text-subtitle2 q-mb-sm">Permission Details</div>
          <q-table
            flat
            bordered
            dense
            :rows="comparisonRows"
            :columns="columns"
            row-key="key"
          >
            <template #body-cell-configured="props">
              <q-td :props="props">
                <div v-if="props.row.configuredPrivileges?.length" class="tag-list">
                  <q-badge
                    v-for="priv in props.row.configuredPrivileges"
                    :key="priv"
                    outline
                    color="primary"
                    :label="priv"
                  />
                </div>
                <span v-else class="text-grey">—</span>
              </q-td>
            </template>
            <template #body-cell-actual="props">
              <q-td :props="props">
                <div v-if="props.row.actualPrivileges?.length" class="tag-list">
                  <q-badge
                    v-for="priv in props.row.actualPrivileges"
                    :key="priv"
                    outline
                    color="secondary"
                    :label="priv"
                  />
                </div>
                <span v-else class="text-grey">—</span>
              </q-td>
            </template>
            <template #body-cell-status="props">
              <q-td :props="props">
                <div class="row items-center q-gutter-xs">
                  <template v-if="props.row.hasMissing">
                    <q-icon name="remove_circle" color="negative" size="xs" />
                    <span class="text-negative text-caption">
                      Missing: {{ props.row.missingPrivileges.join(', ') }}
                    </span>
                  </template>
                  <template v-if="props.row.hasExtra">
                    <q-icon name="add_circle" color="orange" size="xs" />
                    <span class="text-orange text-caption">
                      Extra: {{ props.row.extraPrivileges.join(', ') }}
                    </span>
                  </template>
                  <template v-if="!props.row.hasMissing && !props.row.hasExtra">
                    <q-icon name="check_circle" color="positive" size="xs" />
                    <span class="text-positive text-caption">In sync</span>
                  </template>
                </div>
              </q-td>
            </template>
            <template #no-data>
              <div class="q-pa-sm text-grey-7">No permission data available.</div>
            </template>
          </q-table>
        </div>
      </template>
    </template>
  </q-expansion-item>
</template>

<script setup lang="ts">
import { computed } from 'vue'
import type { QTableColumn } from 'quasar'
import { SyncStatus, type SqlPermissionComparison } from '../../api/client'
import { useAccountSqlStatus } from '../../composables/useAccountSqlStatus'

interface ComparisonRow {
  key: string
  database: string
  schema: string
  configuredPrivileges: string[]
  actualPrivileges: string[]
  missingPrivileges: string[]
  extraPrivileges: string[]
  hasMissing: boolean
  hasExtra: boolean
}

const props = defineProps<{
  accountId: string
}>()

const { data, isLoading, isFetching, error, refetch } = useAccountSqlStatus(
  computed(() => props.accountId)
)

const statusLabel = computed(() => {
  if (!data.value) return 'Unknown'
  switch (data.value.status) {
    case SyncStatus.InSync:
      return 'In Sync'
    case SyncStatus.DriftDetected:
      return 'Drift Detected'
    case SyncStatus.Error:
      return 'Error'
    case SyncStatus.NotApplicable:
      return 'Not Applicable'
    default:
      return 'Unknown'
  }
})

const statusIcon = computed(() => {
  if (!data.value) return 'help'
  switch (data.value.status) {
    case SyncStatus.InSync:
      return 'check_circle'
    case SyncStatus.DriftDetected:
      return 'warning'
    case SyncStatus.Error:
      return 'error'
    case SyncStatus.NotApplicable:
      return 'info'
    default:
      return 'help'
  }
})

const statusColor = computed(() => {
  if (!data.value) return 'grey'
  switch (data.value.status) {
    case SyncStatus.InSync:
      return 'positive'
    case SyncStatus.DriftDetected:
      return 'warning'
    case SyncStatus.Error:
      return 'negative'
    case SyncStatus.NotApplicable:
      return 'grey'
    default:
      return 'grey'
  }
})

const statusClass = computed(() => {
  if (!data.value) return 'status-na'
  switch (data.value.status) {
    case SyncStatus.InSync:
      return 'status-in-sync'
    case SyncStatus.DriftDetected:
      return 'status-drift'
    case SyncStatus.Error:
      return 'status-error'
    case SyncStatus.NotApplicable:
      return 'status-na'
    default:
      return 'status-na'
  }
})

const hasComparisons = computed(() => {
  return data.value?.permissionComparisons && data.value.permissionComparisons.length > 0
})

const columns: QTableColumn<ComparisonRow>[] = [
  { name: 'database', label: 'Database', field: 'database', align: 'left' },
  { name: 'schema', label: 'Schema', field: 'schema', align: 'left' },
  { name: 'configured', label: 'Configured', field: 'configuredPrivileges', align: 'left' },
  { name: 'actual', label: 'Actual (SQL)', field: 'actualPrivileges', align: 'left' },
  { name: 'status', label: 'Status', field: (row) => row.key, align: 'left' }
]

const comparisonRows = computed<ComparisonRow[]>(() => {
  if (!data.value?.permissionComparisons) return []
  return data.value.permissionComparisons.map((comp: SqlPermissionComparison, index: number) => ({
    key: `perm-${index}-${comp.database ?? ''}-${comp.schema ?? ''}`,
    database: comp.database ?? '—',
    schema: comp.schema ?? '—',
    configuredPrivileges: comp.configuredPrivileges ?? [],
    actualPrivileges: comp.actualPrivileges ?? [],
    missingPrivileges: comp.missingPrivileges ?? [],
    extraPrivileges: comp.extraPrivileges ?? [],
    hasMissing: (comp.missingPrivileges?.length ?? 0) > 0,
    hasExtra: (comp.extraPrivileges?.length ?? 0) > 0
  }))
})
</script>

<style scoped>
.section-header {
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
  gap: 0.5rem;
}

.status-summary {
  display: flex;
  flex-direction: column;
  gap: 0.25rem;
  background: var(--fuse-status-bg);
  border: 1px solid var(--fuse-panel-border);
  transition: background 0.2s ease;
}

.status-summary.status-in-sync { --fuse-status-bg: var(--fuse-status-in-sync-bg); }
.status-summary.status-drift { --fuse-status-bg: var(--fuse-status-drift-bg); }
.status-summary.status-error { --fuse-status-bg: var(--fuse-status-error-bg); }
.status-summary.status-na { --fuse-status-bg: var(--fuse-status-na-bg); }

/* Ensure readable selection inside status summary */
.status-summary ::selection {
  background: var(--fuse-selection-bg);
  color: var(--fuse-selection-color);
}

.tag-list {
  display: flex;
  flex-wrap: wrap;
  gap: 0.25rem;
}
</style>
