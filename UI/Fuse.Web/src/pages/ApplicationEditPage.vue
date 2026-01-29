<template>
  <div class="page-container">
    <div class="page-header">
      <div>
        <q-btn flat round dense icon="arrow_back" @click="navigateBack" class="q-mr-md" />
        <div style="display: inline-block">
          <h1>{{ applicationName }}</h1>
          <p class="subtitle">Edit application details, instances, and pipelines.</p>
        </div>
      </div>
      <div class="application-nav-buttons">
        <q-btn
          flat
          round
          dense
          icon="chevron_left"
          :disable="!previousApplication"
          @click="navigateToPreviousApplication"
        >
          <q-tooltip v-if="previousApplication">{{ previousApplication.name }}</q-tooltip>
        </q-btn>
        <q-btn
          flat
          round
          dense
          icon="chevron_right"
          :disable="!nextApplication"
          @click="navigateToNextApplication"
        >
          <q-tooltip v-if="nextApplication">{{ nextApplication.name }}</q-tooltip>
        </q-btn>
      </div>
    </div>

    <q-banner v-if="applicationError" dense class="bg-red-1 text-negative q-mb-md">
      {{ applicationError }}
    </q-banner>

    <q-tabs v-model="activeTab" class="text-primary q-mb-md" align="left" narrow-indicator>
      <q-tab name="details" label="Details" />
      <q-tab name="instances" label="Instances" />
      <q-tab name="ownership" label="Ownership" />
      <q-tab name="pipelines" label="Pipelines" />
    </q-tabs>

    <q-tab-panels v-model="activeTab" animated>
      <q-tab-panel name="details" class="q-pa-none">
        <ApplicationDetailsForm
          :initial-value="application"
          :loading="updateApplicationMutation.isPending.value"
          @cancel="navigateBack"
          @delete="confirmApplicationDelete"
          @submit="handleSubmitApplication"
        />
      </q-tab-panel>

      <q-tab-panel name="instances" class="q-pa-none">
        <q-card class="content-card q-mb-md" data-tour-id="application-detail">
          <q-card-section class="dialog-header">
            <div>
              <div class="text-h6">Application Instances</div>
              <div class="text-caption text-grey-7">
                Track deployments across environments and hosts.
              </div>
            </div>
            <q-btn
              color="primary"
              label="Add Instance"
              dense
              icon="add"
              :disable="!fuseStore.canModify"
              @click="openInstanceDialog()"
              data-tour-id="add-instance"
            />
          </q-card-section>
          <q-separator />
          <q-table
            flat
            bordered
            dense
            :rows="application?.instances ?? []"
            :columns="instanceColumns"
            row-key="id"
            data-tour-id="application-instances-table"
          >
            <template #body-cell-environment="props">
              <q-td :props="props">
                {{ environmentLookup[props.row.environmentId ?? ''] ?? '—' }}
              </q-td>
            </template>
            <template #body-cell-platform="props">
              <q-td :props="props">
                {{ platformLookup[props.row.platformId ?? ''] ?? '—' }}
              </q-td>
            </template>
            <template #body-cell-tags="props">
              <q-td :props="props">
                <div v-if="props.row.tagIds?.length" class="tag-list">
                  <q-badge
                    v-for="tagId in props.row.tagIds"
                    :key="tagId"
                    outline
                    color="secondary"
                    :label="tagLookup[tagId] ?? tagId"
                  />
                </div>
                <span v-else class="text-grey">—</span>
              </q-td>
            </template>
            <template #body-cell-actions="props">
              <q-td :props="props" class="text-right">
                <q-btn
                  dense
                  flat
                  round
                  icon="edit"
                  color="primary"
                  :disable="!fuseStore.canModify"
                  @click="navigateToInstance(props.row)"
                />
                <q-btn
                  dense
                  flat
                  round
                  icon="delete"
                  color="negative"
                  class="q-ml-xs"
                  :disable="!fuseStore.canModify"
                  @click="confirmInstanceDelete(props.row)"
                />
              </q-td>
            </template>
            <template #no-data>
              <div class="q-pa-sm text-grey-7">No instances defined.</div>
            </template>
          </q-table>
        </q-card>
      </q-tab-panel>

      <q-tab-panel name="ownership" class="q-pa-none">
        <ResponsibilityAssignmentTable :application-id="applicationId" />
      </q-tab-panel>

      <q-tab-panel name="pipelines" class="q-pa-none">
        <q-card class="content-card">
          <q-card-section class="dialog-header">
            <div>
              <div class="text-h6">Delivery Pipelines</div>
              <div class="text-caption text-grey-7">
                Document CI/CD workflows powering this application.
              </div>
            </div>
            <q-btn 
              color="primary" 
              label="Add Pipeline" 
              dense 
              icon="add" 
              :disable="!fuseStore.canModify"
              @click="openPipelineDialog()" 
            />
          </q-card-section>
          <q-separator />
          <q-table
            flat
            bordered
            dense
            :rows="application?.pipelines ?? []"
            :columns="pipelineColumns"
            row-key="id"
          >
            <template #body-cell-actions="props">
              <q-td :props="props" class="text-right">
                <q-btn
                  dense
                  flat
                  round
                  icon="edit"
                  color="primary"
                  :disable="!fuseStore.canModify"
                  @click="openPipelineDialog(props.row)"
                />
                <q-btn
                  dense
                  flat
                  round
                  icon="delete"
                  color="negative"
                  class="q-ml-xs"
                  :disable="!fuseStore.canModify"
                  @click="confirmPipelineDelete(props.row)"
                />
              </q-td>
            </template>
            <template #no-data>
              <div class="q-pa-sm text-grey-7">No pipelines documented.</div>
            </template>
          </q-table>
        </q-card>
      </q-tab-panel>
    </q-tab-panels>

    <q-dialog v-model="isInstanceDialogOpen" persistent>
      <ApplicationInstanceForm
        mode="create"
        :loading="createInstanceMutation.isPending.value"
        @submit="handleSubmitInstance"
        @cancel="closeInstanceDialog"
      />
    </q-dialog>

    <q-dialog v-model="isPipelineDialogOpen" persistent>
      <ApplicationPipelineForm
        :mode="editingPipeline ? 'edit' : 'create'"
        :initial-value="editingPipeline"
        :loading="pipelineMutationPending"
        @submit="handleSubmitPipeline"
        @cancel="closePipelineDialog"
      />
    </q-dialog>
  </div>
