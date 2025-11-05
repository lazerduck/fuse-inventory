<template>
  <q-card class="content-card">
    <q-table
      flat
      bordered
      :rows="rows"
      :columns="columns"
      row-key="id"
      :loading="loading"
      :pagination="pagination"
    >
      <template #body-cell-target="props">
        <q-td :props="props">
          {{ targetResolver(props.row) }}
        </q-td>
      </template>
      <template #body-cell-tags="props">
        <q-td :props="props">
          <div v-if="props.row.tagIds?.length" class="tag-list">
            <q-badge
              v-for="tagId in props.row.tagIds"
              :key="tagId"
              outline
              color="primary"
              :label="tagLookup[tagId] ?? tagId"
            />
          </div>
          <span v-else class="text-grey">—</span>
        </q-td>
      </template>
      <template #body-cell-grants="props">
        <q-td :props="props">
          <q-badge color="secondary" :label="`${props.row.grants?.length ?? 0} grants`" />
        </q-td>
      </template>
      <template #body-cell-actions="props">
        <q-td :props="props" class="text-right">
          <q-btn flat dense round icon="edit" color="primary" @click="emit('edit', props.row)" />
          <q-btn
            flat
            dense
            round
            icon="delete"
            color="negative"
            class="q-ml-xs"
            @click="emit('delete', props.row)"
          />
        </q-td>
      </template>
      <template #no-data>
        <div class="q-pa-md text-grey-7">No accounts configured.</div>
      </template>
    </q-table>
  </q-card>
</template>

<script setup lang="ts">
import { computed } from 'vue'
import type { QTableColumn } from 'quasar'
import type { Account } from '../../api/client'

interface Props {
  accounts: Account[]
  loading: boolean
  pagination: { rowsPerPage: number }
  tagLookup: Record<string, string | undefined>
  targetResolver: (account: Account) => string
}

const props = defineProps<Props>()
const emit = defineEmits<{ (event: 'edit', account: Account): void; (event: 'delete', account: Account): void }>()

const rows = computed(() => props.accounts ?? [])

const columns: QTableColumn<Account>[] = [
  { name: 'target', label: 'Target', field: 'targetId', align: 'left', sortable: true },
  { name: 'authKind', label: 'Auth Kind', field: 'authKind', align: 'left' },
  { name: 'userName', label: 'Username', field: 'userName', align: 'left' },
  { name: 'grants', label: 'Grants', field: 'grants', align: 'left' },
  { name: 'tags', label: 'Tags', field: 'tagIds', align: 'left' },
  { name: 'actions', label: '', field: (row) => row.id, align: 'right' }
]
</script>

<style scoped>
.content-card {
  flex: 1;
}

.tag-list {
  display: flex;
  flex-wrap: wrap;
  gap: 0.25rem;
}
</style>
