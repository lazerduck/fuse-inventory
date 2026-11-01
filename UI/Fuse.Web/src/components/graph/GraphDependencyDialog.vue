<template>
  <q-dialog :model-value="true" persistent @update:model-value="value => { if (!value) close() }">
    <q-card class="graph-dependency-dialog">
      <q-card-section class="row items-center justify-between q-gutter-sm">
        <div>
          <div class="text-h6">{{ isEditing ? 'Edit dependency' : fixedTarget ? 'Add dependent' : 'Add dependency' }}</div>
          <div class="text-caption text-grey-7">Describe which resource this application instance uses.</div>
        </div>
        <q-btn flat round dense icon="close" aria-label="Close dependency dialog" :disable="saving" @click="close" />
      </q-card-section>
      <q-separator />
      <q-form ref="formRef" @submit.prevent="save">
        <q-card-section class="q-gutter-md">
          <q-banner v-if="!canUpdate" dense class="bg-orange-1 text-orange-9" role="alert">
            You do not have permission to change instance dependencies.
          </q-banner>
          <q-banner v-if="lookupError" dense class="bg-orange-1 text-orange-9" role="alert">{{ lookupError }}</q-banner>
          <q-select
            v-model="form.sourceInstanceId" label="Source instance" aria-label="Source instance"
            :options="sourceOptions" emit-value map-options outlined dense
            :readonly="fixedSource" :disable="saving" :loading="applicationsQuery.isLoading.value"
            :rules="[requiredSelection]" hint="The instance that depends on the target below."
            @update:model-value="sourceChanged"
          />
          <q-toggle v-model="sameEnvironment" label="Show choices in the same environment" :disable="saving" />
          <div class="row q-col-gutter-md">
            <div class="col-12 col-sm-5">
              <q-select
                :model-value="form.targetKind" label="Target kind" aria-label="Target kind"
                :options="targetKindOptions" emit-value map-options outlined dense
                :readonly="fixedTarget" :disable="saving" @update:model-value="changeTargetKind"
              />
            </div>
            <div class="col-12 col-sm-7">
              <q-select
                v-model="form.targetId" label="Target resource" aria-label="Target resource"
                :options="targetOptions" emit-value map-options outlined dense
                :readonly="fixedTarget" :disable="saving" :loading="targetLoading"
                :rules="[requiredSelection]" no-option-label="No matching resources"
                @update:model-value="targetChanged"
              />
            </div>
          </div>
          <q-banner v-if="crossEnvironment" dense class="bg-blue-1 text-primary">
            This dependency crosses environments: {{ environmentName(sourceInstance?.instance.environmentId) }} → {{ environmentName(selectedTarget?.environmentId) }}.
          </q-banner>
          <div class="row q-col-gutter-md">
            <div class="col-12 col-sm-5">
              <q-input v-model.number="form.port" type="number" min="1" max="65535" step="1" clearable
                label="Port (optional)" aria-label="Port (optional)" outlined dense :disable="saving" :rules="[portRule]" />
            </div>
            <div class="col-12 col-sm-7">
              <q-select v-model="form.severity" label="Dependency severity" aria-label="Dependency severity"
                :options="severityOptions" emit-value map-options outlined dense :disable="saving" />
            </div>
          </div>
          <q-select :model-value="form.authKind" label="Authentication" aria-label="Authentication"
            :options="authOptions" emit-value map-options outlined dense :disable="saving" @update:model-value="changeAuthKind" />
          <q-select v-if="form.authKind === DependencyAuthKind.Account" v-model="form.accountId"
            label="Account" aria-label="Account" :options="accountOptions" emit-value map-options outlined dense
            :rules="[requiredSelection]" :loading="accountsQuery.isLoading.value" :disable="saving" clearable
            hint="Accounts must belong to the selected target resource." no-option-label="No accounts for this target" />
          <q-select v-if="form.authKind === DependencyAuthKind.Identity" v-model="form.identityId"
            label="Identity" aria-label="Identity" :options="identityOptions" emit-value map-options outlined dense
            :rules="[requiredSelection]" :loading="identitiesQuery.isLoading.value" :disable="saving" clearable
            hint="Choose a shared identity or one owned by the source instance." no-option-label="No identities available for this instance" />
          <div v-if="form.authKind === DependencyAuthKind.ApiKey" class="text-caption text-grey-7">
            Uses the target instance's configured API key. Secret values are not shown here.
          </div>
          <q-banner v-if="error" dense class="bg-red-1 text-negative" role="alert">{{ error }}</q-banner>
        </q-card-section>
        <q-separator />
        <q-card-actions align="right">
          <span v-if="draft.isDirty.value" class="text-caption text-grey-7 q-mr-auto" role="status">Unsaved changes</span>
          <q-btn flat label="Cancel" :disable="saving" @click="close" />
          <q-btn color="primary" type="submit" :label="isEditing ? 'Save and close' : 'Add dependency'"
            :loading="saving" :disable="!canUpdate || saving" />
        </q-card-actions>
      </q-form>
    </q-card>
  </q-dialog>
