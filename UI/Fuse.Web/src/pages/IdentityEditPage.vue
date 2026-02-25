<template>
  <div class="page-container">
    <div class="page-header">
      <div>
        <h1>{{ pageTitle }}</h1>
        <p class="subtitle">{{ pageSubtitle }}</p>
      </div>
      <div class="flex q-gutter-sm">
        <q-btn flat label="Cancel" @click="handleCancel" />
        <q-btn 
          color="primary" 
          :label="isEditMode ? 'Save Changes' : 'Create Identity'" 
          :loading="isSaving"
          @click="handleSave"
        />
      </div>
    </div>

    <q-banner v-if="loadError" dense class="bg-red-1 text-negative q-mb-md">
      {{ loadError }}
    </q-banner>

    <q-banner v-if="!fuseStore.canModify" dense class="bg-orange-1 text-orange-9 q-mb-md">
      You do not have permission to {{ isEditMode ? 'edit' : 'create' }} identities.
    </q-banner>

    <q-card v-if="fuseStore.canModify && !loadError" class="content-card">
      <q-card-section>
        <div class="text-h6 q-mb-md">Identity Details</div>
        <div v-if="isLoading" class="row items-center justify-center q-pa-lg">
          <q-spinner color="primary" size="3em" />
        </div>
        <IdentityForm
          v-else
          v-model="form"
          :identity-kind-options="identityKindOptions"
          :instance-options="instanceOptions"
        />
      </q-card-section>

      <q-separator />

      <q-card-section>
        <div class="text-h6 q-mb-md">Assignments</div>
        <IdentityAssignmentsSection
          :assignments="isEditMode ? (identity?.assignments ?? []) : form.assignments"
          :disable-actions="assignmentMutationPending"
          :owner-instance-dependencies="ownerInstanceDependencies"
          :current-identity-id="identityId"
          :has-owner-instance="!!identity?.ownerInstanceId"
          :disable-dependency-actions="dependencyMutationPending"
          @add="openAssignmentDialog()"
          @edit="({ assignment }) => openAssignmentDialog(assignment)"
          @delete="({ assignment }) => confirmAssignmentDelete(assignment)"
          @apply-dependency="({ assignment }) => applyDependency(assignment)"
          @replace-dependency="({ assignment, existingDeps }) => confirmReplaceDependency(assignment, existingDeps)"
        />
      </q-card-section>
    </q-card>

    <!-- Assignment Dialog -->
    <q-dialog v-model="isAssignmentDialogOpen" persistent>
      <q-card class="form-dialog">
        <q-card-section class="dialog-header">
          <div class="text-h6">{{ assignmentDialogTitle }}</div>
          <q-btn flat round dense icon="close" @click="closeAssignmentDialog" />
        </q-card-section>
        <q-separator />
        <q-form @submit.prevent="submitAssignment">
          <q-card-section>
            <div class="form-grid">
              <q-select
                v-model="assignmentForm.targetKind"
                label="Target Kind"
                dense
                outlined
                emit-value
                map-options
                :options="targetKindOptions"
              />
              <q-select
                v-model="assignmentForm.targetId"
                label="Target"
                dense
                outlined
                emit-value
                map-options
                :options="targetOptions"
              />
              <q-input v-model="assignmentForm.role" label="Role" dense outlined />
              <q-input 
                v-model="assignmentForm.notes" 
                label="Notes" 
                dense 
                outlined 
                type="textarea"
                rows="2"
              />
            </div>
          </q-card-section>
          <q-separator />
          <q-card-actions align="right">
            <q-btn flat label="Cancel" @click="closeAssignmentDialog" />
            <q-btn
              color="primary"
              type="submit"
              :label="editingAssignment ? 'Save' : 'Create'"
              :loading="assignmentDialogLoading"
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
import { useMutation, useQuery, useQueryClient } from '@tanstack/vue-query'
import { Notify, Dialog } from 'quasar'
import {
  IdentityKind,
  IdentityAssignment,
  CreateIdentity,
  UpdateIdentity,
  CreateIdentityAssignment,
  UpdateIdentityAssignment,
  TargetKind,
  ApplicationInstanceDependency,
  CreateApplicationDependency,
  UpdateApplicationDependency,
  DependencyAuthKind
} from '../api/client'
import IdentityForm from '../components/identities/IdentityForm.vue'
import IdentityAssignmentsSection from '../components/identities/IdentityAssignmentsSection.vue'
import type {
  IdentityFormModel,
  SelectOption,
  TargetOption,
  AssignmentForm
} from '../components/identities/types'
import { useFuseClient } from '../composables/useFuseClient'
import { useFuseStore } from '../stores/FuseStore'
import { useApplications } from '../composables/useApplications'
import { useDataStores } from '../composables/useDataStores'
import { useExternalResources } from '../composables/useExternalResources'
import { useEnvironments } from '../composables/useEnvironments'
import { getErrorMessage } from '../utils/error'