</template>

<script setup lang="ts">
import { computed, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useQuery, useMutation, useQueryClient } from '@tanstack/vue-query'
import { Notify, Dialog } from 'quasar'
import type { QTableColumn } from 'quasar'
import {
  ApplicationInstance,
  ApplicationPipeline,
  CreateApplicationInstance,
  CreateApplicationPipeline,
  UpdateApplication,
  UpdateApplicationPipeline
} from '../api/client'
import { useFuseClient } from '../composables/useFuseClient'
import { useFuseStore } from '../stores/FuseStore'
import { useTags } from '../composables/useTags'
import { useEnvironments } from '../composables/useEnvironments'
import { usePlatforms } from '../composables/usePlatforms'
import { getErrorMessage } from '../utils/error'
import ApplicationDetailsForm from '../components/applications/ApplicationDetailsForm.vue'
import ApplicationInstanceForm from '../components/applications/ApplicationInstanceForm.vue'
import ApplicationPipelineForm from '../components/applications/ApplicationPipelineForm.vue'
import ResponsibilityAssignmentTable from '../components/responsibilityassignment/ResponsibilityAssignmentTable.vue'

interface ApplicationInstanceFormModel {
  environmentId: string | null
  platformId: string | null
  baseUri: string
  healthUri: string
  openApiUri: string
  version: string
  tagIds: string[]
}