</template>

<script setup lang="ts">
import { computed, reactive, ref, watch } from 'vue'
import { useQuery, useQueryClient } from '@tanstack/vue-query'
import type { QForm } from 'quasar'
import {
  ApplicationInstanceDependency, CreateApplicationDependency, UpdateApplicationDependency,
  DependencyAuthKind, DependencySeverity, SecretBindingKind, TargetKind
} from 'api/client'
import { useFuseClient } from '../../composables/useFuseClient'
import { useFuseStore } from '../../stores/FuseStore'
import { useApplications } from '../../composables/useApplications'
import { useEnvironments } from '../../composables/useEnvironments'
import { useDataStores } from '../../composables/useDataStores'
import { useExternalResources } from '../../composables/useExternalResources'
import { useMessageBrokers } from '../../composables/useMessageBrokers'
import { useUnsavedChanges } from '../../composables/useUnsavedChanges'
import { getErrorMessage } from '../../utils/error'

const props = defineProps<{
  sourceInstanceId?: string
  applicationId?: string
  targetKind?: TargetKind
  targetId?: string
  dependency?: ApplicationInstanceDependency | null
}>()
const emit = defineEmits<{
  close: []
  saved: [result: { applicationId: string; sourceInstanceId: string; dependencyId: string; dependency: ApplicationInstanceDependency }]
}>()

const client = useFuseClient()
const store = useFuseStore()
const queryClient = useQueryClient()
const formRef = ref<QForm | null>(null)
const saving = ref(false)
const error = ref('')
const isEditing = computed(() => !!props.dependency)
const fixedSource = computed(() => !!props.sourceInstanceId || isEditing.value)
const fixedTarget = computed(() => !isEditing.value && !!props.targetKind && !!props.targetId)
const canUpdate = computed(() => store.hasPermission('application:instance:update'))
const applicationsQuery = useApplications()
const environmentsQuery = useEnvironments()
const dataStoresQuery = useDataStores()
const externalsQuery = useExternalResources()
const brokersQuery = useMessageBrokers()
const accountsQuery = useQuery({ queryKey: ['accounts'], queryFn: () => client.accountAll(), enabled: computed(() => store.hasPermission('accounts:read')) })
const identitiesQuery = useQuery({ queryKey: ['identities'], queryFn: () => client.identityAll(), enabled: computed(() => store.hasPermission('identities:read')) })

const form = reactive({
  sourceInstanceId: props.sourceInstanceId ?? null as string | null,
  targetKind: props.dependency?.targetKind ?? props.targetKind ?? TargetKind.Application,
  targetId: props.dependency?.targetId ?? props.targetId ?? null as string | null,
  port: (props.dependency?.port ?? null) as number | string | null,
  severity: props.dependency?.severity ?? DependencySeverity.Full,
  authKind: props.dependency?.authKind ?? DependencyAuthKind.None,
  accountId: props.dependency?.accountId ?? null as string | null,
  identityId: props.dependency?.identityId ?? null as string | null
})
const initialTargetId = props.dependency?.targetId ?? props.targetId ?? null
const normalizedInitialTargetId = ref<string | null>(null)
// Resolving a legacy application ID changes its representation, not the user's
// draft. Canonicalize that one value for dirty tracking without resetting other edits.
const draft = useUnsavedChanges(() => ({
  ...form,
  targetId: normalizedInitialTargetId.value && form.targetId === normalizedInitialTargetId.value
    ? initialTargetId : form.targetId
}))
const sameEnvironment = ref(true)
const instances = computed(() => (applicationsQuery.data.value ?? []).flatMap(application =>
  (application.instances ?? []).filter(instance => !!instance.id).map(instance => ({ application, instance }))
))
const sourceInstance = computed(() => instances.value.find(item => item.instance.id === form.sourceInstanceId))
watch([() => applicationsQuery.data.value, () => sourceInstance.value?.instance.environmentId], () => {
  if (!isEditing.value || !initialTargetId || normalizedInitialTargetId.value ||
    form.targetKind !== TargetKind.Application || form.targetId !== initialTargetId ||
    instances.value.some(item => item.instance.id === initialTargetId)) return
  const environmentId = sourceInstance.value?.instance.environmentId
  if (!environmentId) return
  const application = applicationsQuery.data.value?.find(item => item.id === initialTargetId)
  const matches = application?.instances?.filter(instance => instance.id && instance.environmentId === environmentId) ?? []
  if (matches.length !== 1) return
  normalizedInitialTargetId.value = matches[0]!.id!
  form.targetId = matches[0]!.id!
}, { immediate: true })
function environmentName(id?: string) { return environmentsQuery.lookup.value[id ?? ''] ?? 'Unspecified environment' }
function instanceLabel(item: typeof instances.value[number]) {
  return `${item.application.name ?? item.application.id} — ${environmentName(item.instance.environmentId)}`
}