const route = useRoute()
const router = useRouter()
const client = useFuseClient()
const queryClient = useQueryClient()
const fuseStore = useFuseStore()
const applicationsQuery = useApplications()
const dataStoresQuery = useDataStores()
const externalResourcesQuery = useExternalResources()
const environmentsQuery = useEnvironments()

const identityId = computed(() => route.params.id as string | undefined)
const isEditMode = computed(() => !!identityId.value)

const pageTitle = computed(() => (isEditMode.value ? 'Edit Identity' : 'Create Identity'))
const pageSubtitle = computed(() =>
  isEditMode.value
    ? 'Update identity details and assignments'
    : 'Create a new app-owned identity for authentication'
)

const { data: identity, isLoading, error: loadError } = useQuery({
  queryKey: computed(() => ['identity', identityId.value]),
  queryFn: () => client.identityGET(identityId.value!),
  enabled: computed(() => !!identityId.value),
  retry: false
})

const emptyIdentityForm = (): IdentityFormModel => ({
  name: '',
  kind: IdentityKind.AzureManagedIdentity,
  notes: '',
  ownerInstanceId: null,
  assignments: [],
  tagIds: []
})

const form = ref<IdentityFormModel>(emptyIdentityForm())

const identityKindOptions: SelectOption<IdentityKind>[] = Object.values(IdentityKind).map((value) => ({
  label: formatIdentityKindLabel(value),
  value
}))

const targetKindOptions: SelectOption<TargetKind>[] = Object.values(TargetKind).map((value) => ({
  label: formatTargetKindLabel(value),
  value
}))

const instanceOptions = computed<TargetOption[]>(() => {
  const apps = applicationsQuery.data.value ?? []
  const envLookup = environmentsQuery.lookup.value
  const options: TargetOption[] = []
  
  for (const app of apps) {
    const appName = app.name ?? app.id ?? 'Application'
    for (const inst of app.instances ?? []) {
      if (!inst?.id) continue
      const envName = inst.environmentId ? (envLookup[inst.environmentId] ?? inst.environmentId) : '—'
      options.push({ label: `${appName} — ${envName}`, value: inst.id })
    }
  }
  
  return options
})

const targetOptions = computed<TargetOption[]>(() => {
  const kind = assignmentForm.targetKind ?? TargetKind.Application
  
  if (kind === TargetKind.Application) {
    const apps = applicationsQuery.data.value ?? []
    const envLookup = environmentsQuery.lookup.value
    const options: TargetOption[] = []
    
    for (const app of apps) {
      const appName = app.name ?? app.id ?? 'Application'
      for (const inst of app.instances ?? []) {
        if (!inst?.id) continue
        const envName = inst.environmentId ? (envLookup[inst.environmentId] ?? inst.environmentId) : '—'
        options.push({ label: `${appName} — ${envName}`, value: inst.id, targetKind: TargetKind.Application })
      }
    }
    return options
  }
  
  if (kind === TargetKind.DataStore) {
    return (dataStoresQuery.data.value ?? [])
      .filter((item) => !!item.id)
      .map((item) => ({ label: item.name ?? item.id!, value: item.id!, targetKind: TargetKind.DataStore }))
  }
  
  return (externalResourcesQuery.data.value ?? [])
    .filter((item) => !!item.id)
    .map((item) => ({ label: item.name ?? item.id!, value: item.id!, targetKind: TargetKind.External }))
})