const route = useRoute()
const router = useRouter()
const client = useFuseClient()
const queryClient = useQueryClient()
const fuseStore = useFuseStore()

const activeTab = ref('details')

const applicationId = computed(() => route.params.id as string)

const { data: applicationsData, error: applicationsErrorRef } = useQuery({
  queryKey: ['applications'],
  queryFn: () => client.applicationAll()
})

const application = computed(() => 
  applicationsData.value?.find((app) => app.id === applicationId.value)
)

const applicationName = computed(() => application.value?.name ?? 'Edit Application')

// Sort applications by name for consistent navigation order
// Note: Vue computed properties are cached, so this sort only re-runs when applicationsData changes
const sortedApplications = computed(() => {
  const apps = applicationsData.value ?? []
  return [...apps].sort((a, b) => (a.name ?? '').localeCompare(b.name ?? ''))
})

const currentApplicationIndex = computed(() => {
  return sortedApplications.value.findIndex((app) => app.id === applicationId.value)
})

const previousApplication = computed(() => {
  const idx = currentApplicationIndex.value
  if (idx <= 0) return null
  return sortedApplications.value[idx - 1]
})

const nextApplication = computed(() => {
  const idx = currentApplicationIndex.value
  if (idx < 0 || idx >= sortedApplications.value.length - 1) return null
  return sortedApplications.value[idx + 1]
})

const applicationError = computed(() => {
  if (applicationsErrorRef.value) {
    return getErrorMessage(applicationsErrorRef.value)
  }
  // Check if data is loaded but application not found
  if (applicationsData.value && !application.value) {
    return 'Application not found'
  }
  return null
})

const tagsStore = useTags()
const environmentsStore = useEnvironments()
const platformsStore = usePlatforms()

const tagLookup = tagsStore.lookup
const environmentLookup = environmentsStore.lookup
const platformLookup = platformsStore.lookup

const instanceColumns: QTableColumn<ApplicationInstance>[] = [
  { name: 'environment', label: 'Environment', field: 'environmentId', align: 'left' },
  { name: 'platform', label: 'Platform', field: 'platformId', align: 'left' },
  { name: 'version', label: 'Version', field: 'version', align: 'left' },
  { name: 'baseUri', label: 'Base URI', field: 'baseUri', align: 'left' },
  { name: 'healthUri', label: 'Health URI', field: 'healthUri', align: 'left' },
  { name: 'tags', label: 'Tags', field: 'tagIds', align: 'left' },
  { name: 'actions', label: '', field: (row) => row.id, align: 'right' }
]

const pipelineColumns: QTableColumn<ApplicationPipeline>[] = [
  { name: 'name', label: 'Name', field: 'name', align: 'left' },
  { name: 'pipelineUri', label: 'Pipeline URI', field: 'pipelineUri', align: 'left' },
  { name: 'actions', label: '', field: (row) => row.id, align: 'right' }
]

function navigateBack() {
  router.push({ name: 'applications' })
}

function navigateToPreviousApplication() {
  if (previousApplication.value?.id) {
    router.push({ name: 'applicationEdit', params: { id: previousApplication.value.id } })
  }
}

function navigateToNextApplication() {
  if (nextApplication.value?.id) {
    router.push({ name: 'applicationEdit', params: { id: nextApplication.value.id } })
  }
}

const updateApplicationMutation = useMutation({
  mutationFn: ({ id, payload }: { id: string; payload: UpdateApplication }) => client.applicationPUT(id, payload),
  onSuccess: () => {
    queryClient.invalidateQueries({ queryKey: ['applications'] })
    Notify.create({ type: 'positive', message: 'Application updated' })
  },
  onError: (error) => {
    Notify.create({ type: 'negative', message: getErrorMessage(error, 'Unable to update application') })
  }
})