interface TargetOption { label: string; value: string; environmentId?: string }
const allTargets = computed<TargetOption[]>(() => {
  switch (form.targetKind) {
    case TargetKind.Application:
      return instances.value.map(item => ({ label: instanceLabel(item), value: item.instance.id!, environmentId: item.instance.environmentId }))
    case TargetKind.DataStore:
      return (dataStoresQuery.data.value ?? []).filter(item => !!item.id).map(item => ({ label: `${item.name ?? item.id} — ${environmentName(item.environmentId)}`, value: item.id!, environmentId: item.environmentId }))
    case TargetKind.MessageBroker:
      return (brokersQuery.data.value ?? []).filter(item => !!item.id).map(item => ({ label: `${item.name ?? item.id} — ${environmentName(item.environmentId)}`, value: item.id!, environmentId: item.environmentId }))
    case TargetKind.External:
      return (externalsQuery.data.value ?? []).filter(item => !!item.id).map(item => ({ label: item.name ?? item.id!, value: item.id! }))
  }
})
const selectedTarget = computed(() => allTargets.value.find(target => target.value === form.targetId))
const sourceOptions = computed(() => instances.value
  .filter(item => item.instance.id === form.sourceInstanceId || !sameEnvironment.value || !fixedTarget.value || !selectedTarget.value?.environmentId || item.instance.environmentId === selectedTarget.value.environmentId)
  .map(item => ({ label: instanceLabel(item), value: item.instance.id! })))
const targetOptions = computed(() => allTargets.value.filter(target => target.value === form.targetId || !sameEnvironment.value || !sourceInstance.value?.instance.environmentId || !target.environmentId || target.environmentId === sourceInstance.value.instance.environmentId))
const crossEnvironment = computed(() => !!sourceInstance.value?.instance.environmentId && !!selectedTarget.value?.environmentId && sourceInstance.value.instance.environmentId !== selectedTarget.value.environmentId)
const targetLoading = computed(() => form.targetKind === TargetKind.Application ? applicationsQuery.isLoading.value : form.targetKind === TargetKind.DataStore ? dataStoresQuery.isLoading.value : form.targetKind === TargetKind.MessageBroker ? brokersQuery.isLoading.value : externalsQuery.isLoading.value)
const targetError = computed(() => form.targetKind === TargetKind.Application ? applicationsQuery.error.value : form.targetKind === TargetKind.DataStore ? dataStoresQuery.error.value : form.targetKind === TargetKind.MessageBroker ? brokersQuery.error.value : externalsQuery.error.value)
const lookupError = computed(() => {
  if (applicationsQuery.error.value || targetError.value) return getErrorMessage(applicationsQuery.error.value || targetError.value, 'Unable to load dependency resources.')
  if (form.authKind === DependencyAuthKind.Account) {
    if (!store.hasPermission('accounts:read')) return 'Account read permission is required to choose an account.'
    if (accountsQuery.error.value) return getErrorMessage(accountsQuery.error.value, 'Unable to load accounts.')
  }
  if (form.authKind === DependencyAuthKind.Identity) {
    if (!store.hasPermission('identities:read')) return 'Identity read permission is required to choose an identity.'
    if (identitiesQuery.error.value) return getErrorMessage(identitiesQuery.error.value, 'Unable to load identities.')
  }
  return ''
})
const accountOptions = computed(() => (accountsQuery.data.value ?? [])
  .filter(account => !!account.id && account.targetKind === form.targetKind && account.targetId === form.targetId)
  .map(account => ({ label: account.userName || account.id!, value: account.id! })))
const identityOptions = computed(() => (identitiesQuery.data.value ?? [])
  .filter(identity => !!identity.id && (!identity.ownerInstanceId || identity.ownerInstanceId === form.sourceInstanceId))
  .map(identity => ({ label: identity.name || identity.id!, value: identity.id! })))
const targetHasApiKey = computed(() => form.targetKind === TargetKind.Application && instances.value.some(item =>
  item.instance.id === form.targetId && item.instance.apiKey?.kind && item.instance.apiKey.kind !== SecretBindingKind.None))