// Assignment management
const isAssignmentDialogOpen = ref(false)
const editingAssignment = ref<IdentityAssignment | null>(null)
const assignmentForm = reactive<AssignmentForm>({
  targetKind: TargetKind.Application,
  targetId: null,
  role: '',
  notes: ''
})

const assignmentDialogTitle = computed(() => (editingAssignment.value ? 'Edit Assignment' : 'Add Assignment'))

// Initialize form from identity data
watch(
  identity,
  (ident) => {
    if (ident) {
      Object.assign(form.value, {
        name: ident.name ?? '',
        kind: ident.kind ?? IdentityKind.AzureManagedIdentity,
        notes: ident.notes ?? '',
        ownerInstanceId: ident.ownerInstanceId ?? null,
        assignments: cloneAssignments(ident.assignments),
        tagIds: [...(ident.tagIds ?? [])]
      })
    }
  },
  { immediate: true }
)

// Ensure targetId is valid when targetKind changes
watch(
  () => assignmentForm.targetKind,
  () => {
    const options = targetOptions.value
    if (!assignmentForm.targetId || !options.some((option) => option.value === assignmentForm.targetId)) {
      assignmentForm.targetId = options[0]?.value ?? null
    }
  }
)

function cloneAssignments(assignments?: IdentityAssignment[]): IdentityAssignment[] {
  if (!assignments) return []
  return assignments.map((a) =>
    Object.assign(new IdentityAssignment(), {
      id: a.id,
      targetKind: a.targetKind,
      targetId: a.targetId,
      role: a.role,
      notes: a.notes
    })
  )
}

// Mutations
const createMutation = useMutation({
  mutationFn: (payload: CreateIdentity) => client.identityPOST(payload),
  onSuccess: () => {
    queryClient.invalidateQueries({ queryKey: ['identities'] })
    Notify.create({ type: 'positive', message: 'Identity created' })
    router.push('/identities')
  },
  onError: (err) => {
    Notify.create({ type: 'negative', message: getErrorMessage(err, 'Unable to create identity') })
  }
})

const updateMutation = useMutation({
  mutationFn: ({ id, payload }: { id: string; payload: UpdateIdentity }) => client.identityPUT(id, payload),
  onSuccess: () => {
    queryClient.invalidateQueries({ queryKey: ['identities'] })
    queryClient.invalidateQueries({ queryKey: ['identity', identityId.value] })
    Notify.create({ type: 'positive', message: 'Identity updated' })
    router.push('/identities')
  },
  onError: (err) => {
    Notify.create({ type: 'negative', message: getErrorMessage(err, 'Unable to update identity') })
  }
})

const createAssignmentMutation = useMutation({
  mutationFn: ({ identityId, payload }: { identityId: string; payload: CreateIdentityAssignment }) =>
    client.assignmentPOST(identityId, payload),
  onSuccess: () => {
    queryClient.invalidateQueries({ queryKey: ['identities'] })
    queryClient.invalidateQueries({ queryKey: ['identity', identityId.value] })
    Notify.create({ type: 'positive', message: 'Assignment created' })
    closeAssignmentDialog()
  },
  onError: (err) => {
    Notify.create({ type: 'negative', message: getErrorMessage(err, 'Unable to create assignment') })
  }
})

const updateAssignmentMutation = useMutation({
  mutationFn: ({
    identityId,
    assignmentId,
    payload
  }: {
    identityId: string
    assignmentId: string
    payload: UpdateIdentityAssignment
  }) => client.assignmentPUT(identityId, assignmentId, payload),
  onSuccess: () => {
    queryClient.invalidateQueries({ queryKey: ['identities'] })
    queryClient.invalidateQueries({ queryKey: ['identity', identityId.value] })
    Notify.create({ type: 'positive', message: 'Assignment updated' })
    closeAssignmentDialog()
  },
  onError: (err) => {
    Notify.create({ type: 'negative', message: getErrorMessage(err, 'Unable to update assignment') })
  }
})

const deleteAssignmentMutation = useMutation({
  mutationFn: ({ identityId, assignmentId }: { identityId: string; assignmentId: string }) =>
    client.assignmentDELETE(identityId, assignmentId),
  onSuccess: () => {
    queryClient.invalidateQueries({ queryKey: ['identities'] })
    queryClient.invalidateQueries({ queryKey: ['identity', identityId.value] })
    Notify.create({ type: 'positive', message: 'Assignment removed' })
  },
  onError: (err) => {
    Notify.create({ type: 'negative', message: getErrorMessage(err, 'Unable to delete assignment') })
  }
})

