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
              v-model="form.platformId"
              label="Platform"
              dense
              outlined
              emit-value
              map-options
              clearable
              :options="platformOptions"
              :disable="platformOptions.length === 0"
            />
            <q-input v-model="form.version" label="Version" dense outlined />
            <q-input v-model="form.baseUri" label="Base URI" dense outlined />
            <q-input v-model="form.healthUri" label="Health URI" dense outlined />
            <q-input v-model="form.openApiUri" label="OpenAPI URI" dense outlined />
            <TagSelect v-model="form.tagIds" label="Tags" />
          </div>
        </q-card-section>
        <q-separator />
        <q-card-actions align="right">
          <q-btn flat label="Cancel" @click="navigateBack" />
          <q-btn
            flat
            label="Delete"
            color="negative"
            :disable="!fuseStore.canModify"
            @click="confirmInstanceDelete"
          />
          <q-btn
            color="primary"
            type="submit"
            label="Save"
            :disable="!fuseStore.canModify"
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
          :disable="dependencyActionsDisabled || !fuseStore.canModify"
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
        <template #body-cell-authKind="props">
          <q-td :props="props">
            <q-badge :label="props.row.authKind ?? 'None'" outline />
          </q-td>
        </template>
        <template #body-cell-credential="props">
          <q-td :props="props">
            {{ resolveDependencyCredentialName(props.row) }}
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
              :disable="dependencyActionsDisabled || !fuseStore.canModify"
              @click="openDependencyDialog(props.row)"
            />
            <q-btn
              dense
              flat
              round
              icon="delete"
              color="negative"
              class="q-ml-xs"
              :disable="dependencyActionsDisabled || !fuseStore.canModify"
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
                v-model="dependencyForm.authKind"
                label="Auth Kind"
                dense
                outlined
                emit-value
                map-options
                :options="authKindOptions"
              />
              <q-select
                v-if="dependencyForm.authKind === 'Account'"
                v-model="dependencyForm.accountId"
                label="Account"
                dense
                outlined
                emit-value
                map-options
                clearable
                :options="accountOptions"
                :hint="accountOptions.length === 0 ? 'No accounts available for this target' : undefined"
              />
              <q-select
                v-if="dependencyForm.authKind === 'Identity'"
                v-model="dependencyForm.identityId"
                label="Identity"
                dense
                outlined
                emit-value
                map-options
                clearable
                :options="identityOptions"
                :hint="availableIdentities.length === 0 ? 'No identities available for this instance' : undefined"
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
  DependencyAuthKind,
  TargetKind,
  UpdateApplicationDependency,
  UpdateApplicationInstance
} from '../api/client'
import { useFuseClient } from '../composables/useFuseClient'
import { useFuseStore } from '../stores/FuseStore'
import TagSelect from '../components/tags/TagSelect.vue'
import { useEnvironments } from '../composables/useEnvironments'
import { usePlatforms } from '../composables/usePlatforms'
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
  authKind: DependencyAuthKind
  accountId: string | null
  identityId: string | null
}

const route = useRoute()
const router = useRouter()
const client = useFuseClient()
const queryClient = useQueryClient()
const fuseStore = useFuseStore()

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

const identitiesQuery = useQuery({
  queryKey: ['identities'],
  queryFn: () => client.identityAll()
})

const environmentsStore = useEnvironments()
const platformsStore = usePlatforms()
const dataStoresQuery = useDataStores()
const externalResourcesQuery = useExternalResources()

const environmentLookup = environmentsStore.lookup

const environmentOptions = environmentsStore.options
const platformOptions = platformsStore.options

const form = reactive({
  environmentId: null as string | null,
  platformId: null as string | null,
  baseUri: '',
  healthUri: '',
  openApiUri: '',
  version: '',
  tagIds: [] as string[]
})

