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
    </div>

    <q-banner v-if="applicationError" dense class="bg-red-1 text-negative q-mb-md">
      {{ applicationError }}
    </q-banner>

    <ApplicationDetailsForm
      :initial-value="application"
      :loading="updateApplicationMutation.isPending.value"
      @cancel="navigateBack"
      @delete="confirmApplicationDelete"
      @submit="handleSubmitApplication"
    />

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
        <template #body-cell-server="props">
          <q-td :props="props">
            {{ serverLookup[props.row.serverId ?? ''] ?? '—' }}
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
              @click="openInstanceDialog(props.row)"
            />
            <q-btn
              dense
              flat
              round
              icon="delete"
              color="negative"
              class="q-ml-xs"
              @click="confirmInstanceDelete(props.row)"
            />
          </q-td>
        </template>
        <template #no-data>
          <div class="q-pa-sm text-grey-7">No instances defined.</div>
        </template>
      </q-table>
    </q-card>

    <q-card class="content-card">
      <q-card-section class="dialog-header">
        <div>
          <div class="text-h6">Delivery Pipelines</div>
          <div class="text-caption text-grey-7">
            Document CI/CD workflows powering this application.
          </div>
        </div>
        <q-btn color="primary" label="Add Pipeline" dense icon="add" @click="openPipelineDialog()" />
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
              @click="openPipelineDialog(props.row)"
            />
            <q-btn
              dense
              flat
              round
              icon="delete"
              color="negative"
              class="q-ml-xs"
              @click="confirmPipelineDelete(props.row)"
            />
          </q-td>
        </template>
        <template #no-data>
          <div class="q-pa-sm text-grey-7">No pipelines documented.</div>
        </template>
      </q-table>
    </q-card>

    <q-dialog v-model="isInstanceDialogOpen" persistent>
      <ApplicationInstanceForm
        :mode="editingInstance ? 'edit' : 'create'"
        :initial-value="editingInstance"
        :loading="instanceMutationPending"
        @submit="handleSubmitInstance"
        @cancel="closeInstanceDialog"
      >
        <InstanceDependenciesSection
          v-if="editingInstance"
          :dependencies="editingInstance.dependencies ?? []"
          :disable-actions="dependencyActionsDisabled"
          :resolve-target-name="resolveDependencyTargetName"
          :resolve-account-name="resolveDependencyAccountName"
          @add="openDependencyDialog()"
          @edit="({ dependency }) => openDependencyDialog(dependency)"
          @delete="({ dependency }) => confirmDependencyDelete(dependency)"
        />
      </ApplicationInstanceForm>
    </q-dialog>

    <q-dialog v-model="isDependencyDialogOpen" persistent>
      <q-card class="form-dialog">
        <q-card-section class="dialog-header">
          <div class="text-h6">{{ editingDependency ? 'Edit Dependency' : 'Add Dependency' }}</div>
          <q-btn flat round dense icon="close" @click="closeDependencyDialog" />
        </q-card-section>
        <q-separator />
        <q-form @submit.prevent="submitDependency">
          <q-card-section>
            <div class="form-grid">
              <q-select
                v-model="dependencyForm.targetKind"
                label="Target Kind"
                dense
                outlined
                emit-value
                map-options
                :options="dependencyTargetKindOptions"
              />
              <q-select
                v-model="dependencyForm.targetId"
                label="Target"
                dense
                outlined
                emit-value
                map-options
                :options="dependencyTargetOptions"
                :disable="dependencyTargetOptions.length === 0"
                no-option-label="No targets available"
              />
              <q-input
                v-model.number="dependencyForm.port"
                label="Port"
                type="number"
                dense
                outlined
                :min="0"
                :step="1"
              />
              <q-select
                v-model="dependencyForm.accountId"
                label="Account"
                dense
                outlined
                emit-value
                map-options
                clearable
                :options="accountOptions"
              />
              <q-checkbox
                v-model="environmentLocked"
                label="Lock target to instance environment"
                dense
              />
            </div>
          </q-card-section>
          <q-separator />
          <q-card-actions align="right">
            <q-btn flat label="Cancel" @click="closeDependencyDialog" />
            <q-btn
              color="primary"
              type="submit"
              :label="editingDependency ? 'Save' : 'Add'"
              :loading="dependencyDialogLoading"
              :disable="!dependencyForm.targetId"
            />
          </q-card-actions>
        </q-form>
      </q-card>
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
import { computed, reactive, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useQuery, useMutation, useQueryClient } from '@tanstack/vue-query'
import { Notify, Dialog } from 'quasar'
import type { QTableColumn } from 'quasar'
import {
  Account,
  
  ApplicationInstance,
  ApplicationInstanceDependency,
  ApplicationPipeline,
  CreateApplicationDependency,
  CreateApplicationInstance,
  CreateApplicationPipeline,
  TargetKind,
  UpdateApplication,
  UpdateApplicationDependency,
  UpdateApplicationInstance,
  UpdateApplicationPipeline
} from '../api/client'
import { useFuseClient } from '../composables/useFuseClient'
import { useTags } from '../composables/useTags'
import { useEnvironments } from '../composables/useEnvironments'
import { useServers } from '../composables/useServers'
import { useDataStores } from '../composables/useDataStores'
import { useExternalResources } from '../composables/useExternalResources'
import { getErrorMessage } from '../utils/error'
import InstanceDependenciesSection from '../components/applications/InstanceDependenciesSection.vue'
import ApplicationDetailsForm from '../components/applications/ApplicationDetailsForm.vue'
import ApplicationInstanceForm from '../components/applications/ApplicationInstanceForm.vue'
import ApplicationPipelineForm from '../components/applications/ApplicationPipelineForm.vue'