const deleteApplicationMutation = useMutation({
  mutationFn: (id: string) => client.applicationDELETE(id),
  onSuccess: () => {
    queryClient.invalidateQueries({ queryKey: ['applications'] })
    Notify.create({ type: 'positive', message: 'Application deleted' })
    navigateBack()
  },
  onError: (error) => {
    Notify.create({ type: 'negative', message: getErrorMessage(error, 'Unable to delete application') })
  }
})

function handleSubmitApplication(model: {
  name: string
  version: string
  description: string
  owner: string
  notes: string
  framework: string
  repositoryUri: string
  icon: string
  tagIds: string[]
}) {
  if (!application.value?.id) return
  const payload = Object.assign(new UpdateApplication(), {
    name: model.name || undefined,
    version: model.version || undefined,
    description: model.description || undefined,
    owner: model.owner || undefined,
    notes: model.notes || undefined,
    framework: model.framework || undefined,
    repositoryUri: model.repositoryUri || undefined,
    icon: model.icon || undefined,
    tagIds: model.tagIds.length ? [...model.tagIds] : undefined
  })
  updateApplicationMutation.mutate({ id: application.value.id, payload })
}

function confirmApplicationDelete() {
  if (!application.value?.id) return
  Dialog.create({
    title: 'Delete application',
    message: `Are you sure you want to delete "${application.value.name ?? 'this application'}"?`,
    cancel: true,
    persistent: true
  }).onOk(() => {
    deleteApplicationMutation.mutate(application.value!.id!)
  })
}

// Instance management
const isInstanceDialogOpen = ref(false)

function openInstanceDialog() {
  if (!application.value?.id) {
    Notify.create({ type: 'warning', message: 'Application not loaded. Please try again.' })
    return
  }
  isInstanceDialogOpen.value = true
}

function navigateToInstance(instance: ApplicationInstance) {
  if (!application.value?.id || !instance.id) {
    Notify.create({ type: 'warning', message: 'Unable to navigate to instance' })
    return
  }
  router.push({
    name: 'instanceEdit',
    params: { applicationId: application.value.id, instanceId: instance.id }
  })
}

function closeInstanceDialog() {
  isInstanceDialogOpen.value = false
}

const createInstanceMutation = useMutation({
  mutationFn: ({ appId, payload }: { appId: string; payload: CreateApplicationInstance }) =>
    client.instancesPOST(appId, payload),
  onSuccess: (createdInstance) => {
    queryClient.invalidateQueries({ queryKey: ['applications'] })
    Notify.create({ type: 'positive', message: 'Instance created' })
    closeInstanceDialog()
    if (createdInstance?.id && application.value?.id) {
      router.push({
        name: 'instanceEdit',
        params: { applicationId: application.value.id, instanceId: createdInstance.id }
      })
    }
  },
  onError: (error) => {
    Notify.create({ type: 'negative', message: getErrorMessage(error, 'Unable to create instance') })
  }
})

const deleteInstanceMutation = useMutation({
  mutationFn: ({ appId, instanceId }: { appId: string; instanceId: string }) => client.instancesDELETE(appId, instanceId),
  onSuccess: () => {
    queryClient.invalidateQueries({ queryKey: ['applications'] })
    Notify.create({ type: 'positive', message: 'Instance removed' })
  },
  onError: (error) => {
    Notify.create({ type: 'negative', message: getErrorMessage(error, 'Unable to delete instance') })
  }
})

function handleSubmitInstance(model: ApplicationInstanceFormModel) {
  if (!application.value?.id) return
  const payload = Object.assign(new CreateApplicationInstance(), {
    environmentId: model.environmentId ?? undefined,
    platformId: model.platformId ?? undefined,
    baseUri: model.baseUri || undefined,
    healthUri: model.healthUri || undefined,
    openApiUri: model.openApiUri || undefined,
    version: model.version || undefined,
    tagIds: model.tagIds.length ? [...model.tagIds] : undefined
  })
  createInstanceMutation.mutate({ appId: application.value.id!, payload })
}

