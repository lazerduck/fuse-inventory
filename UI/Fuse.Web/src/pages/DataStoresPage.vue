<template>
  <div class="page-container">
    <div class="page-header">
      <div>
        <h1>Data Stores</h1>
        <p class="subtitle">Capture connection details for databases and messaging backends.</p>
      </div>
      <q-btn
        color="primary"
        label="Create Data Store"
        icon="add"
        :disable="!fuseStore.canModify"
        @click="openCreateDialog"
        data-tour-id="create-data-store"
      />
    </div>

    <q-banner v-if="dataStoreError" dense class="bg-red-1 text-negative q-mb-md">
      {{ dataStoreError }}
    </q-banner>

    <q-banner v-if="!fuseStore.canRead" dense class="bg-orange-1 text-orange-9 q-mb-md">
      You do not have permission to view data stores. Please log in with appropriate credentials.
    </q-banner>

    <q-card v-if="fuseStore.canRead" class="content-card">
      <q-table
        flat
        bordered
        :rows="dataStores"
        :columns="columns"
        row-key="id"
        :loading="isLoading"
        :pagination="pagination"
        data-tour-id="data-stores-table"
      >
        <template #body-cell-environment="props">
          <q-td :props="props">
            {{ environmentLookup[props.row.environmentId ?? ''] ?? '—' }}
          </q-td>
        </template>
        <template #body-cell-platform="props">
          <q-td :props="props">
            {{ platformLookup[props.row.platformId ?? ''] ?? '—' }}
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
        <template #body-cell-actions="props">
          <q-td :props="props" class="text-right">
            <q-btn 
              flat 
              dense 
              round 
              icon="edit" 
              color="primary" 
              :disable="!fuseStore.canModify"
              @click="openEditDialog(props.row)" 
            />
            <q-btn
              flat
              dense
              round
              icon="delete"
              color="negative"
              class="q-ml-xs"
              :disable="!fuseStore.canModify"
              @click="confirmDelete(props.row)"
            />
          </q-td>
        </template>
        <template #no-data>
          <div class="q-pa-md text-grey-7">No data stores documented.</div>
        </template>
      </q-table>
    </q-card>

    <q-dialog v-model="isFormDialogOpen" persistent>
      <DataStoreForm
        :mode="selectedDataStore ? 'edit' : 'create'"
        :initial-value="selectedDataStore"
        :loading="formLoading"
        @submit="handleFormSubmit"
        @cancel="closeFormDialog"
      />
    </q-dialog>
  </div>
</template>

<script setup lang="ts">
import { computed, ref } from 'vue'
import { useMutation, useQueryClient } from '@tanstack/vue-query'
import { Notify, Dialog } from 'quasar'
import type { QTableColumn } from 'quasar'
import { DataStore, CreateDataStore, UpdateDataStore } from '../api/client'
import { useFuseClient } from '../composables/useFuseClient'
import { useFuseStore } from '../stores/FuseStore'
import { useEnvironments } from '../composables/useEnvironments'
import { usePlatforms } from '../composables/usePlatforms'
import { useTags } from '../composables/useTags'
import { getErrorMessage } from '../utils/error'
import DataStoreForm from '../components/dataStore/DataStoreForm.vue'
import { useDataStores } from '../composables/useDataStores'

interface DataStoreFormModel {
  name: string
  kind: string
  environmentId: string | null
  platformId: string | null
  connectionUri: string
  tagIds: string[]
}

const client = useFuseClient()
const queryClient = useQueryClient()
const fuseStore = useFuseStore()
const environmentsStore = useEnvironments()
const platformsStore = usePlatforms()
const tagsStore = useTags()

const pagination = { rowsPerPage: 10 }

const { data, isLoading, error } = useDataStores()

const dataStores = computed(() => data.value ?? [])
const dataStoreError = computed(() => (error.value ? getErrorMessage(error.value) : null))

const environmentLookup = environmentsStore.lookup
const platformLookup = platformsStore.lookup
const tagLookup = tagsStore.lookup

