<template>
  <div class="page-container">
    <div class="page-header">
      <div>
        <h1>Servers</h1>
        <p class="subtitle">Catalogue infrastructure and link it to applications.</p>
      </div>
      <q-btn color="primary" label="Create Server" icon="add" @click="openCreateDialog" />
    </div>

    <q-banner v-if="serverError" dense class="bg-red-1 text-negative q-mb-md">
      {{ serverError }}
    </q-banner>

    <q-card class="content-card">
      <q-table
        flat
        bordered
        :rows="servers"
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
        <template #body-cell-operatingSystem="props">
          <q-td :props="props">
            {{ props.row.operatingSystem ?? '—' }}
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
          <div class="q-pa-md text-grey-7">No servers recorded.</div>
        </template>
      </q-table>
    </q-card>

    <q-dialog v-model="isCreateDialogOpen" persistent>
      <q-card class="form-dialog">
        <q-card-section class="dialog-header">
          <div class="text-h6">Create Server</div>
          <q-btn flat round dense icon="close" @click="isCreateDialogOpen = false" />
        </q-card-section>
        <q-separator />
        <q-form @submit.prevent="submitCreate">
          <q-card-section>
            <div class="form-grid">
              <q-input v-model="createForm.name" label="Name" dense outlined />
              <q-input v-model="createForm.hostname" label="Hostname" dense outlined />
              <q-select
                v-model="createForm.operatingSystem"
                label="Operating System"
                dense
                outlined
                emit-value
                map-options
                :options="operatingSystemOptions"
              />
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
          <div class="text-h6">Edit Server</div>
          <q-btn flat round dense icon="close" @click="closeEditDialog" />
        </q-card-section>
        <q-separator />
        <q-form @submit.prevent="submitEdit">
          <q-card-section>
            <div class="form-grid">
              <q-input v-model="editForm.name" label="Name" dense outlined />
              <q-input v-model="editForm.hostname" label="Hostname" dense outlined />
              <q-select
                v-model="editForm.operatingSystem"
                label="Operating System"
                dense
                outlined
                emit-value
                map-options
                :options="operatingSystemOptions"
              />
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
import { Server, ServerOperatingSystem, CreateServer, UpdateServer } from '../api/client'
import { useFuseClient } from '../composables/useFuseClient'
import { useEnvironments } from '../composables/useEnvironments'
import { useTags } from '../composables/useTags'
import { getErrorMessage } from '../utils/error'

interface ServerForm {
  name: string
  hostname: string
  operatingSystem: ServerOperatingSystem | null
  environmentId: string | null
  tagIds: string[]
}

const client = useFuseClient()
const queryClient = useQueryClient()
const environmentsStore = useEnvironments()
const tagsStore = useTags()

const pagination = { rowsPerPage: 10 }

const { data, isLoading, error } = useQuery({
  queryKey: ['servers'],
  queryFn: () => client.serverAll()
})

const servers = computed(() => data.value ?? [])
const serverError = computed(() => (error.value ? getErrorMessage(error.value) : null))

const environmentOptions = environmentsStore.options
const environmentLookup = environmentsStore.lookup
const tagOptions = tagsStore.options
const tagLookup = tagsStore.lookup

const operatingSystemOptions = Object.values(ServerOperatingSystem).map((value) => ({
  label: value,
  value
}))

const columns: QTableColumn<Server>[] = [
  { name: 'name', label: 'Name', field: 'name', align: 'left', sortable: true },
  { name: 'hostname', label: 'Hostname', field: 'hostname', align: 'left' },
  { name: 'operatingSystem', label: 'Operating System', field: 'operatingSystem', align: 'left' },
  { name: 'environment', label: 'Environment', field: 'environmentId', align: 'left' },
  { name: 'tags', label: 'Tags', field: 'tagIds', align: 'left' },
  { name: 'actions', label: '', field: (row) => row.id, align: 'right' }
]

const isCreateDialogOpen = ref(false)
const isEditDialogOpen = ref(false)
const selectedServer = ref<Server | null>(null)

