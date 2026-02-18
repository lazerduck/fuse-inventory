<template>
  <div class="page-container">
    <div class="page-header">
      <div>
        <h1>Responsibility Types</h1>
        <p class="subtitle">Define accountability labels used to describe ownership.</p>
      </div>
      <q-btn
        color="primary"
        label="Create Responsibility Type"
        icon="add"
        :disable="!fuseStore.canModify"
        @click="openCreateDialog"
      />
    </div>

    <q-banner v-if="typeError" dense class="bg-red-1 text-negative q-mb-md">
      {{ typeError }}
    </q-banner>

    <q-banner v-if="!fuseStore.canRead" dense class="bg-orange-1 text-orange-9 q-mb-md">
      You do not have permission to view responsibility types. Please log in with appropriate credentials.
    </q-banner>

    <q-card v-if="fuseStore.canRead" class="content-card">
      <q-table
        flat
        bordered
        :rows="responsibilityTypes"
        :columns="columns"
        row-key="id"
        :loading="isLoading"
        :pagination="pagination"
        :filter="filter"
      >
        <template #top-right>
          <q-input v-model="filter" dense outlined debounce="300" placeholder="Search...">
            <template #append>
              <q-icon name="search" />
            </template>
          </q-input>
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
          <div class="q-pa-md text-grey-7">No responsibility types defined yet.</div>
        </template>
      </q-table>
    </q-card>

    <ResponsibilityTypeDialog
      v-model="isDialogOpen"
      :responsibility-type="selectedType"
      :loading="isAnyPending"
      @save="handleSave"
    />
  </div>
</template>

<script setup lang="ts">
import { computed, ref } from 'vue'
import { useQuery, useMutation, useQueryClient } from '@tanstack/vue-query'
import { Notify, Dialog } from 'quasar'
import type { QTableColumn } from 'quasar'
import { ResponsibilityType, CreateResponsibilityType, UpdateResponsibilityType } from '../api/client'
import { useFuseClient } from '../composables/useFuseClient'
import { useFuseStore } from '../stores/FuseStore'
import { getErrorMessage } from '../utils/error'
import ResponsibilityTypeDialog from '../components/responsibilitytype/ResponsibilityTypeDialog.vue'

interface ResponsibilityTypeFormModel {
  name: string
  description: string
}

const client = useFuseClient()
const queryClient = useQueryClient()
const fuseStore = useFuseStore()

const pagination = { rowsPerPage: 10 }
const filter = ref('')

const { data, isLoading, error } = useQuery({
  queryKey: ['responsibilityTypes'],
  queryFn: () => client.responsibilityTypeAll()
})

const responsibilityTypes = computed(() => data.value ?? [])
const typeError = computed(() => (error.value ? getErrorMessage(error.value) : null))

const columns: QTableColumn<ResponsibilityType>[] = [
  { name: 'name', label: 'Name', field: 'name', align: 'left', sortable: true },
  { name: 'description', label: 'Description', field: 'description', align: 'left' },
  { name: 'actions', label: '', field: (row) => row.id, align: 'right' }
]

const isDialogOpen = ref(false)
const selectedType = ref<ResponsibilityType | null>(null)

function openCreateDialog() {
  selectedType.value = null
  isDialogOpen.value = true
}

function openEditDialog(type: ResponsibilityType) {
  if (!type.id) return
  selectedType.value = type
  isDialogOpen.value = true
}

const createMutation = useMutation({
  mutationFn: (payload: CreateResponsibilityType) => client.responsibilityTypePOST(payload),
  onSuccess: () => {
    queryClient.invalidateQueries({ queryKey: ['responsibilityTypes'] })
    Notify.create({ type: 'positive', message: 'Responsibility type created' })
    isDialogOpen.value = false
  },
  onError: (err) => {
    Notify.create({ type: 'negative', message: getErrorMessage(err, 'Unable to create responsibility type') })
  }
})

const updateMutation = useMutation({
  mutationFn: ({ id, payload }: { id: string; payload: UpdateResponsibilityType }) =>
    client.responsibilityTypePUT(id, payload),
  onSuccess: () => {
    queryClient.invalidateQueries({ queryKey: ['responsibilityTypes'] })
    Notify.create({ type: 'positive', message: 'Responsibility type updated' })
    isDialogOpen.value = false
    selectedType.value = null
  },
  onError: (err) => {
    Notify.create({ type: 'negative', message: getErrorMessage(err, 'Unable to update responsibility type') })
  }
})

const deleteMutation = useMutation({
  mutationFn: (id: string) => client.responsibilityTypeDELETE(id),
  onSuccess: () => {
    queryClient.invalidateQueries({ queryKey: ['responsibilityTypes'] })
    Notify.create({ type: 'positive', message: 'Responsibility type deleted' })
  },
  onError: (err) => {
    Notify.create({ type: 'negative', message: getErrorMessage(err, 'Unable to delete responsibility type') })
  }
})

const isAnyPending = computed(() => createMutation.isPending.value || updateMutation.isPending.value)

function handleSave(model: ResponsibilityTypeFormModel) {
  const payloadBase = {
    name: model.name || undefined,
    description: model.description || undefined
  }

  if (!selectedType.value) {
    const payload = Object.assign(new CreateResponsibilityType(), payloadBase)
    createMutation.mutate(payload)
  } else if (selectedType.value.id) {
    const payload = Object.assign(new UpdateResponsibilityType(), payloadBase)
    updateMutation.mutate({ id: selectedType.value.id, payload })
  }
}

function confirmDelete(type: ResponsibilityType) {
  if (!type.id) return
  Dialog.create({
    title: 'Delete responsibility type',
    message: `Delete "${type.name ?? formatGuid(type.id)}"?`,
    cancel: true,
    persistent: true
  }).onOk(() => deleteMutation.mutate(type.id!))
}

function formatGuid(value?: string | null) {
  return value ? value.toUpperCase() : '—'
}
</script>

<style scoped>
@import '../styles/pages.css';
</style>
