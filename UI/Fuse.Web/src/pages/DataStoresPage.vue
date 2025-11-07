<template>
  <div class="page-container">
    <div class="page-header">
      <div>
        <h1>Data Stores</h1>
        <p class="subtitle">Capture connection details for databases and messaging backends.</p>
      </div>
      <q-btn color="primary" label="Create Data Store" icon="add" @click="openCreateDialog" />
    </div>

    <q-banner v-if="dataStoreError" dense class="bg-red-1 text-negative q-mb-md">
      {{ dataStoreError }}
    </q-banner>

    <q-card class="content-card">
      <q-table
        flat
        bordered
        :rows="dataStores"
        :columns="columns"
        row-key="id"
        :loading="isLoading"
        :pagination="pagination"
      >
        <template #body-cell-environment="props">
          <q-td :props="props">
            {{ environmentLookup[props.row.environmentId ?? ''] ?? '—' }}
          </q-td>
        </template>
        <template #body-cell-server="props">
          <q-td :props="props">
            {{ serverLookup[props.row.serverId ?? ''] ?? '—' }}
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
            <q-btn flat dense round icon="edit" color="primary" @click="openEditDialog(props.row)" />
            <q-btn
              flat
              dense
              round
              icon="delete"
              color="negative"
              class="q-ml-xs"
              @click="confirmDelete(props.row)"
            />
          </q-td>
        </template>
        <template #no-data>
          <div class="q-pa-md text-grey-7">No data stores documented.</div>
        </template>
      </q-table>
    </q-card>

    <q-dialog v-model="isCreateDialogOpen" persistent>
      <q-card class="form-dialog">
        <q-card-section class="dialog-header">
          <div class="text-h6">Create Data Store</div>
          <q-btn flat round dense icon="close" @click="isCreateDialogOpen = false" />
        </q-card-section>
        <q-separator />
        <q-form @submit.prevent="submitCreate">
          <q-card-section>
            <div class="form-grid">
              <q-input v-model="createForm.name" label="Name" dense outlined />
              <q-input v-model="createForm.kind" label="Kind" dense outlined />
              <q-select
                v-model="createForm.environmentId"
                label="Environment"
                dense
                outlined
                emit-value
                map-options
                :options="environmentOptions"
              />
              <q-select
                v-model="createForm.serverId"
                label="Server"
                dense
                outlined
                emit-value
                map-options
                clearable
                :options="serverOptions"
              />
              <q-input v-model="createForm.connectionUri" label="Connection URI" dense outlined />
              <q-select
                v-model="createForm.tagIds"
                label="Tags"
                dense
                outlined
                use-chips
                multiple
                emit-value
                map-options
                :options="tagOptions"
              />
            </div>
          </q-card-section>
          <q-separator />
          <q-card-actions align="right">
            <q-btn flat label="Cancel" @click="isCreateDialogOpen = false" />
            <q-btn color="primary" type="submit" label="Create" :loading="createMutation.isPending.value" />
          </q-card-actions>
        </q-form>
      </q-card>
    </q-dialog>

    <q-dialog v-model="isEditDialogOpen" persistent>
      <q-card class="form-dialog">
        <q-card-section class="dialog-header">
          <div class="text-h6">Edit Data Store</div>
          <q-btn flat round dense icon="close" @click="closeEditDialog" />
        </q-card-section>
        <q-separator />
        <q-form @submit.prevent="submitEdit">
          <q-card-section>
            <div class="form-grid">
              <q-input v-model="editForm.name" label="Name" dense outlined />
              <q-input v-model="editForm.kind" label="Kind" dense outlined />
              <q-select
                v-model="editForm.environmentId"
                label="Environment"
                dense
                outlined
                emit-value
                map-options
                :options="environmentOptions"
              />
              <q-select
                v-model="editForm.serverId"
                label="Server"
                dense
                outlined
                emit-value
                map-options
                clearable
                :options="serverOptions"
              />
              <q-input v-model="editForm.connectionUri" label="Connection URI" dense outlined />
              <q-select
                v-model="editForm.tagIds"
                label="Tags"
                dense
                outlined
                use-chips
                multiple
                emit-value
                map-options
                :options="tagOptions"
              />
            </div>
          </q-card-section>
          <q-separator />
          <q-card-actions align="right">
            <q-btn flat label="Cancel" @click="closeEditDialog" />
            <q-btn color="primary" type="submit" label="Save" :loading="updateMutation.isPending.value" />
          </q-card-actions>
        </q-form>
      </q-card>
    </q-dialog>
  </div>
</template>

