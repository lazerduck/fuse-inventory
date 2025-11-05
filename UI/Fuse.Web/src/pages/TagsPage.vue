<template>
  <div class="page-container">
    <div class="page-header">
      <div>
        <h1>Tags</h1>
        <p class="subtitle">Organise your inventory with reusable labels and colours.</p>
      </div>
      <q-btn color="primary" label="Create Tag" icon="add" @click="openCreateDialog" />
    </div>

    <q-banner v-if="tagError" dense class="bg-red-1 text-negative q-mb-md">
      {{ tagError }}
    </q-banner>

    <q-card class="content-card">
      <q-table
        flat
        bordered
        :rows="tags"
        :columns="columns"
        row-key="id"
        :loading="isLoading"
        :pagination="pagination"
      >
        <template #body-cell-color="props">
          <q-td :props="props">
            <q-badge
              v-if="props.row.color"
              :label="props.row.color"
              :color="badgeColor(props.row.color)"
              class="uppercase"
            />
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
          <div class="q-pa-md text-grey-7">No tags defined yet.</div>
        </template>
      </q-table>
    </q-card>

    <q-dialog v-model="isCreateDialogOpen" persistent>
      <q-card class="form-dialog">
        <q-card-section class="dialog-header">
          <div class="text-h6">Create Tag</div>
          <q-btn flat round dense icon="close" @click="isCreateDialogOpen = false" />
        </q-card-section>
        <q-separator />
        <q-form @submit.prevent="submitCreate">
          <q-card-section>
            <div class="form-grid">
              <q-input v-model="createForm.name" label="Name" dense outlined />
              <q-select
                v-model="createForm.color"
                label="Color"
                dense
                outlined
                emit-value
                map-options
                :options="colorOptions"
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
          <div class="text-h6">Edit Tag</div>
          <q-btn flat round dense icon="close" @click="closeEditDialog" />
        </q-card-section>
        <q-separator />
        <q-form @submit.prevent="submitEdit">
          <q-card-section>
            <div class="form-grid">
              <q-input v-model="editForm.name" label="Name" dense outlined />
              <q-select
                v-model="editForm.color"
                label="Color"
                dense
                outlined
                emit-value
                map-options
                :options="colorOptions"
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
import { useMutation, useQueryClient } from '@tanstack/vue-query'
import { Notify, Dialog } from 'quasar'
import type { QTableColumn } from 'quasar'
import { Tag, TagColor, CreateTag, UpdateTag } from '../api/client'
import { useTags } from '../composables/useTags'
import { useFuseClient } from '../composables/useFuseClient'
import { getErrorMessage } from '../utils/error'

interface TagForm {
  name: string
  description: string
  color: TagColor | null
}

const client = useFuseClient()
const queryClient = useQueryClient()
const tagsStore = useTags()

const pagination = { rowsPerPage: 10 }

const tags = computed(() => tagsStore.data.value ?? [])
const isLoading = computed(() => tagsStore.isLoading.value)
const tagError = computed(() => (tagsStore.error.value ? getErrorMessage(tagsStore.error.value) : null))

const colorOptions = Object.values(TagColor).map((value) => ({ label: value, value }))

const columns: QTableColumn<Tag>[] = [
  { name: 'name', label: 'Name', field: 'name', align: 'left', sortable: true },
  { name: 'description', label: 'Description', field: 'description', align: 'left' },
  { name: 'color', label: 'Color', field: 'color', align: 'left' },
  { name: 'actions', label: '', field: (row) => row.id, align: 'right' }
]

const isCreateDialogOpen = ref(false)
const isEditDialogOpen = ref(false)
const selectedTag = ref<Tag | null>(null)

const createForm = reactive<TagForm>({ name: '', description: '', color: null })
const editForm = reactive<TagForm & { id: string | null }>({ id: null, name: '', description: '', color: null })

function openCreateDialog() {
  Object.assign(createForm, { name: '', description: '', color: null })
  isCreateDialogOpen.value = true
}

function openEditDialog(tag: Tag) {
  if (!tag.id) return
  selectedTag.value = tag
  Object.assign(editForm, {
    id: tag.id ?? null,
    name: tag.name ?? '',
    description: tag.description ?? '',
    color: tag.color ?? null
  })
  isEditDialogOpen.value = true
}

function closeEditDialog() {
  selectedTag.value = null
  isEditDialogOpen.value = false
}

const createMutation = useMutation({
  mutationFn: (payload: CreateTag) => client.tagPOST(payload),
  onSuccess: () => {
    queryClient.invalidateQueries({ queryKey: ['tags'] })
    Notify.create({ type: 'positive', message: 'Tag created' })
    isCreateDialogOpen.value = false
  },
  onError: (err) => {
    Notify.create({ type: 'negative', message: getErrorMessage(err, 'Unable to create tag') })
  }
})

const updateMutation = useMutation({
  mutationFn: ({ id, payload }: { id: string; payload: UpdateTag }) => client.tagPUT(id, payload),
  onSuccess: () => {
    queryClient.invalidateQueries({ queryKey: ['tags'] })
    Notify.create({ type: 'positive', message: 'Tag updated' })
    closeEditDialog()
  },
  onError: (err) => {
    Notify.create({ type: 'negative', message: getErrorMessage(err, 'Unable to update tag') })
  }
})

const deleteMutation = useMutation({
  mutationFn: (id: string) => client.tagDELETE(id),
  onSuccess: () => {
    queryClient.invalidateQueries({ queryKey: ['tags'] })
    Notify.create({ type: 'positive', message: 'Tag deleted' })
  },
  onError: (err) => {
    Notify.create({ type: 'negative', message: getErrorMessage(err, 'Unable to delete tag') })
  }
})

function submitCreate() {
  const payload = Object.assign(new CreateTag(), {
    name: createForm.name || undefined,
    description: createForm.description || undefined,
    color: createForm.color || undefined
  })
  createMutation.mutate(payload)
}

function submitEdit() {
  if (!editForm.id) return
  const payload = Object.assign(new UpdateTag(), {
    name: editForm.name || undefined,
    description: editForm.description || undefined,
    color: editForm.color || undefined
  })
  updateMutation.mutate({ id: editForm.id, payload })
}

function confirmDelete(tag: Tag) {
  if (!tag.id) return
  Dialog.create({
    title: 'Delete tag',
    message: `Delete "${tag.name ?? 'this tag'}"?`,
    cancel: true,
    persistent: true
  }).onOk(() => deleteMutation.mutate(tag.id!))
}

function badgeColor(color: TagColor) {
  switch (color) {
    case TagColor.Red:
      return 'red-6'
    case TagColor.Green:
      return 'green-6'
    case TagColor.Blue:
      return 'blue-6'
    case TagColor.Yellow:
      return 'yellow-7'
    case TagColor.Purple:
      return 'purple-5'
    case TagColor.Orange:
      return 'orange-6'
    case TagColor.Teal:
      return 'teal-5'
    default:
      return 'grey-7'
  }
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
  min-width: 440px;
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

.uppercase {
  text-transform: uppercase;
}
</style>
