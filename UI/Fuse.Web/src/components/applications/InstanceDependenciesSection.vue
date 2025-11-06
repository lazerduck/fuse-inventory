<template>
  <q-expansion-item dense expand-icon="expand_more" icon="link" label="Dependencies" class="q-mt-md">
    <template #default>
      <div class="section-header">
        <div>
          <div class="text-subtitle1">Service Dependencies</div>
          <div class="text-caption text-grey-7">
            Describe downstream systems this instance relies on.
          </div>
        </div>
        <q-btn
          color="primary"
          label="Add Dependency"
          dense
          icon="add"
          :disable="disableActions"
          @click="emit('add')"
        />
      </div>
      <q-table flat bordered dense :rows="rows" :columns="columns" row-key="__key" class="q-mt-md">
        <template #body-cell-target="props">
          <q-td :props="props">
            {{ resolveTargetName(props.row.__source) }}
          </q-td>
        </template>
        <template #body-cell-account="props">
          <q-td :props="props">
            {{ resolveAccountName(props.row.accountId) }}
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
              @click="emit('edit', { dependency: props.row.__source, index: props.row.__index })"
            />
            <q-btn
              dense
              flat
              round
              icon="delete"
              color="negative"
              class="q-ml-xs"
              :disable="disableActions"
              @click="emit('delete', { dependency: props.row.__source, index: props.row.__index })"
            />
          </q-td>
        </template>
        <template #no-data>
          <div class="q-pa-sm text-grey-7">No dependencies documented.</div>
        </template>
      </q-table>
    </template>
  </q-expansion-item>
</template>

<script setup lang="ts">
import { computed } from 'vue'
import type { QTableColumn } from 'quasar'
import type { ApplicationInstanceDependency, TargetKind } from '../../api/client'

interface DependencyRow {
  __key: string
  __index: number
  __source: ApplicationInstanceDependency
  id?: string
  targetId?: string
  targetKind?: TargetKind
  port?: number
  accountId?: string
}

const props = withDefaults(
  defineProps<{
    dependencies: readonly ApplicationInstanceDependency[]
    disableActions?: boolean
    resolveTargetName: (dependency: ApplicationInstanceDependency) => string
    resolveAccountName: (accountId?: string | null) => string
  }>(),
  {
    dependencies: () => [],
    disableActions: false
  }
)

const emit = defineEmits<{
  (e: 'add'): void
  (e: 'edit', payload: { dependency: ApplicationInstanceDependency; index: number }): void
  (e: 'delete', payload: { dependency: ApplicationInstanceDependency; index: number }): void
}>()

const columns: QTableColumn<DependencyRow>[] = [
  { name: 'target', label: 'Target', field: 'targetId', align: 'left' },
  { name: 'targetKind', label: 'Kind', field: 'targetKind', align: 'left' },
  { name: 'port', label: 'Port', field: 'port', align: 'left' },
  { name: 'account', label: 'Account', field: 'accountId', align: 'left' },
  { name: 'actions', label: '', field: (row) => row.id, align: 'right' }
]

const rows = computed<DependencyRow[]>(() =>
  props.dependencies.map((dependency, index) => ({
    ...dependency,
    __key: dependency.id ?? `dependency-${index}`,
    __index: index,
    __source: dependency
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
</style>