<script setup lang="ts">
import { computed, reactive, ref } from 'vue'
import { useQuery, useMutation, useQueryClient } from '@tanstack/vue-query'
import { Notify, Dialog } from 'quasar'
import type { QTableColumn } from 'quasar'
import { DataStore, CreateDataStore, UpdateDataStore } from '../api/client'
import { useFuseClient } from '../composables/useFuseClient'
import { useEnvironments } from '../composables/useEnvironments'
import { useServers } from '../composables/useServers'
import { useTags } from '../composables/useTags'
import { getErrorMessage } from '../utils/error'

interface DataStoreForm {
  name: string
  kind: string
  environmentId: string | null
  serverId: string | null
  connectionUri: string
  tagIds: string[]
}

const client = useFuseClient()
const queryClient = useQueryClient()
const environmentsStore = useEnvironments()
const serversStore = useServers()
const tagsStore = useTags()

const pagination = { rowsPerPage: 10 }

const { data, isLoading, error } = useQuery({
  queryKey: ['dataStores'],
  queryFn: () => client.dataStoreAll()
})

const dataStores = computed(() => data.value ?? [])
const dataStoreError = computed(() => (error.value ? getErrorMessage(error.value) : null))

const environmentOptions = environmentsStore.options
const environmentLookup = environmentsStore.lookup
const serverOptions = serversStore.options
const serverLookup = serversStore.lookup
const tagOptions = tagsStore.options
const tagLookup = tagsStore.lookup

const columns: QTableColumn<DataStore>[] = [
  { name: 'name', label: 'Name', field: 'name', align: 'left', sortable: true },
  { name: 'kind', label: 'Kind', field: 'kind', align: 'left' },
  { name: 'connectionUri', label: 'Connection URI', field: 'connectionUri', align: 'left' },
  { name: 'environment', label: 'Environment', field: 'environmentId', align: 'left' },
  { name: 'server', label: 'Server', field: 'serverId', align: 'left' },
  { name: 'tags', label: 'Tags', field: 'tagIds', align: 'left' },
  { name: 'actions', label: '', field: (row) => row.id, align: 'right' }
]

const emptyForm = (): DataStoreForm => ({
  name: '',
  kind: '',
  environmentId: null,
  serverId: null,
  connectionUri: '',
  tagIds: []
})

const isCreateDialogOpen = ref(false)
const isEditDialogOpen = ref(false)
const selectedDataStore = ref<DataStore | null>(null)

const createForm = reactive<DataStoreForm>(emptyForm())
const editForm = reactive<DataStoreForm & { id: string | null }>({ id: null, ...emptyForm() })

function openCreateDialog() {
  Object.assign(createForm, emptyForm())
  isCreateDialogOpen.value = true
}

function openEditDialog(store: DataStore) {
  if (!store.id) return
  selectedDataStore.value = store
  Object.assign(editForm, {
    id: store.id ?? null,
    name: store.name ?? '',
    kind: store.kind ?? '',
    environmentId: store.environmentId ?? null,
    serverId: store.serverId ?? null,
    connectionUri: store.connectionUri ?? '',
    tagIds: [...(store.tagIds ?? [])]
  })
  isEditDialogOpen.value = true
}

function closeEditDialog() {
  selectedDataStore.value = null
  isEditDialogOpen.value = false
}

const createMutation = useMutation({
  mutationFn: (payload: CreateDataStore) => client.dataStorePOST(payload),
  onSuccess: () => {
    queryClient.invalidateQueries({ queryKey: ['dataStores'] })
    Notify.create({ type: 'positive', message: 'Data store created' })
    isCreateDialogOpen.value = false
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
    closeEditDialog()
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

function submitCreate() {
  const payload = Object.assign(new CreateDataStore(), {
    name: createForm.name || undefined,
    kind: createForm.kind || undefined,
    environmentId: createForm.environmentId || undefined,
    serverId: createForm.serverId || undefined,
    connectionUri: createForm.connectionUri || undefined,
    tagIds: createForm.tagIds.length ? [...createForm.tagIds] : undefined
  })
  createMutation.mutate(payload)
}

function submitEdit() {
  if (!editForm.id) return
  const payload = Object.assign(new UpdateDataStore(), {
    name: editForm.name || undefined,
    kind: editForm.kind || undefined,
    environmentId: editForm.environmentId || undefined,
    serverId: editForm.serverId || undefined,
    connectionUri: editForm.connectionUri || undefined,
    tagIds: editForm.tagIds.length ? [...editForm.tagIds] : undefined
  })
  updateMutation.mutate({ id: editForm.id, payload })
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