const assignmentMutationPending = computed(
  () => createAssignmentMutation.isPending.value || updateAssignmentMutation.isPending.value
)

const assignmentDialogLoading = computed(() => assignmentMutationPending.value)

// Owner instance dependency resolution
const ownerAppId = computed<string | null>(() => {
  const ownerInstId = identity.value?.ownerInstanceId
  if (!ownerInstId) return null
  for (const app of applicationsQuery.data.value ?? []) {
    if (app.instances?.some((inst) => inst.id === ownerInstId)) return app.id ?? null
  }
  return null
})

const ownerInstanceDependencies = computed<readonly ApplicationInstanceDependency[]>(() => {
  const ownerInstId = identity.value?.ownerInstanceId
  if (!ownerInstId) return []
  for (const app of applicationsQuery.data.value ?? []) {
    const inst = app.instances?.find((i) => i.id === ownerInstId)
    if (inst) return inst.dependencies ?? []
  }
  return []
})

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
    Notify.create({ type: 'positive', message: 'Dependency applied' })
  },
  onError: (err) => {
    Notify.create({ type: 'negative', message: getErrorMessage(err, 'Unable to apply dependency') })
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
  },
  onError: (err) => {
    Notify.create({ type: 'negative', message: getErrorMessage(err, 'Unable to update dependency') })
  }
})

const dependencyMutationPending = computed(
  () => createDependencyMutation.isPending.value || updateDependencyMutation.isPending.value
)

function applyDependency(assignment: IdentityAssignment) {
  const appId = ownerAppId.value
  const instanceId = identity.value?.ownerInstanceId
  const currentId = identityId.value
  if (!appId || !instanceId || !currentId || !assignment.targetId) return

  Dialog.create({
    title: 'Apply dependency',
    message: `Add a dependency from the owner instance to "${assignment.targetId}" using this identity as authentication?`,
    cancel: true,
    persistent: true
  }).onOk(() => {
    const payload = Object.assign(new CreateApplicationDependency(), {
      applicationId: appId,
      instanceId,
      targetId: assignment.targetId,
      targetKind: assignment.targetKind,
      authKind: DependencyAuthKind.Identity,
      identityId: currentId
    })
    createDependencyMutation.mutate({ appId, instanceId, payload })
  })
}

function confirmReplaceDependency(
  assignment: IdentityAssignment,
  existingDeps: ApplicationInstanceDependency[]
) {
  const appId = ownerAppId.value
  const instanceId = identity.value?.ownerInstanceId
  const currentId = identityId.value
  if (!appId || !instanceId || !currentId || !assignment.targetId) return

  const depToReplace = existingDeps[0]
  if (!depToReplace?.id) return

  Dialog.create({
    title: 'Replace existing dependency',
    message: `Update the existing dependency to "${assignment.targetId}" to use this identity as authentication? This will replace its current auth setting.`,
    cancel: {
      label: 'Add as another instead',
      flat: true,
      color: 'primary'
    },
    ok: {
      label: 'Replace',
      color: 'warning'
    },
    persistent: true
  })
    .onOk(() => {
      const payload = Object.assign(new UpdateApplicationDependency(), {
        applicationId: appId,
        instanceId,
        dependencyId: depToReplace.id,
        targetId: depToReplace.targetId,
        targetKind: depToReplace.targetKind,
        port: depToReplace.port,
        authKind: DependencyAuthKind.Identity,
        identityId: currentId,
        accountId: undefined
      })
      updateDependencyMutation.mutate({
        appId,
        instanceId,
        dependencyId: depToReplace.id!,
        payload
      })
    })
    .onCancel(() => {
      applyDependency(assignment)
    })
}

const isSaving = computed(() => createMutation.isPending.value || updateMutation.isPending.value)

function handleCancel() {
  router.push('/identities')
}

