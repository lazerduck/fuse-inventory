<template>
  <div class="page-container">
    <div class="page-header">
      <div>
        <h1>Environments</h1>
        <p class="subtitle">Model environments and attach resources to them.</p>
      </div>
      <q-btn color="primary" label="Create Environment" icon="add" @click="openCreateDialog" />
    </div>

    <q-banner v-if="environmentError" dense class="bg-red-1 text-negative q-mb-md">
      {{ environmentError }}
    </q-banner>

    <q-card class="content-card">
      <q-table
        flat
        bordered
        :rows="environments"
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
          <div class="q-pa-md text-grey-7">No environments yet.</div>
        </template>
      </q-table>
    </q-card>

    <q-dialog v-model="isCreateDialogOpen" persistent>
      <q-card class="form-dialog">
        <q-card-section class="dialog-header">
          <div class="text-h6">Create Environment</div>
          <q-btn flat round dense icon="close" @click="isCreateDialogOpen = false" />
        </q-card-section>
        <q-separator />
        <q-form @submit.prevent="submitCreate">
          <q-card-section>
            <div class="form-grid">
              <q-input v-model="createForm.name" label="Name" dense outlined />
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
          <div class="text-h6">Edit Environment</div>
          <q-btn flat round dense icon="close" @click="closeEditDialog" />
        </q-card-section>
        <q-separator />
        <q-form @submit.prevent="submitEdit">
          <q-card-section>
            <div class="form-grid">
              <q-input v-model="editForm.name" label="Name" dense outlined />
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
import { EnvironmentInfo, CreateEnvironment, UpdateEnvironment } from '../api/client'
import { useFuseClient } from '../composables/useFuseClient'
import { useTags } from '../composables/useTags'
import { getErrorMessage } from '../utils/error'

interface EnvironmentForm {
  name: string
  description: string
  tagIds: string[]
}

const client = useFuseClient()
const queryClient = useQueryClient()
const tagsStore = useTags()

const pagination = { rowsPerPage: 10 }

const { data, isLoading, error } = useQuery({
  queryKey: ['environments'],
  queryFn: () => client.environmentAll()
})

const environments = computed(() => data.value ?? [])
const environmentError = computed(() => (error.value ? getErrorMessage(error.value) : null))
const tagOptions = tagsStore.options
const tagLookup = tagsStore.lookup

const columns: QTableColumn<EnvironmentInfo>[] = [
  { name: 'name', label: 'Name', field: 'name', align: 'left', sortable: true },
  { name: 'description', label: 'Description', field: 'description', align: 'left' },
  { name: 'tags', label: 'Tags', field: 'tagIds', align: 'left' },
  { name: 'actions', label: '', field: (row) => row.id, align: 'right' }
]

const isCreateDialogOpen = ref(false)
const isEditDialogOpen = ref(false)
const selectedEnvironment = ref<EnvironmentInfo | null>(null)

const createForm = reactive<EnvironmentForm>({ name: '', description: '', tagIds: [] })
const editForm = reactive<EnvironmentForm & { id: string | null }>({
  id: null,
  name: '',
  description: '',
  tagIds: []
})

function openCreateDialog() {
  Object.assign(createForm, { name: '', description: '', tagIds: [] })
  isCreateDialogOpen.value = true
}

function openEditDialog(env: EnvironmentInfo) {
  if (!env.id) return
  selectedEnvironment.value = env
  Object.assign(editForm, {
    id: env.id ?? null,
    name: env.name ?? '',
    description: env.description ?? '',
    tagIds: [...(env.tagIds ?? [])]
  })
  isEditDialogOpen.value = true
}

function closeEditDialog() {
  selectedEnvironment.value = null
  isEditDialogOpen.value = false
}

const createMutation = useMutation({
  mutationFn: (payload: CreateEnvironment) => client.environmentPOST(payload),
  onSuccess: () => {
    queryClient.invalidateQueries({ queryKey: ['environments'] })
    Notify.create({ type: 'positive', message: 'Environment created' })
    isCreateDialogOpen.value = false
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
    closeEditDialog()
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

function submitCreate() {
  const payload = Object.assign(new CreateEnvironment(), {
    name: createForm.name || undefined,
    description: createForm.description || undefined,
    tagIds: createForm.tagIds.length ? [...createForm.tagIds] : undefined
  })
  createMutation.mutate(payload)
}

function submitEdit() {
  if (!editForm.id) return
  const payload = Object.assign(new UpdateEnvironment(), {
    name: editForm.name || undefined,
    description: editForm.description || undefined,
    tagIds: editForm.tagIds.length ? [...editForm.tagIds] : undefined
  })
  updateMutation.mutate({ id: editForm.id, payload })
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
  min-width: 480px;
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
