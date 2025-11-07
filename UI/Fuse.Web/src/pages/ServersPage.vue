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

    <q-dialog v-model="isDialogOpen" persistent>
      <ServerForm
        :mode="dialogMode"
        :initial-value="selectedServer"
        :loading="isAnyPending"
        @submit="handleSubmit"
        @cancel="closeDialog"
      />
    </q-dialog>
  </div>
</template>

<script setup lang="ts">
import { computed, ref } from 'vue'
import { useQuery, useMutation, useQueryClient } from '@tanstack/vue-query'
import { Notify, Dialog } from 'quasar'
import type { QTableColumn } from 'quasar'
import { Server, ServerOperatingSystem, CreateServer, UpdateServer } from '../api/client'
import { useFuseClient } from '../composables/useFuseClient'
import { useEnvironments } from '../composables/useEnvironments'
import { useTags } from '../composables/useTags'
import { getErrorMessage } from '../utils/error'
import ServerForm from '../components/servers/ServerForm.vue'

interface ServerFormModel {
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

const environmentLookup = environmentsStore.lookup
const tagLookup = tagsStore.lookup

const columns: QTableColumn<Server>[] = [
  { name: 'name', label: 'Name', field: 'name', align: 'left', sortable: true },
  { name: 'hostname', label: 'Hostname', field: 'hostname', align: 'left' },
  { name: 'operatingSystem', label: 'Operating System', field: 'operatingSystem', align: 'left' },
  { name: 'environment', label: 'Environment', field: 'environmentId', align: 'left' },
  { name: 'tags', label: 'Tags', field: 'tagIds', align: 'left' },
  { name: 'actions', label: '', field: (row) => row.id, align: 'right' }
]

const isDialogOpen = ref(false)
const dialogMode = ref<'create' | 'edit'>('create')
const selectedServer = ref<Server | null>(null)

function openCreateDialog() {
  selectedServer.value = null
  dialogMode.value = 'create'
  isDialogOpen.value = true
}

function openEditDialog(server: Server) {
  if (!server.id) return
  selectedServer.value = server
  dialogMode.value = 'edit'
  isDialogOpen.value = true
}

function closeDialog() {
  selectedServer.value = null
  isDialogOpen.value = false
}

const createMutation = useMutation({
  mutationFn: (payload: CreateServer) => client.serverPOST(payload),
  onSuccess: () => {
    queryClient.invalidateQueries({ queryKey: ['servers'] })
    Notify.create({ type: 'positive', message: 'Server created' })
    closeDialog()
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
    closeDialog()
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

const isAnyPending = computed(() => createMutation.isPending.value || updateMutation.isPending.value)

function handleSubmit(model: ServerFormModel) {
  if (dialogMode.value === 'create') {
    const payload = Object.assign(new CreateServer(), {
      name: model.name || undefined,
      hostname: model.hostname || undefined,
      operatingSystem: model.operatingSystem || undefined,
      environmentId: model.environmentId || undefined,
      tagIds: model.tagIds.length ? [...model.tagIds] : undefined
    })
    createMutation.mutate(payload)
  } else if (dialogMode.value === 'edit' && selectedServer.value?.id) {
    const payload = Object.assign(new UpdateServer(), {
      name: model.name || undefined,
      hostname: model.hostname || undefined,
      operatingSystem: model.operatingSystem || undefined,
      environmentId: model.environmentId || undefined,
      tagIds: model.tagIds.length ? [...model.tagIds] : undefined
    })
    updateMutation.mutate({ id: selectedServer.value.id, payload })
  }
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
@import '../styles/pages.css';
</style>