interface SelectOption<T = string> {
  label: string
  value: T
}

interface ApplicationInstanceFormModel {
  environmentId: string | null
  serverId: string | null
  baseUri: string
  healthUri: string
  openApiUri: string
  version: string
  tagIds: string[]
}

// (pipeline form model is handled in child component)

interface DependencyForm {
  targetKind: TargetKind
  targetId: string | null
  port: number | null
  accountId: string | null
}

const route = useRoute()
const router = useRouter()
const client = useFuseClient()
const queryClient = useQueryClient()

const applicationId = computed(() => route.params.id as string)

const { data: applicationsData, error: applicationsErrorRef } = useQuery({
  queryKey: ['applications'],
  queryFn: () => client.applicationAll()
})

const application = computed(() => 
  applicationsData.value?.find((app) => app.id === applicationId.value)
)

const applicationName = computed(() => application.value?.name ?? 'Edit Application')

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

const accountsQuery = useQuery({
  queryKey: ['accounts'],
  queryFn: () => client.accountAll()
})

const tagsStore = useTags()
const environmentsStore = useEnvironments()
const serversStore = useServers()
const dataStoresQuery = useDataStores()
const externalResourcesQuery = useExternalResources()

const tagLookup = tagsStore.lookup
const environmentLookup = environmentsStore.lookup
const serverLookup = serversStore.lookup

var environmentLocked = ref(true);

const dependencyTargetKindOptions: SelectOption<TargetKind>[] = Object.values(TargetKind).map((value) => ({
  label: value,
  value
}))

const accountLookup = computed<Record<string, string>>(() => {
  const map: Record<string, string> = {}
  for (const account of accountsQuery.data.value ?? []) {
    if (account.id) {
      map[account.id] = formatAccountLabel(account)
    }
  }
  return map
})

const accountOptions = computed<SelectOption<string>[]>(() =>
  Object.entries(accountLookup.value).map(([value, label]) => ({ label, value }))
)