function confirmInstanceDelete(instance: ApplicationInstance) {
  if (!application.value?.id || !instance.id) return
  Dialog.create({
    title: 'Remove instance',
    message: 'Are you sure you want to remove this instance?',
    cancel: true,
    persistent: true
  }).onOk(() => {
    deleteInstanceMutation.mutate({ appId: application.value!.id!, instanceId: instance.id! })
  })
}



function handleSubmitPipeline(model: { name: string; pipelineUri: string }) {
  if (!application.value?.id) return
  const payload = Object.assign(new UpdateApplicationPipeline(), {
    name: model.name || undefined,
    pipelineUri: model.pipelineUri || undefined
  })
  if (editingPipeline.value?.id) {
    updatePipelineMutation.mutate({ appId: application.value.id!, pipelineId: editingPipeline.value.id!, payload })
  } else {
    const createPayload = Object.assign(new CreateApplicationPipeline(), payload)
    createPipelineMutation.mutate({ appId: application.value.id!, payload: createPayload })
  }
}

// Pipeline management
const isPipelineDialogOpen = ref(false)
const editingPipeline = ref<ApplicationPipeline | null>(null)

function openPipelineDialog(pipeline?: ApplicationPipeline) {
  if (!application.value?.id) {
    Notify.create({ type: 'warning', message: 'Application not loaded. Please try again.' })
    return
  }
  if (pipeline) {
    editingPipeline.value = pipeline
  } else {
    editingPipeline.value = null
  }
  isPipelineDialogOpen.value = true
}

function closePipelineDialog() {
  isPipelineDialogOpen.value = false
  editingPipeline.value = null
}

const createPipelineMutation = useMutation({
  mutationFn: ({ appId, payload }: { appId: string; payload: CreateApplicationPipeline }) =>
    client.pipelinesPOST(appId, payload),
  onSuccess: () => {
    queryClient.invalidateQueries({ queryKey: ['applications'] })
    Notify.create({ type: 'positive', message: 'Pipeline created' })
    closePipelineDialog()
  },
  onError: (error) => {
    Notify.create({ type: 'negative', message: getErrorMessage(error, 'Unable to create pipeline') })
  }
})

const updatePipelineMutation = useMutation({
  mutationFn: ({ appId, pipelineId, payload }: { appId: string; pipelineId: string; payload: UpdateApplicationPipeline }) =>
    client.pipelinesPUT(appId, pipelineId, payload),
  onSuccess: () => {
    queryClient.invalidateQueries({ queryKey: ['applications'] })
    Notify.create({ type: 'positive', message: 'Pipeline updated' })
    closePipelineDialog()
  },
  onError: (error) => {
    Notify.create({ type: 'negative', message: getErrorMessage(error, 'Unable to update pipeline') })
  }
})

const deletePipelineMutation = useMutation({
  mutationFn: ({ appId, pipelineId }: { appId: string; pipelineId: string }) => client.pipelinesDELETE(appId, pipelineId),
  onSuccess: () => {
    queryClient.invalidateQueries({ queryKey: ['applications'] })
    Notify.create({ type: 'positive', message: 'Pipeline removed' })
  },
  onError: (error) => {
    Notify.create({ type: 'negative', message: getErrorMessage(error, 'Unable to delete pipeline') })
  }
})

const pipelineMutationPending = computed(() => createPipelineMutation.isPending.value || updatePipelineMutation.isPending.value)

function confirmPipelineDelete(pipeline: ApplicationPipeline) {
  if (!application.value?.id || !pipeline.id) return
  Dialog.create({
    title: 'Remove pipeline',
    message: 'Are you sure you want to remove this pipeline?',
    cancel: true,
    persistent: true
  }).onOk(() => {
    deletePipelineMutation.mutate({ appId: application.value!.id!, pipelineId: pipeline.id! })
  })
}
</script>

<style scoped>
@import '../styles/pages.css';

.form-dialog {
  min-width: 500px;
  max-width: 700px;
}

.application-nav-buttons {
  display: flex;
  gap: 4px;
  align-items: center;
}
</style>
