<template>
  <div class="page-container">
    <div class="page-header">
      <div>
        <h1>External Resources</h1>
        <p class="subtitle">Track third-party services, SaaS accounts, and shared tooling.</p>
      </div>
      <q-btn color="primary" label="Create Resource" icon="add" @click="openCreateDialog" />
    </div>

    <q-banner v-if="resourceError" dense class="bg-red-1 text-negative q-mb-md">
      {{ resourceError }}
    </q-banner>

    <q-card class="content-card">
      <q-table
        flat
        bordered
        :rows="resources"
        :columns="columns"
        row-key="id"
        :loading="isLoading"
        :pagination="pagination"
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
          <div class="q-pa-md text-grey-7">No external resources yet.</div>
        </template>
      </q-table>
    </q-card>

    <q-dialog v-model="isCreateDialogOpen" persistent>
      <q-card class="form-dialog">
        <q-card-section class="dialog-header">
          <div class="text-h6">Create Resource</div>
          <q-btn flat round dense icon="close" @click="isCreateDialogOpen = false" />
        </q-card-section>
        <q-separator />
        <q-form @submit.prevent="submitCreate">
          <q-card-section>
            <div class="form-grid">
              <q-input v-model="createForm.name" label="Name" dense outlined />
              <q-input v-model="createForm.resourceUri" label="Resource URI" dense outlined />
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
              <q-input
                v-model="createForm.description"
                type="textarea"
                label="Description"
                dense
                outlined
                autogrow
                class="full-span"
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
          <div class="text-h6">Edit Resource</div>
          <q-btn flat round dense icon="close" @click="closeEditDialog" />
        </q-card-section>
        <q-separator />
        <q-form @submit.prevent="submitEdit">
          <q-card-section>
            <div class="form-grid">
              <q-input v-model="editForm.name" label="Name" dense outlined />
              <q-input v-model="editForm.resourceUri" label="Resource URI" dense outlined />
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
              <q-input
                v-model="editForm.description"
                type="textarea"
                label="Description"
                dense
                outlined
                autogrow
                class="full-span"
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
import { ExternalResource, CreateExternalResource, UpdateExternalResource } from '../api/client'
import { useFuseClient } from '../composables/useFuseClient'
import { useTags } from '../composables/useTags'
import { getErrorMessage } from '../utils/error'

interface ExternalResourceForm {
  name: string
  resourceUri: string
  description: string
  tagIds: string[]
}

const client = useFuseClient()
const queryClient = useQueryClient()
const tagsStore = useTags()

const pagination = { rowsPerPage: 10 }

const { data, isLoading, error } = useQuery({
  queryKey: ['externalResources'],
  queryFn: () => client.externalResourceAll()
})

const resources = computed(() => data.value ?? [])
const resourceError = computed(() => (error.value ? getErrorMessage(error.value) : null))

const tagOptions = tagsStore.options
const tagLookup = tagsStore.lookup

const columns: QTableColumn<ExternalResource>[] = [
  { name: 'name', label: 'Name', field: 'name', align: 'left', sortable: true },
  { name: 'resourceUri', label: 'Resource URI', field: 'resourceUri', align: 'left' },
  { name: 'description', label: 'Description', field: 'description', align: 'left' },
  { name: 'tags', label: 'Tags', field: 'tagIds', align: 'left' },
  { name: 'actions', label: '', field: (row) => row.id, align: 'right' }
]

const emptyForm = (): ExternalResourceForm => ({
  name: '',
  resourceUri: '',
  description: '',
  tagIds: []
})

const isCreateDialogOpen = ref(false)
const isEditDialogOpen = ref(false)
const selectedResource = ref<ExternalResource | null>(null)

const createForm = reactive<ExternalResourceForm>(emptyForm())
const editForm = reactive<ExternalResourceForm & { id: string | null }>({ id: null, ...emptyForm() })

function openCreateDialog() {
  Object.assign(createForm, emptyForm())
  isCreateDialogOpen.value = true
}

function openEditDialog(resource: ExternalResource) {
  if (!resource.id) return
  selectedResource.value = resource
  Object.assign(editForm, {
    id: resource.id ?? null,
    name: resource.name ?? '',
    resourceUri: resource.resourceUri ?? '',
    description: resource.description ?? '',
    tagIds: [...(resource.tagIds ?? [])]
  })
  isEditDialogOpen.value = true
}

function closeEditDialog() {
  selectedResource.value = null
  isEditDialogOpen.value = false
}

const createMutation = useMutation({
  mutationFn: (payload: CreateExternalResource) => client.externalResourcePOST(payload),
  onSuccess: () => {
    queryClient.invalidateQueries({ queryKey: ['externalResources'] })
    Notify.create({ type: 'positive', message: 'Resource created' })
    isCreateDialogOpen.value = false
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
    closeEditDialog()
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

function submitCreate() {
  const payload = Object.assign(new CreateExternalResource(), {
    name: createForm.name || undefined,
    resourceUri: createForm.resourceUri || undefined,
    description: createForm.description || undefined,
    tagIds: createForm.tagIds.length ? [...createForm.tagIds] : undefined
  })
  createMutation.mutate(payload)
}

function submitEdit() {
  if (!editForm.id) return
  const payload = Object.assign(new UpdateExternalResource(), {
    name: editForm.name || undefined,
    resourceUri: editForm.resourceUri || undefined,
    description: editForm.description || undefined,
    tagIds: editForm.tagIds.length ? [...editForm.tagIds] : undefined
  })
  updateMutation.mutate({ id: editForm.id, payload })
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
