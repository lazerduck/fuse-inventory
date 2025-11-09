<template>
  <div class="page-container">
    <div class="page-header">
      <div>
        <h1>Platforms</h1>
        <p class="subtitle">Catalogue infrastructure and link it to applications.</p>
      </div>
      <q-btn color="primary" label="Create Platform" icon="add" @click="openCreateDialog" />
    </div>

    <q-banner v-if="platformError" dense class="bg-red-1 text-negative q-mb-md">
      {{ platformError }}
    </q-banner>

    <q-card class="content-card">
      <q-table
        flat
        bordered
        :rows="platforms"
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
        <template #body-cell-kind="props">
          <q-td :props="props">
            {{ props.row.kind ?? '—' }}
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
          <div class="q-pa-md text-grey-7">No platforms recorded.</div>
        </template>
      </q-table>
    </q-card>

    <q-dialog v-model="isDialogOpen" persistent>
      <PlatformForm
        :mode="dialogMode"
        :initial-value="selectedPlatform"
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
import { Platform, CreatePlatform, UpdatePlatform } from '../api/client'
import { useFuseClient } from '../composables/useFuseClient'
import { useEnvironments } from '../composables/useEnvironments'
import { useTags } from '../composables/useTags'
import { getErrorMessage } from '../utils/error'
import PlatformForm, { type PlatformFormModel } from '../components/platforms/PlatformForm.vue'

const client = useFuseClient()
const queryClient = useQueryClient()
const environmentsStore = useEnvironments()
const tagsStore = useTags()

const pagination = { rowsPerPage: 10 }

const { data, isLoading, error } = useQuery({
  queryKey: ['platforms'],
  queryFn: () => client.platformAll()
})

const platforms = computed(() => data.value ?? [])
const platformError = computed(() => (error.value ? getErrorMessage(error.value) : null))

const environmentLookup = environmentsStore.lookup
const tagLookup = tagsStore.lookup

const columns: QTableColumn<Platform>[] = [
  { name: 'name', label: 'Name', field: 'displayName', align: 'left', sortable: true },
  { name: 'dnsName', label: 'DNS Name', field: 'dnsName', align: 'left' },
  { name: 'os', label: 'Operating System', field: 'os', align: 'left' },
  { name: 'kind', label: 'Kind', field: 'kind', align: 'left' },
  { name: 'ipAddress', label: 'IP', field: 'ipAddress', align: 'left' },
  { name: 'tags', label: 'Tags', field: 'tagIds', align: 'left' },
  { name: 'actions', label: '', field: (row) => row.id, align: 'right' }
]

const isDialogOpen = ref(false)
const dialogMode = ref<'create' | 'edit'>('create')
const selectedPlatform = ref<Platform | null>(null)

function openCreateDialog() {
  selectedPlatform.value = null
  dialogMode.value = 'create'
  isDialogOpen.value = true
}

function openEditDialog(platform: Platform) {
  if (!platform.id) return
  selectedPlatform.value = platform
  dialogMode.value = 'edit'
  isDialogOpen.value = true
}

function closeDialog() {
  selectedPlatform.value = null
  isDialogOpen.value = false
}

const createMutation = useMutation({
  mutationFn: (payload: CreatePlatform) => client.platformPOST(payload),
  onSuccess: () => {
    queryClient.invalidateQueries({ queryKey: ['platforms'] })
    Notify.create({ type: 'positive', message: 'Platform created' })
    closeDialog()
  },
  onError: (err) => {
    Notify.create({ type: 'negative', message: getErrorMessage(err, 'Unable to create platform') })
  }
})

const updateMutation = useMutation({
  mutationFn: ({ id, payload }: { id: string; payload: UpdatePlatform }) => client.platformPUT(id, payload),
  onSuccess: () => {
    queryClient.invalidateQueries({ queryKey: ['platforms'] })
    Notify.create({ type: 'positive', message: 'Platform updated' })
    closeDialog()
  },
  onError: (err) => {
    Notify.create({ type: 'negative', message: getErrorMessage(err, 'Unable to update platform') })
  }
})

const deleteMutation = useMutation({
  mutationFn: (id: string) => client.platformDELETE(id),
  onSuccess: () => {
    queryClient.invalidateQueries({ queryKey: ['platforms'] })
    Notify.create({ type: 'positive', message: 'Platform deleted' })
  },
  onError: (err) => {
    Notify.create({ type: 'negative', message: getErrorMessage(err, 'Unable to delete platform') })
  }
})

const isAnyPending = computed(() => createMutation.isPending.value || updateMutation.isPending.value)

function handleSubmit(model: PlatformFormModel) {
  if (dialogMode.value === 'create') {
    const payload = Object.assign(new CreatePlatform(), {
      displayName: model.displayName || undefined,
      dnsName: model.dnsName || undefined,
      os: model.os || undefined,
      kind: model.kind || undefined,
      ipAddress: model.ipAddress || undefined,
      notes: model.notes || undefined,
      tagIds: model.tagIds.length ? [...model.tagIds] : undefined
    })
    createMutation.mutate(payload)
  } else if (dialogMode.value === 'edit' && selectedPlatform.value?.id) {
    const payload = Object.assign(new UpdatePlatform(), {
      displayName: model.displayName || undefined,
      dnsName: model.dnsName || undefined,
      os: model.os || undefined,
      kind: model.kind || undefined,
      ipAddress: model.ipAddress || undefined,
      notes: model.notes || undefined,
      tagIds: model.tagIds.length ? [...model.tagIds] : undefined
    })
    updateMutation.mutate({ id: selectedPlatform.value.id, payload })
  }
}

function confirmDelete(platform: Platform) {
  if (!platform.id) return
  Dialog.create({
    title: 'Delete platform',
    message: `Delete "${platform.displayName ?? platform.dnsName ?? 'this platform'}"?`,
    cancel: true,
    persistent: true
  }).onOk(() => deleteMutation.mutate(platform.id!))
}
</script>

<style scoped>
@import '../styles/pages.css';
</style>