const targetKindOptions = [
  { label: 'Application instance', value: TargetKind.Application }, { label: 'Data store', value: TargetKind.DataStore },
  { label: 'External resource', value: TargetKind.External }, { label: 'Message broker', value: TargetKind.MessageBroker }
]
const severityOptions = [{ label: 'Full — cannot work without this resource', value: DependencySeverity.Full }, { label: 'Partial — degraded without this resource', value: DependencySeverity.Partial }]
const authOptions = computed(() => [
  { label: 'None', value: DependencyAuthKind.None }, { label: 'Account', value: DependencyAuthKind.Account },
  { label: 'Identity', value: DependencyAuthKind.Identity }, { label: 'Target API key', value: DependencyAuthKind.ApiKey, disable: !targetHasApiKey.value }
])
const requiredSelection = (value: unknown) => !!value || 'Select an option.'
function portRule(value: unknown) {
  return value === null || value === undefined || value === '' || (Number.isInteger(Number(value)) && Number(value) >= 1 && Number(value) <= 65535) || 'Enter a whole port number from 1 to 65535.'
}
function sourceChanged() {
  if (form.identityId && !identityOptions.value.some(option => option.value === form.identityId)) form.identityId = null
}
function targetChanged() {
  if (form.accountId && !accountOptions.value.some(option => option.value === form.accountId)) form.accountId = null
  if (form.authKind === DependencyAuthKind.ApiKey && !targetHasApiKey.value) form.authKind = DependencyAuthKind.None
}
function changeTargetKind(kind: TargetKind) {
  if (fixedTarget.value || saving.value || form.targetKind === kind) return
  form.targetKind = kind
  form.targetId = null
  form.accountId = null
  if (form.authKind === DependencyAuthKind.ApiKey) form.authKind = DependencyAuthKind.None
}
function changeAuthKind(kind: DependencyAuthKind) {
  form.authKind = kind
  if (kind !== DependencyAuthKind.Account) form.accountId = null
  if (kind !== DependencyAuthKind.Identity) form.identityId = null
}

async function close() {
  if (!saving.value) await draft.discard(() => emit('close'))
}
async function save() {
  if (saving.value) return
  error.value = ''
  if (!canUpdate.value) { error.value = 'You do not have permission to change instance dependencies.'; return }
  if (!await formRef.value?.validate()) return
  if (!canUpdate.value || saving.value) return
  const source = sourceInstance.value
  if (!source?.application.id || !source.instance.id || !selectedTarget.value || !form.targetId) {
    error.value = 'Select an available source instance and target resource.'; return
  }
  if (props.applicationId && fixedSource.value && props.applicationId !== source.application.id) {
    error.value = 'The source instance does not belong to the selected application.'; return
  }
  if (isEditing.value && !props.dependency?.id) { error.value = 'The dependency could not be identified. Reopen it from the graph.'; return }
  if (form.authKind === DependencyAuthKind.Account && (!store.hasPermission('accounts:read') || !accountOptions.value.some(option => option.value === form.accountId))) {
    error.value = 'Choose an account belonging to the selected target resource.'; return
  }
  if (form.authKind === DependencyAuthKind.Identity && (!store.hasPermission('identities:read') || !identityOptions.value.some(option => option.value === form.identityId))) {
    error.value = 'Choose a shared identity or one owned by the source instance.'; return
  }
  if (form.authKind === DependencyAuthKind.ApiKey && !targetHasApiKey.value) {
    error.value = 'The target must be an application instance with an API key configured.'; return
  }
  const payload = {
    applicationId: source.application.id, instanceId: source.instance.id,
    targetKind: form.targetKind, targetId: form.targetId,
    port: form.port === null || form.port === '' ? undefined : Number(form.port),
    severity: form.severity, authKind: form.authKind,
    accountId: form.authKind === DependencyAuthKind.Account ? form.accountId! : undefined,
    identityId: form.authKind === DependencyAuthKind.Identity ? form.identityId! : undefined
  }
  saving.value = true
  draft.captureSave()
  try {
    const dependency = isEditing.value
      ? await client.dependenciesPUT(source.application.id, source.instance.id, props.dependency!.id!, new UpdateApplicationDependency({ ...payload, dependencyId: props.dependency!.id }))
      : await client.dependenciesPOST(source.application.id, source.instance.id, new CreateApplicationDependency(payload))
    draft.markSaved()
    void queryClient.invalidateQueries({ queryKey: ['applications'] })
    void queryClient.invalidateQueries({ queryKey: ['application', source.application.id] })
    emit('saved', { applicationId: source.application.id, sourceInstanceId: source.instance.id, dependencyId: dependency.id ?? props.dependency?.id ?? '', dependency })
    emit('close')
  } catch (cause) {
    error.value = getErrorMessage(cause, 'Unable to save the dependency. Your changes are still here.')
  } finally { saving.value = false }
}
</script>

<style scoped>
.graph-dependency-dialog { width: 680px; max-width: 96vw; }
</style>
