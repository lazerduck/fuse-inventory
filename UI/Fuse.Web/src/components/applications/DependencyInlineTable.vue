<template>
  <q-card class="content-card">
    <q-card-section class="dialog-header">
      <div>
        <div class="text-h6">Dependencies</div>
        <div class="text-caption text-grey-7">
          Describe downstream systems this instance relies on.
        </div>
      </div>
      <div class="row q-gutter-sm items-center">
        <q-checkbox v-model="environmentLocked" label="Lock to environment" dense size="sm" />
        <q-btn
          color="primary"
          label="Add Dependency"
          dense
          icon="add"
          :disable="!canCreate || !instanceId"
          @click="addDraftRow"
        />
      </div>
    </q-card-section>
    <q-separator />
    <q-table
      flat
      bordered
      dense
      :rows="tableRows"
      :columns="columns"
      row-key="id"
    >
      <template #body-cell-targetKind="cellProps">
        <q-td :props="cellProps">
          <q-select
            v-if="cellProps.row.isEditing"
            :model-value="cellProps.row.form.targetKind"
            @update:model-value="(v) => onTargetKindChange(cellProps.row.form, v)"
            dense
            outlined
            emit-value
            map-options
            :options="targetKindOptions"
            class="dep-select"
          />
          <span v-else>{{ cellProps.row.dep.targetKind }}</span>
        </q-td>
      </template>

      <template #body-cell-target="cellProps">
        <q-td :props="cellProps">
          <q-select
            v-if="cellProps.row.isEditing"
            :model-value="cellProps.row.form.targetId"
            @update:model-value="(v) => onTargetIdChange(cellProps.row.form, v)"
            dense
            outlined
            emit-value
            map-options
            :options="getTargetOptions(cellProps.row.form.targetKind)"
            no-option-label="No targets available"
            class="dep-select-wide"
          />
          <span v-else>{{ getTargetLabel(cellProps.row.dep.targetKind, cellProps.row.dep.targetId) }}</span>
        </q-td>
      </template>

      <template #body-cell-port="cellProps">
        <q-td :props="cellProps">
          <q-input
            v-if="cellProps.row.isEditing"
            v-model.number="cellProps.row.form.port"
            dense
            outlined
            type="number"
            :min="1"
            :max="65535"
            :step="1"
            class="dep-input-port"
            clearable
          />
          <span v-else>{{ cellProps.row.dep.port ?? '—' }}</span>
        </q-td>
      </template>

      <template #body-cell-authKind="cellProps">
        <q-td :props="cellProps">
          <q-select
            v-if="cellProps.row.isEditing"
            :model-value="cellProps.row.form.authKind"
            @update:model-value="(v) => onAuthKindChange(cellProps.row.form, v)"
            dense
            outlined
            emit-value
            map-options
            :options="authKindOptions"
            class="dep-select"
          />
          <q-badge v-else :label="cellProps.row.dep.authKind ?? 'None'" outline />
        </q-td>
      </template>

      <template #body-cell-credential="cellProps">
        <q-td :props="cellProps">
          <template v-if="cellProps.row.isEditing">
            <q-select
              v-if="cellProps.row.form.authKind === DependencyAuthKind.Account"
              v-model="cellProps.row.form.accountId"
              dense
              outlined
              emit-value
              map-options
              clearable
              :options="getAccountOptions(cellProps.row.form.targetKind, cellProps.row.form.targetId)"
              :hint="getAccountOptions(cellProps.row.form.targetKind, cellProps.row.form.targetId).length === 0 ? 'No accounts for this target' : undefined"
              class="dep-select-wide"
            />
            <q-select
              v-else-if="cellProps.row.form.authKind === DependencyAuthKind.Identity"
              v-model="cellProps.row.form.identityId"
              dense
              outlined
              emit-value
              map-options
              clearable
              :options="identityOptions"
              :hint="identityOptions.length === 0 ? 'No identities available' : undefined"
              class="dep-select-wide"
            />
            <span v-else class="text-grey-5">—</span>
          </template>
          <span v-else>{{ getCredentialLabel(cellProps.row.dep) }}</span>
        </q-td>
      </template>

      <template #body-cell-actions="cellProps">
        <q-td :props="cellProps" class="text-right">
          <template v-if="cellProps.row.isEditing">
            <q-btn
              dense
              flat
              round
              icon="check"
              color="positive"
              :disable="!isRowValid(cellProps.row.form) || cellProps.row.form.saving"
              :loading="cellProps.row.form.saving"
              @click="saveRow(cellProps.row)"
            >
              <q-tooltip>Save dependency</q-tooltip>
            </q-btn>
            <q-btn
              dense
              flat
              round
              icon="close"
              color="grey-7"
              class="q-ml-xs"
              :disable="cellProps.row.form.saving"
              @click="cancelRow(cellProps.row)"
            >
              <q-tooltip>Discard changes</q-tooltip>
            </q-btn>
          </template>
          <template v-else>
            <q-btn
              dense
              flat
              round
              icon="edit"
              color="primary"
              :disable="!canUpdate"
              @click="startEdit(cellProps.row.dep)"
            />
            <q-btn
              dense
              flat
              round
              icon="delete"
              color="negative"
              class="q-ml-xs"
              :disable="!canDelete"
              @click="confirmDelete(cellProps.row.dep)"
            />
          </template>
        </q-td>
      </template>

      <template #no-data>
        <div class="q-pa-sm text-grey-7">No dependencies documented.</div>
      </template>
    </q-table>
  </q-card>