watch(instance, (inst) => {
  if (inst) {
    form.environmentId = inst.environmentId ?? null
    form.platformId = inst.platformId ?? null
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
    platformId: form.platformId ?? undefined,
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

// Filter accounts to only show those with matching target (same targetKind and targetId as the dependency)
const filteredAccounts = computed(() => {
  const targetKind = dependencyForm.targetKind
  const targetId = dependencyForm.targetId
  return (accountsQuery.data.value ?? []).filter((account) => {
    if (!account.id) return false
    // Account must target the same resource as the dependency
    return account.targetKind === targetKind && account.targetId === targetId
  })
})

const accountOptions = computed<SelectOption<string>[]>(() =>
  filteredAccounts.value.map((account) => ({
    label: formatAccountLabel(account),
    value: account.id!
  }))
)

const identityLookup = computed<Record<string, string>>(() => {
  const map: Record<string, string> = {}
  for (const identity of identitiesQuery.data.value ?? []) {
    if (identity.id) {
      map[identity.id] = identity.name ?? identity.id
    }
  }
  return map
})

// Filter identities that are owned by this instance or have no owner
const availableIdentities = computed(() => {
  const currentInstanceId = instanceId.value
  return (identitiesQuery.data.value ?? []).filter((identity) => {
    // Include if no owner (shared identity)
    if (!identity.ownerInstanceId) return true
    // Include if owned by this instance
    if (identity.ownerInstanceId === currentInstanceId) return true
    return false
  })
})

const identityOptions = computed<SelectOption<string>[]>(() =>
  availableIdentities.value
    .filter((identity) => !!identity.id)
    .map((identity) => ({
      label: identity.name ?? identity.id!,
      value: identity.id!
    }))
)

const authKindOptions: SelectOption<DependencyAuthKind>[] = [
  { label: 'None', value: DependencyAuthKind.None },
  { label: 'Account', value: DependencyAuthKind.Account },
  { label: 'Identity', value: DependencyAuthKind.Identity }
]

const dependencyTargetOptions = computed<SelectOption<string>[]>(() =>
  getDependencyTargetOptions(dependencyForm.targetKind)
)

const dependencyColumns: QTableColumn<ApplicationInstanceDependency>[] = [
  { name: 'target', label: 'Target', field: 'targetId', align: 'left' },
  { name: 'targetKind', label: 'Kind', field: 'targetKind', align: 'left' },
  { name: 'port', label: 'Port', field: 'port', align: 'left' },
  { name: 'authKind', label: 'Auth', field: 'authKind', align: 'left' },
  { name: 'credential', label: 'Credential', field: (row) => row.accountId || row.identityId, align: 'left' },
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

// Watch for targetKind or targetId changes and clear account if it's no longer valid
watch(
  () => [dependencyForm.targetKind, dependencyForm.targetId],
  () => {
    // If auth kind is Account, clear the account when target changes
    if (dependencyForm.authKind === DependencyAuthKind.Account) {
      // Check if current account is still valid for the new target
      const currentAccountId = dependencyForm.accountId
      if (currentAccountId) {
        const isAccountStillValid = filteredAccounts.value.some(
          (account) => account.id === currentAccountId
        )
        if (!isAccountStillValid) {
          dependencyForm.accountId = null
        }
      }
    }
  }
)

watch(accountsQuery.data, () => {
  ensureDependencyAccount()
})

watch(identitiesQuery.data, () => {
  ensureDependencyIdentity()
})

watch(
  () => dependencyForm.authKind,
  (newKind) => {
    // Clear account/identity when switching auth kind
    if (newKind === DependencyAuthKind.None) {
      dependencyForm.accountId = null
      dependencyForm.identityId = null
    } else if (newKind === DependencyAuthKind.Account) {
      dependencyForm.identityId = null
      ensureDependencyAccount()
    } else if (newKind === DependencyAuthKind.Identity) {
      dependencyForm.accountId = null
      ensureDependencyIdentity()
    }
  }
)

function getEmptyDependencyForm(): DependencyForm {
  return {
    targetKind: TargetKind.DataStore,
    targetId: null,
    port: null,
    authKind: DependencyAuthKind.None,
    accountId: null,
    identityId: null
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
      authKind: dependency.authKind ?? DependencyAuthKind.None,
      accountId: dependency.accountId ?? null,
      identityId: dependency.identityId ?? null
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
    authKind: dependencyForm.authKind,
    accountId: dependencyForm.accountId ?? undefined,
    identityId: dependencyForm.identityId ?? undefined
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
  // Check if current account is still valid for the current target
  const isValid = filteredAccounts.value.some(
    (account) => account.id === dependencyForm.accountId
  )
  if (!isValid) {
    dependencyForm.accountId = null
  }
}

function ensureDependencyIdentity() {
  if (!dependencyForm.identityId) {
    return
  }
  if (!identityLookup.value[dependencyForm.identityId]) {
    dependencyForm.identityId = null
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

function resolveDependencyCredentialName(dependency: ApplicationInstanceDependency) {
  if (dependency.authKind === DependencyAuthKind.Account && dependency.accountId) {
    return accountLookup.value[dependency.accountId] ?? dependency.accountId
  }
  if (dependency.authKind === DependencyAuthKind.Identity && dependency.identityId) {
    return identityLookup.value[dependency.identityId] ?? dependency.identityId
  }
  return '—'
}
</script>

<style scoped>
@import '../styles/pages.css';

.form-dialog {
  min-width: 500px;
  max-width: 700px;
}
</style>
