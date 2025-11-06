<template>
  <q-expansion-item dense expand-icon="expand_more" icon="security" label="Grants" class="q-mt-lg">
    <template #default>
      <div class="section-header">
        <div>
          <div class="text-subtitle1">Account Grants</div>
          <div class="text-caption text-grey-7">Document permissions granted to this account.</div>
        </div>
        <q-btn
          color="primary"
          label="Add Grant"
          dense
          icon="add"
          :disable="disableActions"
          @click="emit('add')"
        />
      </div>
      <q-table
        flat
        bordered
        dense
        :rows="rows"
        :columns="columns"
        row-key="__key"
        class="q-mt-md"
      >
        <template #body-cell-privileges="props">
          <q-td :props="props">
            <div v-if="props.row.privileges?.length" class="tag-list">
              <q-badge
                v-for="privilege in props.row.privileges"
                :key="privilege"
                outline
                color="secondary"
                :label="privilege"
              />
            </div>
            <span v-else class="text-grey">—</span>
          </q-td>
        </template>
        <template #body-cell-actions="props">
          <q-td :props="props" class="text-right">
            <q-btn
              dense
              flat
              round
              icon="edit"
              color="primary"
              :disable="disableActions"
              @click="emit('edit', { grant: props.row.__source, index: props.row.__index })"
            />
            <q-btn
              dense
              flat
              round
              icon="delete"
              color="negative"
              class="q-ml-xs"
              :disable="disableActions"
              @click="emit('delete', { grant: props.row.__source, index: props.row.__index })"
            />
          </q-td>
        </template>
        <template #no-data>
          <div class="q-pa-sm text-grey-7">No grants defined.</div>
        </template>
      </q-table>
    </template>
  </q-expansion-item>
</template>

<script setup lang="ts">
import { computed } from 'vue'
import type { QTableColumn } from 'quasar'
import type { Grant, Privilege } from '../../api/client'

interface GrantRow {
  __key: string
  __index: number
  __source: Grant
  id?: string
  database?: string
  schema?: string
  privileges?: Privilege[]
}

const props = withDefaults(
  defineProps<{
    grants: readonly Grant[]
    disableActions?: boolean
  }>(),
  {
    grants: () => [],
    disableActions: false
  }
)

const emit = defineEmits<{
  (e: 'add'): void
  (e: 'edit', payload: { grant: Grant; index: number }): void
  (e: 'delete', payload: { grant: Grant; index: number }): void
}>()

const columns: QTableColumn<GrantRow>[] = [
  { name: 'database', label: 'Database', field: 'database', align: 'left' },
  { name: 'schema', label: 'Schema', field: 'schema', align: 'left' },
  { name: 'privileges', label: 'Privileges', field: 'privileges', align: 'left' },
  { name: 'actions', label: '', field: (row) => row.id, align: 'right' }
]

const rows = computed<GrantRow[]>(() =>
  props.grants.map((grant, index) => ({
    ...grant,
    __key: grant.id ?? `grant-${index}`,
    __index: index,
    __source: grant
  }))
)
</script>

<style scoped>
.section-header {
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
  gap: 0.5rem;
}

.tag-list {
  display: flex;
  flex-wrap: wrap;
  gap: 0.25rem;
}
</style>