</template>

<script setup lang="ts">
import { computed, ref, reactive } from 'vue'
import { Notify, Dialog } from 'quasar'
import type { QTableColumn } from 'quasar'
import {
  Account,
  ApplicationInstance,
  ApplicationInstanceDependency,
  CreateApplicationDependency,
  DependencyAuthKind,
  TargetKind,
  UpdateApplicationDependency
} from '../../api/client'
import { useFuseClient } from '../../composables/useFuseClient'
import { useEnvironments } from '../../composables/useEnvironments'
import { useDataStores } from '../../composables/useDataStores'
import { useExternalResources } from '../../composables/useExternalResources'
import { useMessageBrokers } from '../../composables/useMessageBrokers'
import { useQuery, useMutation, useQueryClient } from '@tanstack/vue-query'
import { getErrorMessage } from '../../utils/error'

interface SelectOption<T = string> {
  label: string
  value: T
}

interface DependencyRowForm {
  draftId?: string
  targetKind: TargetKind
  targetId: string | null
  port: number | null
  authKind: DependencyAuthKind
  accountId: string | null
  identityId: string | null
  saving: boolean
}

interface TableRow {
  id: string
  isDraft: boolean
  dep: ApplicationInstanceDependency
  form: DependencyRowForm
  isEditing: boolean
}

interface Props {
  applicationId: string
  instanceId: string
  instance: ApplicationInstance | null | undefined
  canCreate: boolean
  canUpdate: boolean
  canDelete: boolean
}

const props = defineProps<Props>()

const client = useFuseClient()
const queryClient = useQueryClient()
const environmentsStore = useEnvironments()
const dataStoresQuery = useDataStores()
const externalResourcesQuery = useExternalResources()
const messageBrokersQuery = useMessageBrokers()

const environmentLocked = ref(true)
const editingRowForms = reactive<Record<string, DependencyRowForm>>({})
const draftRows = ref<DependencyRowForm[]>([])

const environmentLookup = environmentsStore.lookup

const applicationsQuery = useQuery({
  queryKey: ['applications'],
  queryFn: () => client.applicationAll()
})

const accountsQuery = useQuery({
  queryKey: ['accounts'],
  queryFn: () => client.accountAll()
})

const identitiesQuery = useQuery({
  queryKey: ['identities'],
  queryFn: () => client.identityAll()
})

const targetKindOptions: SelectOption<TargetKind>[] = Object.values(TargetKind).map((value) => ({
  label: value,
  value
}))

const authKindOptions: SelectOption<DependencyAuthKind>[] = [
  { label: 'None', value: DependencyAuthKind.None },
  { label: 'Account', value: DependencyAuthKind.Account },
  { label: 'Identity', value: DependencyAuthKind.Identity }
]

const identityOptions = computed<SelectOption<string>[]>(() => {
  const currentInstanceId = props.instanceId
  return (identitiesQuery.data.value ?? [])
    .filter((identity) => {
      if (!identity.id) return false
      if (!identity.ownerInstanceId) return true
      return identity.ownerInstanceId === currentInstanceId
    })
    .map((identity) => ({
      label: identity.name ?? identity.id!,
      value: identity.id!
    }))
})

const columns: QTableColumn<TableRow>[] = [
  { name: 'targetKind', label: 'Kind', field: (row) => row.dep?.targetKind ?? '', align: 'left' },
  { name: 'target', label: 'Target', field: (row) => row.dep?.targetId ?? '', align: 'left' },
  { name: 'port', label: 'Port', field: (row) => row.dep?.port ?? '', align: 'left' },
  { name: 'authKind', label: 'Auth', field: (row) => row.dep?.authKind ?? '', align: 'left' },
  { name: 'credential', label: 'Credential', field: (row) => row.dep?.accountId ?? row.dep?.identityId ?? '', align: 'left' },
  { name: 'actions', label: '', field: (row) => row.id, align: 'right' }
]

