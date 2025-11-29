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
      :filter="filter"
    >
      <template #top-right>
        <q-input
          v-model="filter"
          dense
          outlined
          debounce="300"
          placeholder="Search..."
        >
          <template #append>
            <q-icon name="search" />
          </template>
        </q-input>
      </template>
      <template #body-cell-kind="props">
        <q-td :props="props">
          <q-badge :label="formatIdentityKind(props.row.kind)" outline color="primary" />
        </q-td>
      </template>
      <template #body-cell-ownerInstance="props">
        <q-td :props="props">
          {{ ownerInstanceResolver(props.row) }}
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
      <template #body-cell-assignments="props">
        <q-td :props="props">
          <q-badge color="secondary" :label="`${props.row.assignments?.length ?? 0} assignments`" />
        </q-td>
      </template>
      <template #body-cell-actions="cellProps">
        <q-td :props="cellProps" class="text-right">
          <q-btn 
            flat 
            dense 
            round 
            icon="edit" 
            color="primary" 
            :disable="!props.canModify"
            @click="emit('edit', cellProps.row)" 
          />
          <q-btn
            flat
            dense
            round
            icon="delete"
            color="negative"
            class="q-ml-xs"
            :disable="!props.canModify"
            @click="emit('delete', cellProps.row)"
          />
        </q-td>
      </template>
      <template #no-data>
        <div class="q-pa-md text-grey-7">No identities configured.</div>
      </template>
    </q-table>
  </q-card>
</template>

<script setup lang="ts">
import { computed, ref } from 'vue'
import type { QTableColumn } from 'quasar'
import type { Identity, IdentityKind } from '../../api/client'

interface Props {
  identities: Identity[]
  loading: boolean
  pagination: { rowsPerPage: number }
  tagLookup: Record<string, string | undefined>
  ownerInstanceResolver: (identity: Identity) => string
  canModify: boolean
}

const props = defineProps<Props>()
const emit = defineEmits<{ (event: 'edit', identity: Identity): void; (event: 'delete', identity: Identity): void }>()

const filter = ref('')
const rows = computed(() => props.identities ?? [])

const columns: QTableColumn<Identity>[] = [
  { name: 'name', label: 'Name', field: 'name', align: 'left', sortable: true },
  { name: 'kind', label: 'Kind', field: 'kind', align: 'left' },
  { name: 'ownerInstance', label: 'Owner Instance', field: 'ownerInstanceId', align: 'left' },
  { name: 'assignments', label: 'Assignments', field: 'assignments', align: 'left' },
  { name: 'tags', label: 'Tags', field: 'tagIds', align: 'left' },
  { name: 'actions', label: '', field: (row) => row.id, align: 'right' }
]

function formatIdentityKind(kind: IdentityKind | undefined): string {
  switch (kind) {
    case 'AzureManagedIdentity':
      return 'Azure Managed Identity'
    case 'KubernetesServiceAccount':
      return 'K8s Service Account'
    case 'AwsIamRole':
      return 'AWS IAM Role'
    case 'Custom':
      return 'Custom'
    default:
      return '—'
  }
}
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