function handleSave() {
  if (!form.value.name.trim()) {
    Notify.create({ type: 'warning', message: 'Name is required.' })
    return
  }

  if (isEditMode.value && identityId.value) {
    const payload = Object.assign(new UpdateIdentity(), {
      name: form.value.name.trim(),
      kind: form.value.kind,
      notes: form.value.notes || undefined,
      ownerInstanceId: form.value.ownerInstanceId || undefined,
      assignments: cloneAssignments(identity.value?.assignments),
      tagIds: form.value.tagIds.length ? [...form.value.tagIds] : undefined
    })
    updateMutation.mutate({ id: identityId.value, payload })
  } else {
    const payload = Object.assign(new CreateIdentity(), {
      name: form.value.name.trim(),
      kind: form.value.kind,
      notes: form.value.notes || undefined,
      ownerInstanceId: form.value.ownerInstanceId || undefined,
      assignments: cloneAssignments(form.value.assignments),
      tagIds: form.value.tagIds.length ? [...form.value.tagIds] : undefined
    })
    createMutation.mutate(payload)
  }
}

function openAssignmentDialog(assignment?: IdentityAssignment) {
  if (!isEditMode.value && !assignment) {
    Notify.create({ type: 'warning', message: 'Save the identity first before adding assignments' })
    return
  }

  if (!identityId.value && !assignment) {
    Notify.create({ type: 'warning', message: 'Identity ID is required' })
    return
  }

  editingAssignment.value = assignment ?? null
  if (assignment) {
    Object.assign(assignmentForm, {
      targetKind: assignment.targetKind ?? TargetKind.Application,
      targetId: assignment.targetId ?? null,
      role: assignment.role ?? '',
      notes: assignment.notes ?? ''
    })
  } else {
    Object.assign(assignmentForm, {
      targetKind: TargetKind.Application,
      targetId: null,
      role: '',
      notes: ''
    })
  }
  isAssignmentDialogOpen.value = true
}

function closeAssignmentDialog() {
  isAssignmentDialogOpen.value = false
  editingAssignment.value = null
}

function submitAssignment() {
  if (!identityId.value) return

  if (editingAssignment.value?.id) {
    const payload = Object.assign(new UpdateIdentityAssignment(), {
      identityId: identityId.value,
      assignmentId: editingAssignment.value.id,
      targetKind: assignmentForm.targetKind,
      targetId: assignmentForm.targetId ?? undefined,
      role: assignmentForm.role || undefined,
      notes: assignmentForm.notes || undefined
    })
    updateAssignmentMutation.mutate({
      identityId: identityId.value,
      assignmentId: editingAssignment.value.id,
      payload
    })
  } else {
    const payload = Object.assign(new CreateIdentityAssignment(), {
      identityId: identityId.value,
      targetKind: assignmentForm.targetKind,
      targetId: assignmentForm.targetId ?? undefined,
      role: assignmentForm.role || undefined,
      notes: assignmentForm.notes || undefined
    })
    createAssignmentMutation.mutate({ identityId: identityId.value, payload })
  }
}

function confirmAssignmentDelete(assignment: IdentityAssignment) {
  if (!identityId.value || !assignment.id) return
  Dialog.create({
    title: 'Delete assignment',
    message: `Delete assignment for "${assignment.targetId ?? 'this target'}"?`,
    cancel: true,
    persistent: true
  }).onOk(() =>
    deleteAssignmentMutation.mutate({ identityId: identityId.value!, assignmentId: assignment.id! })
  )
}

function formatIdentityKindLabel(kind: IdentityKind): string {
  switch (kind) {
    case IdentityKind.AzureManagedIdentity:
      return 'Azure Managed Identity'
    case IdentityKind.KubernetesServiceAccount:
      return 'Kubernetes Service Account'
    case IdentityKind.AwsIamRole:
      return 'AWS IAM Role'
    case IdentityKind.Custom:
      return 'Custom'
    default:
      return String(kind)
  }
}

function formatTargetKindLabel(kind: TargetKind): string {
  switch (kind) {
    case TargetKind.Application:
      return 'Application'
    case TargetKind.DataStore:
      return 'Data Store'
    case TargetKind.External:
      return 'External Resource'
    default:
      return String(kind)
  }
}
</script>

<style scoped>
@import '../styles/pages.css';

.form-grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(220px, 1fr));
  gap: 1rem;
}

.form-dialog {
  min-width: 540px;
}

.dialog-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
}
</style>