const tableRows = computed<TableRow[]>(() => {
  const savedRows = (props.instance?.dependencies ?? []).map((dep) => ({
    id: dep.id!,
    isDraft: false,
    dep,
    form: editingRowForms[dep.id!] ?? ({} as DependencyRowForm),
    isEditing: dep.id! in editingRowForms
  }))
  const newRows = draftRows.value.map((form) => ({
    id: form.draftId!,
    isDraft: true,
    dep: {} as ApplicationInstanceDependency,
    form,
    isEditing: true
  }))
  return [...savedRows, ...newRows]
})

function createEmptyForm(draftId?: string): DependencyRowForm {
  return {
    draftId,
    targetKind: TargetKind.DataStore,
    targetId: null,
    port: null,
    authKind: DependencyAuthKind.None,
    accountId: null,
    identityId: null,
    saving: false
  }
}

function addDraftRow() {
  const draftId = crypto.randomUUID()
  draftRows.value.push(createEmptyForm(draftId))
}

function startEdit(dep: ApplicationInstanceDependency) {
  if (!dep.id) return
  editingRowForms[dep.id] = {
    targetKind: dep.targetKind ?? TargetKind.DataStore,
    targetId: dep.targetId ?? null,
    port: dep.port ?? null,
    authKind: dep.authKind ?? DependencyAuthKind.None,
    accountId: dep.accountId ?? null,
    identityId: dep.identityId ?? null,
    saving: false
  }
}

function cancelRow(row: TableRow) {
  if (row.isDraft) {
    draftRows.value = draftRows.value.filter((f) => f.draftId !== row.id)
  } else {
    delete editingRowForms[row.id]
  }
}

function isRowValid(form: DependencyRowForm): boolean {
  if (!form.targetId) return false
  if (form.authKind === DependencyAuthKind.Account && !form.accountId) return false
  if (form.authKind === DependencyAuthKind.Identity && !form.identityId) return false
  return true
}

function onTargetKindChange(form: DependencyRowForm, kind: TargetKind) {
  form.targetKind = kind
  form.targetId = null
  form.accountId = null
  form.identityId = null
}

function onTargetIdChange(form: DependencyRowForm, targetId: string | null) {
  form.targetId = targetId
  if (form.authKind === DependencyAuthKind.Account && form.accountId) {
    const valid = getAccountOptions(form.targetKind, targetId).some(
      (opt) => opt.value === form.accountId
    )
    if (!valid) form.accountId = null
  }
}

function onAuthKindChange(form: DependencyRowForm, kind: DependencyAuthKind) {
  form.authKind = kind
  if (kind !== DependencyAuthKind.Account) form.accountId = null
  if (kind !== DependencyAuthKind.Identity) form.identityId = null
}