const instanceColumns: QTableColumn<ApplicationInstance>[] = [
  { name: 'environment', label: 'Environment', field: 'environmentId', align: 'left' },
  { name: 'server', label: 'Server', field: 'serverId', align: 'left' },
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
const editingInstance = ref<ApplicationInstance | null>(null)
const isDependencyDialogOpen = ref(false)
const editingDependency = ref<ApplicationInstanceDependency | null>(null)
const dependencyForm = reactive<DependencyForm>(getEmptyDependencyForm())
const dependencyTargetOptions = computed<SelectOption<string>[]>(() =>
  getDependencyTargetOptions(dependencyForm.targetKind)
)

watch(application, (app) => {
  if (app && editingInstance.value?.id) {
    const latestInstance = app.instances?.find((instance) => instance.id === editingInstance.value?.id)
    if (latestInstance) {
      editingInstance.value = latestInstance
    }
  }
}, { immediate: true })

// Watch for changes that require re-validating dependency form
watch(
  () => [
    dependencyForm.targetKind,
    applicationsData.value,
    dataStoresQuery.data.value,
    externalResourcesQuery.data.value
  ],
  () => {
    ensureDependencyTarget()
  }
)

watch(accountsQuery.data, () => {
  ensureDependencyAccount()
})

function getEmptyDependencyForm(): DependencyForm {
  return {
    targetKind: TargetKind.DataStore,
    targetId: null,
    port: null,
    accountId: null
  }
}

function resetDependencyForm() {
  Object.assign(dependencyForm, getEmptyDependencyForm())
}

function openInstanceDialog(instance?: ApplicationInstance) {
  if (!application.value?.id) {
    Notify.create({ type: 'warning', message: 'Application not loaded. Please try again.' })
    return
  }
  if (instance) {
    editingInstance.value = instance
  } else {
    editingInstance.value = null
  }
  isInstanceDialogOpen.value = true
}

function closeInstanceDialog() {
  isInstanceDialogOpen.value = false
  editingInstance.value = null
}

const createInstanceMutation = useMutation({
  mutationFn: ({ appId, payload }: { appId: string; payload: CreateApplicationInstance }) =>
    client.instancesPOST(appId, payload),
  onSuccess: () => {
    queryClient.invalidateQueries({ queryKey: ['applications'] })
    Notify.create({ type: 'positive', message: 'Instance created' })
    closeInstanceDialog()
  },
  onError: (error) => {
    Notify.create({ type: 'negative', message: getErrorMessage(error, 'Unable to create instance') })
  }
})

const updateInstanceMutation = useMutation({
  mutationFn: ({ appId, instanceId, payload }: { appId: string; instanceId: string; payload: UpdateApplicationInstance }) =>
    client.instancesPUT(appId, instanceId, payload),
  onSuccess: () => {
    queryClient.invalidateQueries({ queryKey: ['applications'] })
    Notify.create({ type: 'positive', message: 'Instance updated' })
    closeInstanceDialog()
  },
  onError: (error) => {
    Notify.create({ type: 'negative', message: getErrorMessage(error, 'Unable to update instance') })
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

const instanceMutationPending = computed(
  () => createInstanceMutation.isPending.value || updateInstanceMutation.isPending.value
)

const createDependencyMutation = useMutation({
  mutationFn: ({
    appId,
    instanceId,
    payload
  }: {
    appId: string
    instanceId: string
    payload: CreateApplicationDependency
  }) => client.dependenciesPOST(appId, instanceId, payload),
  onSuccess: () => {
    queryClient.invalidateQueries({ queryKey: ['applications'] })
    Notify.create({ type: 'positive', message: 'Dependency added' })
    closeDependencyDialog()
  },
  onError: (error) => {
    Notify.create({ type: 'negative', message: getErrorMessage(error, 'Unable to add dependency') })
  }
})

const updateDependencyMutation = useMutation({
  mutationFn: ({
    appId,
    instanceId,
    dependencyId,
    payload
  }: {
    appId: string
    instanceId: string
    dependencyId: string
    payload: UpdateApplicationDependency
  }) => client.dependenciesPUT(appId, instanceId, dependencyId, payload),
  onSuccess: () => {
    queryClient.invalidateQueries({ queryKey: ['applications'] })
    Notify.create({ type: 'positive', message: 'Dependency updated' })
    closeDependencyDialog()
  },
  onError: (error) => {
    Notify.create({ type: 'negative', message: getErrorMessage(error, 'Unable to update dependency') })
  }
})

const deleteDependencyMutation = useMutation({
  mutationFn: ({
    appId,
    instanceId,
    dependencyId
  }: {
    appId: string
    instanceId: string
    dependencyId: string
  }) => client.dependenciesDELETE(appId, instanceId, dependencyId),
  onSuccess: () => {
    queryClient.invalidateQueries({ queryKey: ['applications'] })
    Notify.create({ type: 'positive', message: 'Dependency removed' })
  },
  onError: (error) => {
    Notify.create({ type: 'negative', message: getErrorMessage(error, 'Unable to delete dependency') })
  }
})

const dependencyMutationPending = computed(
  () => createDependencyMutation.isPending.value || updateDependencyMutation.isPending.value
)

const dependencyDialogLoading = computed(() => dependencyMutationPending.value)

const dependencyActionsDisabled = computed(
  () => dependencyMutationPending.value || !editingInstance.value?.id
)

function handleSubmitInstance(model: ApplicationInstanceFormModel) {
  if (!application.value?.id) return
  const payload = Object.assign(new UpdateApplicationInstance(), {
    environmentId: model.environmentId ?? undefined,
    serverId: model.serverId ?? undefined,
    baseUri: model.baseUri || undefined,
    healthUri: model.healthUri || undefined,
    openApiUri: model.openApiUri || undefined,
    version: model.version || undefined,
    tagIds: model.tagIds.length ? [...model.tagIds] : undefined
  })
  if (editingInstance.value?.id) {
    updateInstanceMutation.mutate({ appId: application.value.id!, instanceId: editingInstance.value.id!, payload })
  } else {
    const createPayload = Object.assign(new CreateApplicationInstance(), payload)
    createInstanceMutation.mutate({ appId: application.value.id!, payload: createPayload })
  }
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

function openDependencyDialog(dependency?: ApplicationInstanceDependency) {
  if (!application.value?.id || !editingInstance.value?.id) {
    Notify.create({ type: 'warning', message: 'Instance not loaded. Please try again.' })
    return
  }
  if (dependency) {
    editingDependency.value = dependency
    Object.assign(dependencyForm, {
      targetKind: dependency.targetKind ?? TargetKind.DataStore,
      targetId: dependency.targetId ?? null,
      port: dependency.port ?? null,
      accountId: dependency.accountId ?? null
    })
  } else {
    editingDependency.value = null
    resetDependencyForm()
  }
  ensureDependencyTarget()
  ensureDependencyAccount()
  isDependencyDialogOpen.value = true
}

function closeDependencyDialog() {
  isDependencyDialogOpen.value = false
  editingDependency.value = null
  resetDependencyForm()
}

function submitDependency() {
  if (!application.value?.id || !editingInstance.value?.id) {
    return
  }
  if (!dependencyForm.targetId) {
    Notify.create({ type: 'negative', message: 'Select a dependency target' })
    return
  }

  const base = {
    applicationId: application.value.id!,
    instanceId: editingInstance.value.id!,
    targetKind: dependencyForm.targetKind,
    targetId: dependencyForm.targetId,
    port: dependencyForm.port ?? undefined,
    accountId: dependencyForm.accountId ?? undefined
  }

  if (editingDependency.value?.id) {
    const payload = Object.assign(new UpdateApplicationDependency(), {
      ...base,
      dependencyId: editingDependency.value.id!
    })
    updateDependencyMutation.mutate({
      appId: base.applicationId,
      instanceId: base.instanceId,
      dependencyId: editingDependency.value.id!,
      payload
    })
  } else {
    const payload = Object.assign(new CreateApplicationDependency(), base)
    createDependencyMutation.mutate({ appId: base.applicationId, instanceId: base.instanceId, payload })
  }
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

function confirmDependencyDelete(dependency: ApplicationInstanceDependency) {
  if (!application.value?.id || !editingInstance.value?.id || !dependency.id) return
  Dialog.create({
    title: 'Remove dependency',
    message: 'Are you sure you want to remove this dependency?',
    cancel: true,
    persistent: true
  }).onOk(() =>
    deleteDependencyMutation.mutate({
      appId: application.value!.id!,
      instanceId: editingInstance.value!.id!,
      dependencyId: dependency.id!
    })
  )
}

function ensureDependencyTarget() {
  const options = dependencyTargetOptions.value
  if (!dependencyForm.targetId || !options.some((option) => option.value === dependencyForm.targetId)) {
    dependencyForm.targetId = options[0]?.value ?? null
  }
}

function ensureDependencyAccount() {
  if (!dependencyForm.accountId) {
    return
  }
  if (!accountLookup.value[dependencyForm.accountId]) {
    dependencyForm.accountId = null
  }
}

function getDependencyTargetOptions(kind: TargetKind): SelectOption<string>[] {
  switch (kind) {
    case TargetKind.Application: {
      // Use application instances as targets, label with App Name — Environment
      const apps = applicationsData.value ?? []
      const options: SelectOption<string>[] = []
      for (const app of apps) {
        const appName = app.name ?? app.id ?? 'Application'
        for (const inst of app.instances ?? []) {
          if (!inst?.id) continue
          // Filter by environment if locked
          if (environmentLocked.value && inst.environmentId !== editingInstance.value?.environmentId) {
            continue
          }
          const envName = environmentLookup.value[inst.environmentId ?? ''] ?? '—'
          options.push({ label: `${appName} — ${envName}` , value: inst.id })
        }
      }
      return options
    }
    case TargetKind.DataStore:
      return (dataStoresQuery.data.value ?? [])
        .filter((store) => {
          if (!store.id) return false
          // Filter by environment if locked
          if (environmentLocked.value && store.environmentId !== editingInstance.value?.environmentId) {
            return false
          }
          return true
        })
        .map((store) => {
          const storeName = store.name ?? store.id!
          // Include environment in label if not locked
          if (!environmentLocked.value) {
            const envName = environmentLookup.value[store.environmentId ?? ''] ?? '—'
            return { label: `${storeName} — ${envName}`, value: store.id! }
          }
          return { label: storeName, value: store.id! }
        })
    case TargetKind.External:
      return (externalResourcesQuery.data.value ?? [])
        .filter((resource) => !!resource.id)
        .map((resource) => ({ label: resource.name ?? resource.id!, value: resource.id! }))
    default:
      return []
  }
}

function targetLabel(kind: TargetKind | undefined, id: string | null) {
  if (!id) return '—'
  switch (kind) {
    case TargetKind.Application: {
      // Resolve by instance ID first; fallback to legacy application ID
      const apps = applicationsData.value ?? []
      for (const app of apps) {
        const match = (app.instances ?? []).find((i) => i.id === id)
        if (match) {
          const envName = environmentLookup.value[match.environmentId ?? ''] ?? '—'
          const appName = app.name ?? app.id ?? id
          return `${appName} — ${envName}`
        }
      }
      // Fallback: treat id as application id
      return apps.find((a) => a.id === id)?.name ?? id
    }
    case TargetKind.DataStore:
      return dataStoresQuery.data.value?.find((store) => store.id === id)?.name ?? id
    case TargetKind.External:
      return externalResourcesQuery.data.value?.find((resource) => resource.id === id)?.name ?? id
    default:
      return id
  }
}

function formatAccountLabel(account: Account) {
  const identity = account.userName || account.id || 'Account'
  const targetName = targetLabel(account.targetKind, account.targetId ?? null)
  return `${identity} → ${targetName}`
}

function resolveDependencyTargetName(dependency: ApplicationInstanceDependency) {
  return targetLabel(dependency.targetKind, dependency.targetId ?? null)
}

function resolveDependencyAccountName(accountId?: string | null) {
  if (!accountId) return '—'
  return accountLookup.value[accountId] ?? accountId
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
</style>