const emptyForm = (): ServerForm => ({
  name: '',
  hostname: '',
  operatingSystem: null,
  environmentId: null,
  tagIds: []
})

const createForm = reactive<ServerForm>(emptyForm())
const editForm = reactive<ServerForm & { id: string | null }>({ id: null, ...emptyForm() })

function openCreateDialog() {
  Object.assign(createForm, emptyForm())
  isCreateDialogOpen.value = true
}

function openEditDialog(server: Server) {
  if (!server.id) return
  selectedServer.value = server
  Object.assign(editForm, {
    id: server.id ?? null,
    name: server.name ?? '',
    hostname: server.hostname ?? '',
    operatingSystem: server.operatingSystem ?? null,
    environmentId: server.environmentId ?? null,
    tagIds: [...(server.tagIds ?? [])]
  })
  isEditDialogOpen.value = true
}

function closeEditDialog() {
  selectedServer.value = null
  isEditDialogOpen.value = false
}

const createMutation = useMutation({
  mutationFn: (payload: CreateServer) => client.serverPOST(payload),
  onSuccess: () => {
    queryClient.invalidateQueries({ queryKey: ['servers'] })
    Notify.create({ type: 'positive', message: 'Server created' })
    isCreateDialogOpen.value = false
  },
  onError: (err) => {
    Notify.create({ type: 'negative', message: getErrorMessage(err, 'Unable to create server') })
  }
})

const updateMutation = useMutation({
  mutationFn: ({ id, payload }: { id: string; payload: UpdateServer }) => client.serverPUT(id, payload),
  onSuccess: () => {
    queryClient.invalidateQueries({ queryKey: ['servers'] })
    Notify.create({ type: 'positive', message: 'Server updated' })
    closeEditDialog()
  },
  onError: (err) => {
    Notify.create({ type: 'negative', message: getErrorMessage(err, 'Unable to update server') })
  }
})

const deleteMutation = useMutation({
  mutationFn: (id: string) => client.serverDELETE(id),
  onSuccess: () => {
    queryClient.invalidateQueries({ queryKey: ['servers'] })
    Notify.create({ type: 'positive', message: 'Server deleted' })
  },
  onError: (err) => {
    Notify.create({ type: 'negative', message: getErrorMessage(err, 'Unable to delete server') })
  }
})

function submitCreate() {
  const payload = Object.assign(new CreateServer(), {
    name: createForm.name || undefined,
    hostname: createForm.hostname || undefined,
    operatingSystem: createForm.operatingSystem || undefined,
    environmentId: createForm.environmentId || undefined,
    tagIds: createForm.tagIds.length ? [...createForm.tagIds] : undefined
  })
  createMutation.mutate(payload)
}

function submitEdit() {
  if (!editForm.id) return
  const payload = Object.assign(new UpdateServer(), {
    name: editForm.name || undefined,
    hostname: editForm.hostname || undefined,
    operatingSystem: editForm.operatingSystem || undefined,
    environmentId: editForm.environmentId || undefined,
    tagIds: editForm.tagIds.length ? [...editForm.tagIds] : undefined
  })
  updateMutation.mutate({ id: editForm.id, payload })
}

function confirmDelete(server: Server) {
  if (!server.id) return
  Dialog.create({
    title: 'Delete server',
    message: `Delete "${server.name ?? server.hostname ?? 'this server'}"?`,
    cancel: true,
    persistent: true
  }).onOk(() => deleteMutation.mutate(server.id!))
}
</script>

<style scoped>
.page-container {
  padding: 2rem;
  display: flex;
  flex-direction: column;
  gap: 1rem;
}

.page-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
}

.subtitle {
  margin: 0;
  color: #6c757d;
}

.content-card {
  flex: 1;
}

.form-dialog {
  min-width: 520px;
}

.dialog-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
}

.form-grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(220px, 1fr));
  gap: 1rem;
}

.full-span {
  grid-column: 1 / -1;
}

.tag-list {
  display: flex;
  flex-wrap: wrap;
  gap: 0.25rem;
}
</style>
