<template>
  <div class="page-container">
    <div class="page-header">
      <div>
        <h1>Positions</h1>
        <p class="subtitle">Positions represent organisational functions, not individuals.</p>
      </div>
      <q-btn
        color="primary"
        label="Create Position"
        icon="add"
        :disable="!fuseStore.canModify"
        @click="openCreateDialog"
      />
    </div>

    <q-banner v-if="positionError" dense class="bg-red-1 text-negative q-mb-md">
      {{ positionError }}
    </q-banner>

    <q-banner v-if="!fuseStore.canRead" dense class="bg-orange-1 text-orange-9 q-mb-md">
      You do not have permission to view positions. Please log in with appropriate credentials.
    </q-banner>

    <q-card v-if="fuseStore.canRead" class="content-card">
      <q-table
        flat
        bordered
        :rows="positions"
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
        <template #body-cell-tags="props">
          <q-td :props="props">
            <div v-if="props.row.tagIds?.length" class="tag-list">
              <TagChip
                v-for="tagId in props.row.tagIds"
                :key="tagId"
                :label="tagInfoLookup[tagId]?.name ?? formatGuid(tagId)"
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
          <div class="q-pa-md text-grey-7">No positions defined yet.</div>
        </template>
      </q-table>
    </q-card>

    <PositionDialog
      v-model="isDialogOpen"
      :position="selectedPosition"
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
import { Position, CreatePosition, UpdatePosition } from '../api/client'
import { useFuseClient } from '../composables/useFuseClient'
import { useFuseStore } from '../stores/FuseStore'
import { useTags } from '../composables/useTags'
import { getErrorMessage } from '../utils/error'
import TagChip from '../components/tags/TagChip.vue'
import PositionDialog from '../components/position/PositionDialog.vue'

interface PositionFormModel {
  name: string
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
  queryKey: ['positions'],
  queryFn: () => client.positionAll()
})

const positions = computed(() => data.value ?? [])
const positionError = computed(() => (error.value ? getErrorMessage(error.value) : null))
const tagInfoLookup = tagsStore.tagInfoLookup

const columns: QTableColumn<Position>[] = [
  { name: 'name', label: 'Name', field: 'name', align: 'left', sortable: true },
  { name: 'description', label: 'Description', field: 'description', align: 'left' },
  { name: 'tags', label: 'Tags', field: 'tagIds', align: 'left' },
  { name: 'actions', label: '', field: (row) => row.id, align: 'right' }
]

const isDialogOpen = ref(false)
const selectedPosition = ref<Position | null>(null)

function openCreateDialog() {
  selectedPosition.value = null
  isDialogOpen.value = true
}

function openEditDialog(position: Position) {
  if (!position.id) return
  selectedPosition.value = position
  isDialogOpen.value = true
}

const createMutation = useMutation({
  mutationFn: (payload: CreatePosition) => client.positionPOST(payload),
  onSuccess: () => {
    queryClient.invalidateQueries({ queryKey: ['positions'] })
    Notify.create({ type: 'positive', message: 'Position created' })
    isDialogOpen.value = false
  },
  onError: (err) => {
    Notify.create({ type: 'negative', message: getErrorMessage(err, 'Unable to create position') })
  }
})

const updateMutation = useMutation({
  mutationFn: ({ id, payload }: { id: string; payload: UpdatePosition }) => client.positionPUT(id, payload),
  onSuccess: () => {
    queryClient.invalidateQueries({ queryKey: ['positions'] })
    Notify.create({ type: 'positive', message: 'Position updated' })
    isDialogOpen.value = false
    selectedPosition.value = null
  },
  onError: (err) => {
    Notify.create({ type: 'negative', message: getErrorMessage(err, 'Unable to update position') })
  }
})

const deleteMutation = useMutation({
  mutationFn: (id: string) => client.positionDELETE(id),
  onSuccess: () => {
    queryClient.invalidateQueries({ queryKey: ['positions'] })
    Notify.create({ type: 'positive', message: 'Position deleted' })
  },
  onError: (err) => {
    Notify.create({ type: 'negative', message: getErrorMessage(err, 'Unable to delete position') })
  }
})

const isAnyPending = computed(() => createMutation.isPending.value || updateMutation.isPending.value)

function handleSave(model: PositionFormModel) {
  const payloadBase = {
    name: model.name || undefined,
    description: model.description || undefined,
    tagIds: model.tagIds.length ? [...model.tagIds] : undefined
  }

  if (!selectedPosition.value) {
    const payload = Object.assign(new CreatePosition(), payloadBase)
    createMutation.mutate(payload)
  } else if (selectedPosition.value.id) {
    const payload = Object.assign(new UpdatePosition(), payloadBase)
    updateMutation.mutate({ id: selectedPosition.value.id, payload })
  }
}

function confirmDelete(position: Position) {
  if (!position.id) return
  Dialog.create({
    title: 'Delete position',
    message: `Delete "${position.name ?? formatGuid(position.id)}"?`,
    cancel: true,
    persistent: true
  }).onOk(() => deleteMutation.mutate(position.id!))
}

function formatGuid(value?: string | null) {
  return value ? value.toUpperCase() : '—'
}
</script>

<style scoped>
@import '../styles/pages.css';
</style>
