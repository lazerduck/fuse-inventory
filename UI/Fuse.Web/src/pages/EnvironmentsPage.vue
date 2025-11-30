<template>
  <div class="page-container">
    <div class="page-header">
      <div>
        <h1>Environments</h1>
        <p class="subtitle">Model environments and attach resources to them.</p>
      </div>
      <div class="q-gutter-sm">
        <q-btn
          flat
          color="primary"
          label="Run Automation"
          icon="autorenew"
          :disable="!fuseStore.canModify"
          @click="runAutomation"
          data-tour-id="run-automation"
        />
        <q-btn
          color="primary"
          label="Create Environment"
          icon="add"
          :disable="!fuseStore.canModify"
          @click="openCreateDialog"
          data-tour-id="create-environment"
        />
      </div>
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
        :filter="filter"
        data-tour-id="environments-table"
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
import { EnvironmentInfo, CreateEnvironment, UpdateEnvironment, ApplyEnvironmentAutomation } from '../api/client'
import { useFuseClient } from '../composables/useFuseClient'
import { useFuseStore } from '../stores/FuseStore'
import { useTags } from '../composables/useTags'
import { getErrorMessage } from '../utils/error'
import EnvironmentForm from '../components/environments/EnvironmentForm.vue'
import TagChip from '../components/tags/TagChip.vue'

interface EnvironmentFormModel {
  name: string
  description: string
  tagIds: string[]
  autoCreateInstances: boolean
  baseUriTemplate: string
  healthUriTemplate: string
  openApiUriTemplate: string
}

const client = useFuseClient()
const queryClient = useQueryClient()
const fuseStore = useFuseStore()
const tagsStore = useTags()

const pagination = { rowsPerPage: 10 }
const filter = ref('')

const { data, isLoading, error } = useQuery({
  queryKey: ['environments'],
  queryFn: () => client.environmentAll()
})

const environments = computed(() => data.value ?? [])
const environmentError = computed(() => (error.value ? getErrorMessage(error.value) : null))
const tagInfoLookup = tagsStore.tagInfoLookup

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

const automationMutation = useMutation({
  mutationFn: (payload: ApplyEnvironmentAutomation) => client.applyAutomation(payload),
  onSuccess: (instancesCreated) => {
    queryClient.invalidateQueries({ queryKey: ['applications'] })
    const message = instancesCreated === 0 
      ? 'No new instances created (all environments already have instances or automation is not enabled)'
      : `Successfully created ${instancesCreated} instance${instancesCreated === 1 ? '' : 's'}`
    Notify.create({ type: 'positive', message })
  },
  onError: (err) => {
    Notify.create({ type: 'negative', message: getErrorMessage(err, 'Unable to run automation') })
  }
})

const isAnyPending = computed(() => createMutation.isPending.value || updateMutation.isPending.value || automationMutation.isPending.value)

function handleSubmit(model: EnvironmentFormModel) {
  if (dialogMode.value === 'create') {
    const payload = Object.assign(new CreateEnvironment(), {
      name: model.name || undefined,
      description: model.description || undefined,
      tagIds: model.tagIds.length ? [...model.tagIds] : undefined,
      autoCreateInstances: model.autoCreateInstances,
      baseUriTemplate: model.baseUriTemplate || undefined,
      healthUriTemplate: model.healthUriTemplate || undefined,
      openApiUriTemplate: model.openApiUriTemplate || undefined
    })
    createMutation.mutate(payload)
  } else if (dialogMode.value === 'edit' && selectedEnvironment.value?.id) {
    const payload = Object.assign(new UpdateEnvironment(), {
      name: model.name || undefined,
      description: model.description || undefined,
      tagIds: model.tagIds.length ? [...model.tagIds] : undefined,
      autoCreateInstances: model.autoCreateInstances,
      baseUriTemplate: model.baseUriTemplate || undefined,
      healthUriTemplate: model.healthUriTemplate || undefined,
      openApiUriTemplate: model.openApiUriTemplate || undefined
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

function runAutomation() {
  Dialog.create({
    title: 'Run automation',
    message: 'This will create instances for all applications in environments that have auto-create enabled. Existing instances will not be modified. Continue?',
    cancel: true,
    persistent: true
  }).onOk(() => {
    const payload = new ApplyEnvironmentAutomation()
    automationMutation.mutate(payload)
  })
}
</script>

<style scoped>
@import '../styles/pages.css';
</style>
