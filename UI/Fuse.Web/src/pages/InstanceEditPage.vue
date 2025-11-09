<template>
  <div class="page-container">
    <div class="page-header">
      <div>
        <q-btn flat round dense icon="arrow_back" @click="navigateBack" class="q-mr-md" />
        <div style="display: inline-block">
          <h1>{{ pageTitle }}</h1>
          <p class="subtitle">Manage instance details and dependencies.</p>
        </div>
      </div>
    </div>

    <q-banner v-if="errorMessage" dense class="bg-red-1 text-negative q-mb-md">
      {{ errorMessage }}
    </q-banner>

    <q-card class="content-card q-mb-md">
      <q-card-section class="dialog-header">
        <div class="text-h6">Instance Details</div>
      </q-card-section>
      <q-separator />
      <q-form @submit.prevent="handleSubmitInstance">
        <q-card-section>
          <div class="form-grid">
            <q-select
              v-model="form.environmentId"
              label="Environment*"
              dense
              outlined
              emit-value
              map-options
              :options="environmentOptions"
              :rules="[v => !!v || 'Environment is required']"
              :disable="environmentOptions.length === 0"
            />
            <q-select
              v-model="form.serverId"
              label="Server"
              dense
              outlined
              emit-value
              map-options
              clearable
              :options="serverOptions"
              :disable="serverOptions.length === 0"
            />
            <q-input v-model="form.version" label="Version" dense outlined />
            <q-input v-model="form.baseUri" label="Base URI" dense outlined />
            <q-input v-model="form.healthUri" label="Health URI" dense outlined />
            <q-input v-model="form.openApiUri" label="OpenAPI URI" dense outlined />
            <q-select
              v-model="form.tagIds"
              label="Tags"
              dense
              outlined
              use-chips
              multiple
              emit-value
              map-options
              :options="tagOptions"
            />
          </div>
        </q-card-section>
        <q-separator />
        <q-card-actions align="right">
          <q-btn flat label="Cancel" @click="navigateBack" />
          <q-btn
            flat
            label="Delete"
            color="negative"
            @click="confirmInstanceDelete"
          />
          <q-btn
            color="primary"
            type="submit"
            label="Save"
            :loading="updateInstanceMutation.isPending.value"
          />
        </q-card-actions>
      </q-form>
    </q-card>

    <q-card class="content-card">
      <q-card-section class="dialog-header">
        <div>
          <div class="text-h6">Dependencies</div>
          <div class="text-caption text-grey-7">
            Describe downstream systems this instance relies on.
          </div>
        </div>
        <q-btn
          color="primary"
          label="Add Dependency"
          dense
          icon="add"
          :disable="dependencyActionsDisabled"
          @click="openDependencyDialog()"
        />
      </q-card-section>
      <q-separator />
      <q-table
        flat
        bordered
        dense
        :rows="instance?.dependencies ?? []"
        :columns="dependencyColumns"
        row-key="id"
      >
        <template #body-cell-target="props">
          <q-td :props="props">
            {{ resolveDependencyTargetName(props.row) }}
          </q-td>
        </template>
        <template #body-cell-account="props">
          <q-td :props="props">
            {{ resolveDependencyAccountName(props.row.accountId) }}
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
              :disable="dependencyActionsDisabled"
              @click="openDependencyDialog(props.row)"
            />
            <q-btn
              dense
              flat
              round
              icon="delete"
              color="negative"
              class="q-ml-xs"
              :disable="dependencyActionsDisabled"
              @click="confirmDependencyDelete(props.row)"
            />
          </q-td>
        </template>
        <template #no-data>
          <div class="q-pa-sm text-grey-7">No dependencies documented.</div>
        </template>
      </q-table>
    </q-card>

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
  ApplicationInstanceDependency,
  CreateApplicationDependency,
  TargetKind,
  UpdateApplicationDependency,
  UpdateApplicationInstance
} from '../api/client'
import { useFuseClient } from '../composables/useFuseClient'
import { useTags } from '../composables/useTags'
import { useEnvironments } from '../composables/useEnvironments'
import { useServers } from '../composables/useServers'
import { useDataStores } from '../composables/useDataStores'
import { useExternalResources } from '../composables/useExternalResources'
import { getErrorMessage } from '../utils/error'

interface SelectOption<T = string> {
  label: string
  value: T
}

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

const applicationId = computed(() => route.params.applicationId as string)
const instanceId = computed(() => route.params.instanceId as string)

const { data: applicationsData, error: applicationsErrorRef } = useQuery({
  queryKey: ['applications'],
  queryFn: () => client.applicationAll()
})

const application = computed(() => 
  applicationsData.value?.find((app) => app.id === applicationId.value)
)

const instance = computed(() => 
  application.value?.instances?.find((inst) => inst.id === instanceId.value)
)

const pageTitle = computed(() => {
  const appName = application.value?.name ?? 'Application'
  const envName = environmentLookup.value[instance.value?.environmentId ?? ''] ?? 'Instance'
  return `${appName} — ${envName}`
})

