<template>
  <q-card v-if="principals?.length" class="content-card q-mt-md">
    <q-card-section>
      <div class="text-h6 q-mb-sm">
        <q-icon name="help_outline" class="q-mr-sm" />
        Unmanaged SQL Accounts
      </div>
      <div class="text-caption text-grey-7 q-mb-md">
        SQL principals found that are not managed by any Fuse account. Import them to start managing their permissions.
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
            <div v-if="props.row.actualPermissions?.length" class="tag-list">
              <template v-for="grant in props.row.actualPermissions" :key="grant.database">
                <q-badge
                  v-for="priv in grant.privileges"
                  :key="`${grant.database}-${priv}`"
                  outline
                  color="secondary"
                  :label="`${grant.database ?? 'default'}:${priv}`"
                />
              </template>
            </div>
            <span v-else class="text-grey">—</span>
          </q-td>
        </template>
        <template #body-cell-actions="props">
          <q-td :props="props" class="text-right">
            <q-btn
              v-if="canResolve"
              flat
              dense
              size="sm"
              color="primary"
              label="Import"
              :loading="isImportingPrincipal === props.row.principalName"
              @click="emit('import-orphan', props.row)"
            >
              <q-tooltip>Create Fuse account from SQL principal</q-tooltip>
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
import { computed } from 'vue'
import type { QTableColumn } from 'quasar'
import type { SqlOrphanPrincipal } from '../../api/client'

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
  { name: 'permissions', label: 'Actual Permissions', field: (row) => row.principalName, align: 'left' },
  { name: 'actions', label: '', field: (row) => row.principalName, align: 'right' }
]
const isImportingPrincipal = computed(() => props.isImportingPrincipal)
</script>

<style scoped>
.tag-list {
  display: flex;
  flex-wrap: wrap;
  gap: 0.25rem;
}
</style>