const columns: QTableColumn<DataStore>[] = [
  { name: 'name', label: 'Name', field: 'name', align: 'left', sortable: true },
  { name: 'kind', label: 'Kind', field: 'kind', align: 'left' },
  { name: 'connectionUri', label: 'Connection URI', field: 'connectionUri', align: 'left' },
  { name: 'environment', label: 'Environment', field: 'environmentId', align: 'left' },
  { name: 'platform', label: 'Platform', field: 'platformId', align: 'left' },
  { name: 'tags', label: 'Tags', field: 'tagIds', align: 'left' },
  { name: 'actions', label: '', field: (row) => row.id, align: 'right' }
]

const isFormDialogOpen = ref(false)
const selectedDataStore = ref<DataStore | null>(null)

function openCreateDialog() {
  selectedDataStore.value = null
  isFormDialogOpen.value = true
}

function openEditDialog(store: DataStore) {
  if (!store.id) return
  selectedDataStore.value = store
  isFormDialogOpen.value = true
}

function closeFormDialog() {
  selectedDataStore.value = null
  isFormDialogOpen.value = false
}

const createMutation = useMutation({
  mutationFn: (payload: CreateDataStore) => client.dataStorePOST(payload),
  onSuccess: () => {
    queryClient.invalidateQueries({ queryKey: ['dataStores'] })
    Notify.create({ type: 'positive', message: 'Data store created' })
  closeFormDialog()
  },
  onError: (err) => {
    Notify.create({ type: 'negative', message: getErrorMessage(err, 'Unable to create data store') })
  }
})

const updateMutation = useMutation({
  mutationFn: ({ id, payload }: { id: string; payload: UpdateDataStore }) => client.dataStorePUT(id, payload),
  onSuccess: () => {
    queryClient.invalidateQueries({ queryKey: ['dataStores'] })
    Notify.create({ type: 'positive', message: 'Data store updated' })
  closeFormDialog()
  },
  onError: (err) => {
    Notify.create({ type: 'negative', message: getErrorMessage(err, 'Unable to update data store') })
  }
})

const deleteMutation = useMutation({
  mutationFn: (id: string) => client.dataStoreDELETE(id),
  onSuccess: () => {
    queryClient.invalidateQueries({ queryKey: ['dataStores'] })
    Notify.create({ type: 'positive', message: 'Data store deleted' })
  },
  onError: (err) => {
    Notify.create({ type: 'negative', message: getErrorMessage(err, 'Unable to delete data store') })
  }
})

const formLoading = computed(() =>
  selectedDataStore.value ? updateMutation.isPending.value : createMutation.isPending.value
)

function handleFormSubmit(values: DataStoreFormModel) {
  if (selectedDataStore.value?.id) {
    const payload = Object.assign(new UpdateDataStore(), {
      name: values.name || undefined,
      kind: values.kind || undefined,
      environmentId: values.environmentId || undefined,
      platformId: values.platformId || undefined,
      connectionUri: values.connectionUri || undefined,
      tagIds: values.tagIds.length ? [...values.tagIds] : undefined
    })
    updateMutation.mutate({ id: selectedDataStore.value.id, payload })
  } else {
    const payload = Object.assign(new CreateDataStore(), {
      name: values.name || undefined,
      kind: values.kind || undefined,
      environmentId: values.environmentId || undefined,
      platformId: values.platformId || undefined,
      connectionUri: values.connectionUri || undefined,
      tagIds: values.tagIds.length ? [...values.tagIds] : undefined
    })
    createMutation.mutate(payload)
  }
}

function confirmDelete(store: DataStore) {
  if (!store.id) return
  Dialog.create({
    title: 'Delete data store',
    message: `Delete "${store.name ?? 'this data store'}"?`,
    cancel: true,
    persistent: true
  }).onOk(() => deleteMutation.mutate(store.id!))
}
</script>

<style scoped>
@import '../styles/pages.css';

.form-dialog {
  min-width: 520px;
}
</style>
