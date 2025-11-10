<template>
  <div class="page-container">
    <div class="page-header">
      <div>
        <h1>Environments</h1>
        <p class="subtitle">Model environments and attach resources to them.</p>
      </div>
      <q-btn
        color="primary"
        label="Create Environment"
        icon="add"
        :disable="!fuseStore.canModify"
        @click="openCreateDialog"
        data-tour-id="create-environment"
      />
    </div>

    <q-banner v-if="environmentError" dense class="bg-red-1 text-negative q-mb-md">
      {{ environmentError }}
    </q-banner>

    <q-banner v-if="!fuseStore.canRead" dense class="bg-orange-1 text-orange-9 q-mb-md">
      You do not have permission to view environments. Please log in with appropriate credentials.
    </q-banner>

    <q-card v-if="fuseStore.canRead" class="content-card">
      <q-table
        flat
        bordered
        :rows="environments"
        :columns="columns"
        row-key="id"
        :loading="isLoading"
        :pagination="pagination"
        data-tour-id="environments-table"
      >
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
          <div class="q-pa-md text-grey-7">No environments yet.</div>
        </template>
      </q-table>
    </q-card>

    <q-dialog v-model="isDialogOpen" persistent>
      <EnvironmentForm
        :mode="dialogMode"
        :initial-value="selectedEnvironment"
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
import { EnvironmentInfo, CreateEnvironment, UpdateEnvironment } from '../api/client'
import { useFuseClient } from '../composables/useFuseClient'
import { useFuseStore } from '../stores/FuseStore'
import { useTags } from '../composables/useTags'
import { getErrorMessage } from '../utils/error'
import EnvironmentForm from '../components/environments/EnvironmentForm.vue'

interface EnvironmentFormModel {
  name: string
  description: string
  tagIds: string[]
}

const client = useFuseClient()
const queryClient = useQueryClient()
const fuseStore = useFuseStore()
const tagsStore = useTags()

const pagination = { rowsPerPage: 10 }

const { data, isLoading, error } = useQuery({
  queryKey: ['environments'],
  queryFn: () => client.environmentAll()
})

const environments = computed(() => data.value ?? [])
const environmentError = computed(() => (error.value ? getErrorMessage(error.value) : null))
const tagLookup = tagsStore.lookup

const columns: QTableColumn<EnvironmentInfo>[] = [
  { name: 'name', label: 'Name', field: 'name', align: 'left', sortable: true },
  { name: 'description', label: 'Description', field: 'description', align: 'left' },
  { name: 'tags', label: 'Tags', field: 'tagIds', align: 'left' },
  { name: 'actions', label: '', field: (row) => row.id, align: 'right' }
]

const isDialogOpen = ref(false)
const dialogMode = ref<'create' | 'edit'>('create')
const selectedEnvironment = ref<EnvironmentInfo | null>(null)

function openCreateDialog() {
  selectedEnvironment.value = null
  dialogMode.value = 'create'
  isDialogOpen.value = true
}

function openEditDialog(env: EnvironmentInfo) {
  if (!env.id) return
  selectedEnvironment.value = env
  dialogMode.value = 'edit'
  isDialogOpen.value = true
}

function closeDialog() {
  selectedEnvironment.value = null
  isDialogOpen.value = false
}

const createMutation = useMutation({
  mutationFn: (payload: CreateEnvironment) => client.environmentPOST(payload),
  onSuccess: () => {
    queryClient.invalidateQueries({ queryKey: ['environments'] })
    Notify.create({ type: 'positive', message: 'Environment created' })
    closeDialog()
  },
  onError: (err) => {
    Notify.create({ type: 'negative', message: getErrorMessage(err, 'Unable to create environment') })
  }
})

const updateMutation = useMutation({
  mutationFn: ({ id, payload }: { id: string; payload: UpdateEnvironment }) => client.environmentPUT(id, payload),
  onSuccess: () => {
    queryClient.invalidateQueries({ queryKey: ['environments'] })
    Notify.create({ type: 'positive', message: 'Environment updated' })
    closeDialog()
  },
  onError: (err) => {
    Notify.create({ type: 'negative', message: getErrorMessage(err, 'Unable to update environment') })
  }
})

const deleteMutation = useMutation({
  mutationFn: (id: string) => client.environmentDELETE(id),
  onSuccess: () => {
    queryClient.invalidateQueries({ queryKey: ['environments'] })
    Notify.create({ type: 'positive', message: 'Environment deleted' })
  },
  onError: (err) => {
    Notify.create({ type: 'negative', message: getErrorMessage(err, 'Unable to delete environment') })
  }
})

const isAnyPending = computed(() => createMutation.isPending.value || updateMutation.isPending.value)

function handleSubmit(model: EnvironmentFormModel) {
  if (dialogMode.value === 'create') {
    const payload = Object.assign(new CreateEnvironment(), {
      name: model.name || undefined,
      description: model.description || undefined,
      tagIds: model.tagIds.length ? [...model.tagIds] : undefined
    })
    createMutation.mutate(payload)
  } else if (dialogMode.value === 'edit' && selectedEnvironment.value?.id) {
    const payload = Object.assign(new UpdateEnvironment(), {
      name: model.name || undefined,
      description: model.description || undefined,
      tagIds: model.tagIds.length ? [...model.tagIds] : undefined
    })
    updateMutation.mutate({ id: selectedEnvironment.value.id, payload })
  }
}

function confirmDelete(env: EnvironmentInfo) {
  if (!env.id) return
  Dialog.create({
    title: 'Delete environment',
    message: `Delete "${env.name ?? 'this environment'}"?`,
    cancel: true,
    persistent: true
  }).onOk(() => deleteMutation.mutate(env.id!))
}
</script>

<style scoped>
@import '../styles/pages.css';
</style>