function getTargetOptions(targetKind: TargetKind): SelectOption<string>[] {
  const currentEnvId = props.instance?.environmentId
  switch (targetKind) {
    case TargetKind.Application: {
      const apps = applicationsQuery.data.value ?? []
      const options: SelectOption<string>[] = []
      for (const app of apps) {
        const appName = app.name ?? app.id ?? 'Application'
        for (const inst of app.instances ?? []) {
          if (!inst?.id) continue
          if (environmentLocked.value && inst.environmentId !== currentEnvId) continue
          const envName = environmentLookup.value[inst.environmentId ?? ''] ?? '—'
          options.push({ label: `${appName} — ${envName}`, value: inst.id })
        }
      }
      return options
    }
    case TargetKind.DataStore:
      return (dataStoresQuery.data.value ?? [])
        .filter((store) => {
          if (!store.id) return false
          if (environmentLocked.value && store.environmentId !== currentEnvId) return false
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
    case TargetKind.MessageBroker:
      return (messageBrokersQuery.data.value ?? [])
        .filter((broker) => !!broker.id)
        .map((broker) => {
          const label = broker.name ?? broker.id!
          if (!environmentLocked.value) {
            const envName = environmentLookup.value[broker.environmentId ?? ''] ?? '—'
            return { label: `${label} — ${envName}`, value: broker.id! }
          }
          return { label, value: broker.id! }
        })
    default:
      return []
  }
}

function getAccountOptions(
  targetKind: TargetKind,
  targetId: string | null
): SelectOption<string>[] {
  if (!targetId) return []
  return (accountsQuery.data.value ?? [])
    .filter((account) => {
      if (!account.id) return false
      return account.targetKind === targetKind && account.targetId === targetId
    })
    .map((account) => ({
      label: formatAccountLabel(account),
      value: account.id!
    }))
}

function formatAccountLabel(account: Account): string {
  const identity = account.userName || account.id || 'Account'
  const targetName = getTargetLabel(account.targetKind, account.targetId ?? null)
  return `${identity} → ${targetName}`
}

function getTargetLabel(kind: TargetKind | undefined, id: string | null): string {
  if (!id) return '—'
  switch (kind) {
    case TargetKind.Application: {
      const apps = applicationsQuery.data.value ?? []
      for (const app of apps) {
        const match = (app.instances ?? []).find((i) => i.id === id)
        if (match) {
          const envName = environmentLookup.value[match.environmentId ?? ''] ?? '—'
          const appName = app.name ?? app.id ?? id
          return `${appName} — ${envName}`
        }
      }
      return id
    }
    case TargetKind.DataStore:
      return dataStoresQuery.data.value?.find((s) => s.id === id)?.name ?? id
    case TargetKind.External:
      return externalResourcesQuery.data.value?.find((r) => r.id === id)?.name ?? id
    case TargetKind.MessageBroker:
      return messageBrokersQuery.data.value?.find((b) => b.id === id)?.name ?? id
    default:
      return id
  }
}

function getCredentialLabel(dep: ApplicationInstanceDependency): string {
  if (dep.authKind === DependencyAuthKind.Account && dep.accountId) {
    const account = accountsQuery.data.value?.find((a) => a.id === dep.accountId)
    if (account) return formatAccountLabel(account)
    return dep.accountId
  }
  if (dep.authKind === DependencyAuthKind.Identity && dep.identityId) {
    const identity = identitiesQuery.data.value?.find((i) => i.id === dep.identityId)
    return identity?.name ?? dep.identityId
  }
  return '—'
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
  onError: (error) => {
    Notify.create({
      type: 'negative',
      message: getErrorMessage(error, 'Unable to update dependency')
    })
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
    Notify.create({
      type: 'negative',
      message: getErrorMessage(error, 'Unable to delete dependency')
    })
  }
})

async function saveRow(row: TableRow) {
  if (!row.form || !row.form.targetId) return
  const form = row.form
  form.saving = true

  const basePayload = {
    applicationId: props.applicationId,
    instanceId: props.instanceId,
    targetKind: form.targetKind,
    targetId: form.targetId,
    port: form.port ?? undefined,
    authKind: form.authKind,
    accountId: form.accountId ?? undefined,
    identityId: form.identityId ?? undefined
  }

  try {
    if (row.isDraft) {
      const payload = Object.assign(new CreateApplicationDependency(), basePayload)
      await createDependencyMutation.mutateAsync({
        appId: props.applicationId,
        instanceId: props.instanceId,
        payload
      })
      queryClient.invalidateQueries({ queryKey: ['applications'] })
      Notify.create({ type: 'positive', message: 'Dependency added' })
      draftRows.value = draftRows.value.filter((f) => f.draftId !== row.id)
    } else {
      const payload = Object.assign(new UpdateApplicationDependency(), {
        ...basePayload,
        dependencyId: row.id
      })
      await updateDependencyMutation.mutateAsync({
        appId: props.applicationId,
        instanceId: props.instanceId,
        dependencyId: row.id,
        payload
      })
      queryClient.invalidateQueries({ queryKey: ['applications'] })
      Notify.create({ type: 'positive', message: 'Dependency updated' })
      delete editingRowForms[row.id]
    }
  } catch {
    form.saving = false
  }
}

function confirmDelete(dep: ApplicationInstanceDependency) {
  if (!dep.id) return
  Dialog.create({
    title: 'Remove dependency',
    message: 'Are you sure you want to remove this dependency?',
    cancel: true,
    persistent: true
  }).onOk(() => {
    deleteDependencyMutation.mutate({
      appId: props.applicationId,
      instanceId: props.instanceId,
      dependencyId: dep.id!
    })
    if (dep.id! in editingRowForms) {
      delete editingRowForms[dep.id!]
    }
  })
}
</script>

<style scoped>
@import '../../styles/pages.css';

.dep-select {
  min-width: 120px;
}

.dep-select-wide {
  min-width: 160px;
}

.dep-input-port {
  min-width: 80px;
  max-width: 100px;
}
</style>
