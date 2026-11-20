<template>
  <q-card v-if="principals?.length" class="content-card">
    <q-card-section>
      <h2 class="text-h6 q-ma-none q-mb-xs">Unmanaged SQL accounts</h2>
      <div class="text-caption muted q-mb-md">
        Import a SQL principal to document its whole-database grants in Fuse.
      </div>
      <q-table
        flat
        bordered
        :rows="principals"
        :columns="columns"
        row-key="principalName"
        :pagination="{ rowsPerPage: 10 }"
      >
        <template #body-cell-permissions="props">
          <q-td :props="props">
            <div v-if="props.row.errorMessage && props.row.errorMessage !== props.row.coverageWarning" class="text-negative q-mb-sm" role="alert">
              {{ props.row.errorMessage }}
            </div>
            <SqlAccessCoverage v-if="!props.row.errorMessage || props.row.coverage?.length || props.row.coverageWarning"
              :coverage="props.row.coverage" :warning="props.row.coverageWarning" :principal="props.row.principalName" />
          </q-td>
        </template>
        <template #body-cell-actions="props">
          <q-td :props="props" class="text-right">
            <q-btn
              v-if="canResolve"
              flat
              dense
              no-caps
              class="table-action"
              color="primary"
              label="Import"
              :loading="isImportingPrincipal === props.row.principalName"
              :disable="!!(props.row.errorMessage || props.row.coverageWarning)"
              @click="emit('import-orphan', props.row)"
            >
              <q-tooltip>{{ props.row.errorMessage || props.row.coverageWarning || 'Create Fuse account from SQL principal' }}</q-tooltip>
            </q-btn>
          </q-td>
        </template>
        <template #no-data>
          <div class="q-pa-md text-grey-7">
            No orphan principals detected.
          </div>
        </template>
      </q-table>
    </q-card-section>
  </q-card>
</template>

<script setup lang="ts">
import SqlAccessCoverage from './SqlAccessCoverage.vue'
import { computed } from 'vue'
import type { QTableColumn } from 'quasar'
import type { SqlOrphanPrincipal } from 'api/client'

interface Props {
  principals?: SqlOrphanPrincipal[] | null
  canResolve: boolean
  isImportingPrincipal?: string | null
}

const props = withDefaults(defineProps<Props>(), {
  principals: () => [],
  isImportingPrincipal: null
})

const emit = defineEmits<{
  (e: 'import-orphan', orphan: SqlOrphanPrincipal): void
}>()

const columns: QTableColumn<SqlOrphanPrincipal>[] = [
  { name: 'principalName', label: 'Principal Name', field: 'principalName', align: 'left', sortable: true },
  { name: 'permissions', label: 'Observed access', field: (row) => row.principalName, align: 'left' },
  { name: 'actions', label: '', field: (row) => row.principalName, align: 'right' }
]
const isImportingPrincipal = computed(() => props.isImportingPrincipal)
</script>

<style scoped>
.table-action { min-height: 32px; padding: 0 .5rem; font-size: .82rem; }
.muted { color: var(--fuse-text-muted); }
:deep(td:nth-child(2)) { min-width: 13rem; }
@media (max-width: 600px) {
  :deep(.q-table thead) { position: absolute; width: 1px; height: 1px; overflow: hidden; clip-path: inset(50%); }
  :deep(.q-table), :deep(.q-table tbody) { display: block; }
  :deep(.q-table tbody tr) { display: grid; grid-template-columns: minmax(0, 1fr) auto; gap: .55rem; height: auto; padding: .8rem; }
  :deep(.q-table tbody tr + tr) { border-top: 1px solid var(--fuse-panel-border); }
  :deep(.q-table tbody td) { height: auto; min-width: 0; padding: 0; border: 0; }
  :deep(td:first-child) { overflow-wrap: anywhere; white-space: normal; font-weight: 600; }
  :deep(td:nth-child(2)) { grid-column: 1 / -1; grid-row: 2; }
  :deep(td:last-child) { grid-column: 2; grid-row: 1; }
}
</style>
