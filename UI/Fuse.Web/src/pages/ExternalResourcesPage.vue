<template>
  <div class="page-container">
    <div class="page-header">
      <div>
        <h1>External Resources</h1>
        <p class="subtitle">Track third-party services, SaaS accounts, and shared tooling.</p>
      </div>
      <q-btn 
        color="primary" 
        label="Create Resource" 
        icon="add" 
        :disable="!fuseStore.canModify"
        @click="openCreateDialog" 
      />
    </div>

    <q-banner v-if="resourceError" dense class="bg-red-1 text-negative q-mb-md">
      {{ resourceError }}
    </q-banner>

    <q-banner v-if="!fuseStore.canRead" dense class="bg-orange-1 text-orange-9 q-mb-md">
      You do not have permission to view external resources. Please log in with appropriate credentials.
    </q-banner>

    <q-card v-if="fuseStore.canRead" class="content-card">
      <q-table
        flat
        bordered
        :rows="resources"
        :columns="columns"
        row-key="id"
        :loading="isLoading"
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
        <template #body-cell-tags="props">
          <q-td :props="props">
            <div v-if="props.row.tagIds?.length" class="tag-list">
              <TagChip
                v-for="tagId in props.row.tagIds"
                :key="tagId"
                :label="tagInfoLookup[tagId]?.name ?? tagId"
                :color="tagInfoLookup[tagId]?.color"
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
          <div class="q-pa-md text-grey-7">No external resources yet.</div>
        </template>
      </q-table>
    </q-card>

    <q-dialog v-model="isDialogOpen" persistent>
      <ExternalResourceForm
        :mode="dialogMode"
        :initial-value="selectedResource"
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
import { ExternalResource, CreateExternalResource, UpdateExternalResource } from '../api/client'
import { useFuseClient } from '../composables/useFuseClient'
import { useFuseStore } from '../stores/FuseStore'
import { useTags } from '../composables/useTags'
import { getErrorMessage } from '../utils/error'
import ExternalResourceForm from '../components/externalResources/ExternalResourceForm.vue'
import TagChip from '../components/tags/TagChip.vue'

interface ExternalResourceFormModel {
  name: string
  resourceUri: string
  description: string
  tagIds: string[]
}

const client = useFuseClient()
const queryClient = useQueryClient()
const fuseStore = useFuseStore()
const tagsStore = useTags()

const pagination = { rowsPerPage: 10 }
const filter = ref('')

const { data, isLoading, error } = useQuery({
  queryKey: ['externalResources'],
  queryFn: () => client.externalResourceAll()
})

const resources = computed(() => data.value ?? [])
const resourceError = computed(() => (error.value ? getErrorMessage(error.value) : null))

const tagInfoLookup = tagsStore.tagInfoLookup

const columns: QTableColumn<ExternalResource>[] = [
  { name: 'name', label: 'Name', field: 'name', align: 'left', sortable: true },
  { name: 'resourceUri', label: 'Resource URI', field: 'resourceUri', align: 'left' },
  { name: 'description', label: 'Description', field: 'description', align: 'left' },
  { name: 'tags', label: 'Tags', field: 'tagIds', align: 'left' },
  { name: 'actions', label: '', field: (row) => row.id, align: 'right' }
]

const isDialogOpen = ref(false)
const dialogMode = ref<'create' | 'edit'>('create')
const selectedResource = ref<ExternalResource | null>(null)

function openCreateDialog() {
  selectedResource.value = null
  dialogMode.value = 'create'
  isDialogOpen.value = true
}

function openEditDialog(resource: ExternalResource) {
  if (!resource.id) return
  selectedResource.value = resource
  dialogMode.value = 'edit'
  isDialogOpen.value = true
}

function closeDialog() {
  selectedResource.value = null
  isDialogOpen.value = false
}

const createMutation = useMutation({
  mutationFn: (payload: CreateExternalResource) => client.externalResourcePOST(payload),
  onSuccess: () => {
    queryClient.invalidateQueries({ queryKey: ['externalResources'] })
    Notify.create({ type: 'positive', message: 'Resource created' })
    closeDialog()
  },
  onError: (err) => {
    Notify.create({ type: 'negative', message: getErrorMessage(err, 'Unable to create resource') })
  }
})

const updateMutation = useMutation({
  mutationFn: ({ id, payload }: { id: string; payload: UpdateExternalResource }) =>
    client.externalResourcePUT(id, payload),
  onSuccess: () => {
    queryClient.invalidateQueries({ queryKey: ['externalResources'] })
    Notify.create({ type: 'positive', message: 'Resource updated' })
    closeDialog()
  },
  onError: (err) => {
    Notify.create({ type: 'negative', message: getErrorMessage(err, 'Unable to update resource') })
  }
})

const deleteMutation = useMutation({
  mutationFn: (id: string) => client.externalResourceDELETE(id),
  onSuccess: () => {
    queryClient.invalidateQueries({ queryKey: ['externalResources'] })
    Notify.create({ type: 'positive', message: 'Resource deleted' })
  },
  onError: (err) => {
    Notify.create({ type: 'negative', message: getErrorMessage(err, 'Unable to delete resource') })
  }
})

const isAnyPending = computed(() => createMutation.isPending.value || updateMutation.isPending.value)

function handleSubmit(model: ExternalResourceFormModel) {
  if (dialogMode.value === 'create') {
    const payload = Object.assign(new CreateExternalResource(), {
      name: model.name || undefined,
      resourceUri: model.resourceUri || undefined,
      description: model.description || undefined,
      tagIds: model.tagIds.length ? [...model.tagIds] : undefined
    })
    createMutation.mutate(payload)
  } else if (dialogMode.value === 'edit' && selectedResource.value?.id) {
    const payload = Object.assign(new UpdateExternalResource(), {
      name: model.name || undefined,
      resourceUri: model.resourceUri || undefined,
      description: model.description || undefined,
      tagIds: model.tagIds.length ? [...model.tagIds] : undefined
    })
    updateMutation.mutate({ id: selectedResource.value.id, payload })
  }
}

function confirmDelete(resource: ExternalResource) {
  if (!resource.id) return
  Dialog.create({
    title: 'Delete resource',
    message: `Delete "${resource.name ?? 'this resource'}"?`,
    cancel: true,
    persistent: true
  }).onOk(() => deleteMutation.mutate(resource.id!))
}
</script>

<style scoped>
@import '../styles/pages.css';
</style>
