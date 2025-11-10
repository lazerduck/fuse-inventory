<template>
  <div class="page-container">
    <div class="page-header">
      <div>
        <h1>Applications</h1>
        <p class="subtitle">Manage applications, deployments, and delivery pipelines.</p>
      </div>
      <q-btn
        color="primary"
        label="Create Application"
        icon="add"
        :disable="!fuseStore.canModify"
        @click="openCreateDialog"
        data-tour-id="create-application"
      />
    </div>

    <q-banner v-if="applicationsError" dense class="bg-red-1 text-negative q-mb-md">
      {{ applicationsError }}
    </q-banner>

    <q-banner v-if="!fuseStore.canRead" dense class="bg-orange-1 text-orange-9 q-mb-md">
      You do not have permission to view applications. Please log in with appropriate credentials.
    </q-banner>

    <q-card v-if="fuseStore.canRead" class="content-card">
      <q-table
        flat
        bordered
        :rows="applications ?? []"
        :columns="columns"
        row-key="id"
        :loading="applicationsLoading"
        :pagination="pagination"
        data-tour-id="applications-table"
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
              @click="navigateToEdit(props.row)"
            />
            <q-btn
              flat
              dense
              round
              icon="delete"
              color="negative"
              class="q-ml-xs"
              :disable="!fuseStore.canModify"
              @click="confirmApplicationDelete(props.row)"
            />
          </q-td>
        </template>

        <template #no-data>
          <div class="q-pa-md text-grey-7">
            No applications found. Create one to get started.
          </div>
        </template>
      </q-table>
    </q-card>

    <q-dialog v-model="isCreateDialogOpen" persistent>
      <q-card class="form-dialog">
        <q-card-section class="dialog-header">
          <div class="text-h6">Create Application</div>
          <q-btn flat round dense icon="close" @click="isCreateDialogOpen = false" />
        </q-card-section>
        <q-separator />
        <q-form @submit.prevent="submitCreate">
          <q-card-section>
            <div class="form-grid">
              <q-input v-model="createForm.name" label="Name" dense outlined />
              <q-input v-model="createForm.version" label="Version" dense outlined />
              <q-input v-model="createForm.owner" label="Owner" dense outlined />
              <q-input v-model="createForm.framework" label="Framework" dense outlined />
              <q-input v-model="createForm.repositoryUri" label="Repository URI" dense outlined />
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
              <q-input
                v-model="createForm.notes"
                type="textarea"
                label="Notes"
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
            <q-btn color="primary" type="submit" label="Create" :loading="createApplicationMutation.isPending.value" />
          </q-card-actions>
        </q-form>
      </q-card>
    </q-dialog>
  </div>
</template>

<script setup lang="ts">
import { computed, reactive, ref } from 'vue'
import { useRouter } from 'vue-router'
import { useQuery, useMutation, useQueryClient } from '@tanstack/vue-query'
import { Notify, Dialog } from 'quasar'
import type { QTableColumn } from 'quasar'
import {
  Application,
  CreateApplication
} from '../api/client'
import { useFuseClient } from '../composables/useFuseClient'
import { useFuseStore } from '../stores/FuseStore'
import { useTags } from '../composables/useTags'
import { getErrorMessage } from '../utils/error'

interface ApplicationForm {
  name: string
  version: string
  description: string
  owner: string
  notes: string
  framework: string
  repositoryUri: string
  tagIds: string[]
}

const router = useRouter()
const client = useFuseClient()
const queryClient = useQueryClient()
const fuseStore = useFuseStore()

const pagination = { rowsPerPage: 10 }

const { data: applicationsData, isLoading, error: applicationsErrorRef } = useQuery({
  queryKey: ['applications'],
  queryFn: () => client.applicationAll()
})

const applications = computed(() => applicationsData.value ?? [])
const applicationsLoading = computed(() => isLoading.value)
const applicationsError = computed(() =>
  applicationsErrorRef.value ? getErrorMessage(applicationsErrorRef.value) : null
)

const tagsStore = useTags()

const tagOptions = tagsStore.options
const tagLookup = tagsStore.lookup

const columns: QTableColumn<Application>[] = [
  { name: 'name', label: 'Name', field: 'name', align: 'left', sortable: true },
  { name: 'version', label: 'Version', field: 'version', align: 'left', sortable: true },
  { name: 'owner', label: 'Owner', field: 'owner', align: 'left' },
  { name: 'repositoryUri', label: 'Repository', field: 'repositoryUri', align: 'left' },
  { name: 'tags', label: 'Tags', field: 'tagIds', align: 'left' },
  { name: 'actions', label: '', field: (row) => row.id, align: 'right' }
]

const isCreateDialogOpen = ref(false)

const createForm = reactive<ApplicationForm>(getEmptyApplicationForm())

function getEmptyApplicationForm(): ApplicationForm {
  return {
    name: '',
    version: '',
    description: '',
    owner: '',
    notes: '',
    framework: '',
    repositoryUri: '',
    tagIds: []
  }
}

function resetCreateForm() {
  Object.assign(createForm, getEmptyApplicationForm())
}

function openCreateDialog() {
  resetCreateForm()
  isCreateDialogOpen.value = true
}

function navigateToEdit(app: Application) {
  if (!app.id) return
  router.push({ name: 'applicationEdit', params: { id: app.id } })
}

const createApplicationMutation = useMutation({
  mutationFn: (payload: CreateApplication) => client.applicationPOST(payload),
  onSuccess: () => {
    queryClient.invalidateQueries({ queryKey: ['applications'] })
    isCreateDialogOpen.value = false
    Notify.create({ type: 'positive', message: 'Application created' })
  },
  onError: (error) => {
    Notify.create({ type: 'negative', message: getErrorMessage(error, 'Unable to create application') })
  }
})

const deleteApplicationMutation = useMutation({
  mutationFn: (id: string) => client.applicationDELETE(id),
  onSuccess: () => {
    queryClient.invalidateQueries({ queryKey: ['applications'] })
    Notify.create({ type: 'positive', message: 'Application deleted' })
  },
  onError: (error) => {
    Notify.create({ type: 'negative', message: getErrorMessage(error, 'Unable to delete application') })
  }
})

function submitCreate() {
  const payload = Object.assign(new CreateApplication(), {
    name: createForm.name || undefined,
    version: createForm.version || undefined,
    description: createForm.description || undefined,
    owner: createForm.owner || undefined,
    notes: createForm.notes || undefined,
    framework: createForm.framework || undefined,
    repositoryUri: createForm.repositoryUri || undefined,
    tagIds: createForm.tagIds.length ? [...createForm.tagIds] : undefined
  })
  createApplicationMutation.mutate(payload)
}

function confirmApplicationDelete(app: Application) {
  if (!app.id) return
  Dialog.create({
    title: 'Delete application',
    message: `Are you sure you want to delete "${app.name ?? 'this application'}"?`,
    cancel: true,
    persistent: true
  }).onOk(() => {
    deleteApplicationMutation.mutate(app.id!)
  })
}
</script>

<style scoped>
@import '../styles/pages.css';

.form-dialog {
  min-width: 500px;
  max-width: 700px;
}
</style>