const errorMessage = computed(() => {
  if (applicationsErrorRef.value) {
    return getErrorMessage(applicationsErrorRef.value)
  }
  if (applicationsData.value && !application.value) {
    return 'Application not found'
  }
  if (applicationsData.value && !instance.value) {
    return 'Instance not found'
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

const environmentLookup = environmentsStore.lookup

const environmentOptions = environmentsStore.options
const serverOptions = serversStore.options
const tagOptions = tagsStore.options

const form = reactive({
  environmentId: null as string | null,
  serverId: null as string | null,
  baseUri: '',
  healthUri: '',
  openApiUri: '',
  version: '',
  tagIds: [] as string[]
})

watch(instance, (inst) => {
  if (inst) {
    form.environmentId = inst.environmentId ?? null
    form.serverId = inst.serverId ?? null
    form.baseUri = inst.baseUri ?? ''
    form.healthUri = inst.healthUri ?? ''
    form.openApiUri = inst.openApiUri ?? ''
    form.version = inst.version ?? ''
    form.tagIds = [...(inst.tagIds ?? [])]
  }
}, { immediate: true })

function navigateBack() {
  router.push({ name: 'applicationEdit', params: { id: applicationId.value } })
}

const updateInstanceMutation = useMutation({
  mutationFn: ({ appId, instanceId, payload }: { appId: string; instanceId: string; payload: UpdateApplicationInstance }) =>
    client.instancesPUT(appId, instanceId, payload),
  onSuccess: () => {
    queryClient.invalidateQueries({ queryKey: ['applications'] })
    Notify.create({ type: 'positive', message: 'Instance updated' })
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
    navigateBack()
  },
  onError: (error) => {
    Notify.create({ type: 'negative', message: getErrorMessage(error, 'Unable to delete instance') })
  }
})

function handleSubmitInstance() {
  if (!applicationId.value || !instanceId.value) return
  const payload = Object.assign(new UpdateApplicationInstance(), {
    environmentId: form.environmentId ?? undefined,
    serverId: form.serverId ?? undefined,
    baseUri: form.baseUri || undefined,
    healthUri: form.healthUri || undefined,
    openApiUri: form.openApiUri || undefined,
    version: form.version || undefined,
    tagIds: form.tagIds.length ? [...form.tagIds] : undefined
  })
  updateInstanceMutation.mutate({ appId: applicationId.value, instanceId: instanceId.value, payload })
}

function confirmInstanceDelete() {
  if (!applicationId.value || !instanceId.value) return
  Dialog.create({
    title: 'Remove instance',
    message: 'Are you sure you want to remove this instance?',
    cancel: true,
    persistent: true
  }).onOk(() => {
    deleteInstanceMutation.mutate({ appId: applicationId.value, instanceId: instanceId.value })
  })
}

// Dependency management
var environmentLocked = ref(true)
const isDependencyDialogOpen = ref(false)
const editingDependency = ref<ApplicationInstanceDependency | null>(null)
const dependencyForm = reactive<DependencyForm>(getEmptyDependencyForm())

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

const dependencyTargetOptions = computed<SelectOption<string>[]>(() =>
  getDependencyTargetOptions(dependencyForm.targetKind)
)

const dependencyColumns: QTableColumn<ApplicationInstanceDependency>[] = [
  { name: 'target', label: 'Target', field: 'targetId', align: 'left' },
  { name: 'targetKind', label: 'Kind', field: 'targetKind', align: 'left' },
  { name: 'port', label: 'Port', field: 'port', align: 'left' },
  { name: 'account', label: 'Account', field: 'accountId', align: 'left' },
  { name: 'actions', label: '', field: (row) => row.id, align: 'right' }
]

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

function openDependencyDialog(dependency?: ApplicationInstanceDependency) {
  if (!applicationId.value || !instanceId.value) {
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
  () => dependencyMutationPending.value || !instanceId.value
)

function submitDependency() {
  if (!applicationId.value || !instanceId.value) {
    return
  }
  if (!dependencyForm.targetId) {
    Notify.create({ type: 'negative', message: 'Select a dependency target' })
    return
  }

  const base = {
    applicationId: applicationId.value,
    instanceId: instanceId.value,
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

function confirmDependencyDelete(dependency: ApplicationInstanceDependency) {
  if (!applicationId.value || !instanceId.value || !dependency.id) return
  Dialog.create({
    title: 'Remove dependency',
    message: 'Are you sure you want to remove this dependency?',
    cancel: true,
    persistent: true
  }).onOk(() =>
    deleteDependencyMutation.mutate({
      appId: applicationId.value,
      instanceId: instanceId.value,
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
      const apps = applicationsData.value ?? []
      const options: SelectOption<string>[] = []
      for (const app of apps) {
        const appName = app.name ?? app.id ?? 'Application'
        for (const inst of app.instances ?? []) {
          if (!inst?.id) continue
          if (environmentLocked.value && inst.environmentId !== instance.value?.environmentId) {
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
          if (environmentLocked.value && store.environmentId !== instance.value?.environmentId) {
            return false
          }
          return true
        })
        .map((store) => {
          const storeName = store.name ?? store.id!
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
      const apps = applicationsData.value ?? []
      for (const app of apps) {
        const match = (app.instances ?? []).find((i) => i.id === id)
        if (match) {
          const envName = environmentLookup.value[match.environmentId ?? ''] ?? '—'
          const appName = app.name ?? app.id ?? id
          return `${appName} — ${envName}`
        }
      }
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
</script>

<style scoped>
@import '../styles/pages.css';

.form-dialog {
  min-width: 500px;
  max-width: 700px;
}
</style>
