<template>
  <div class="page-container">
    <div class="page-header">
      <div>
        <h1>Accounts</h1>
        <p class="subtitle">Manage credentials, authorisations, and grants for your targets.</p>
      </div>
      <q-btn color="primary" label="Create Account" icon="add" @click="openCreateDialog" />
    </div>

    <q-banner v-if="accountError" dense class="bg-red-1 text-negative q-mb-md">
      {{ accountError }}
    </q-banner>

    <AccountsTable
      :accounts="accounts"
      :loading="isLoading"
      :pagination="pagination"
      :tag-lookup="tagLookup"
      :target-resolver="resolveTargetName"
      @edit="openEditDialog"
      @delete="confirmDelete"
    />

    <q-dialog v-model="isCreateDialogOpen" persistent>
      <q-card class="form-dialog">
        <q-card-section class="dialog-header">
          <div class="text-h6">Create Account</div>
          <q-btn flat round dense icon="close" @click="isCreateDialogOpen = false" />
        </q-card-section>
        <q-separator />
        <q-form @submit.prevent="submitCreate">
          <q-card-section>
            <AccountForm
              v-model="createForm"
              :target-kind-options="targetKindOptions"
              :target-options="createTargetOptions"
              :auth-kind-options="authKindOptions"
              :tag-options="tagOptions"
            />
            <AccountGrantsSection
              :grants="createForm.grants"
              @add="openCreateGrantDialog()"
              @edit="openCreateGrantDialog"
              @delete="({ index }) => confirmCreateGrantDelete(index)"
            />
          </q-card-section>
          <q-separator />
          <q-card-actions align="right">
            <q-btn flat label="Cancel" @click="isCreateDialogOpen = false" />
            <q-btn color="primary" type="submit" label="Create" :loading="createMutation.isPending.value" />
          </q-card-actions>
        </q-form>
      </q-card>
    </q-dialog>

    <q-dialog v-model="isEditDialogOpen" persistent>
      <q-card class="form-dialog large">
        <q-card-section class="dialog-header">
          <div class="text-h6">Edit Account</div>
          <q-btn flat round dense icon="close" @click="closeEditDialog" />
        </q-card-section>
        <q-separator />
        <q-form @submit.prevent="submitEdit">
          <q-card-section>
            <AccountForm
              v-model="editForm"
              :target-kind-options="targetKindOptions"
              :target-options="editTargetOptions"
              :auth-kind-options="authKindOptions"
              :tag-options="tagOptions"
            />

            <AccountGrantsSection
              :grants="selectedAccount?.grants ?? []"
              :disable-actions="grantMutationPending"
              @add="openGrantDialog()"
              @edit="({ grant }) => openGrantDialog(grant)"
              @delete="({ grant }) => confirmGrantDelete(grant)"
            />
          </q-card-section>
          <q-separator />
          <q-card-actions align="right">
            <q-btn flat label="Cancel" @click="closeEditDialog" />
            <q-btn color="primary" type="submit" label="Save" :loading="updateMutation.isPending.value" />
          </q-card-actions>
        </q-form>
      </q-card>
    </q-dialog>

    <q-dialog v-model="isGrantDialogOpen" persistent>
      <q-card class="form-dialog">
        <q-card-section class="dialog-header">
          <div class="text-h6">{{ grantDialogTitle }}</div>
          <q-btn flat round dense icon="close" @click="closeGrantDialog" />
        </q-card-section>
        <q-separator />
        <q-form @submit.prevent="submitGrant">
          <q-card-section>
            <div class="form-grid">
              <q-input v-model="grantForm.database" label="Database" dense outlined />
              <q-input v-model="grantForm.schema" label="Schema" dense outlined />
              <q-select
                v-model="grantForm.privileges"
                label="Privileges"
                dense
                outlined
                use-chips
                multiple
                emit-value
                map-options
                :options="privilegeOptions"
              />
            </div>
          </q-card-section>
          <q-separator />
          <q-card-actions align="right">
            <q-btn flat label="Cancel" @click="closeGrantDialog" />
            <q-btn
              color="primary"
              type="submit"
              :label="grantDialogSubmitLabel"
              :loading="grantDialogLoading"
            />
          </q-card-actions>
        </q-form>
      </q-card>
    </q-dialog>
  </div>
</template>

<script setup lang="ts">
import { computed, reactive, ref, watch } from 'vue'
import { useMutation, useQuery, useQueryClient } from '@tanstack/vue-query'
import { Notify, Dialog } from 'quasar'
import {
  Account,
  AuthKind,
  CreateAccount,
  CreateAccountGrant,
  Grant,
  Privilege,
  TargetKind,
  UpdateAccount,
  UpdateAccountGrant
} from '../api/client'
import AccountForm from '../components/accounts/AccountForm.vue'
import AccountGrantsSection from '../components/accounts/AccountGrantsSection.vue'
import AccountsTable from '../components/accounts/AccountsTable.vue'
import type { AccountFormModel, KeyValuePair, TargetOption, SelectOption } from '../components/accounts/types'
import { useFuseClient } from '../composables/useFuseClient'
import { useTags } from '../composables/useTags'
import { useApplications } from '../composables/useApplications'
import { useDataStores } from '../composables/useDataStores'
import { useExternalResources } from '../composables/useExternalResources'
import { getErrorMessage } from '../utils/error'

interface GrantForm {
  database: string
  schema: string
  privileges: Privilege[]
}

const client = useFuseClient()
const queryClient = useQueryClient()
const tagsStore = useTags()
const applicationsQuery = useApplications()
const dataStoresQuery = useDataStores()
const externalResourcesQuery = useExternalResources()

const pagination = { rowsPerPage: 10 }

const { data, isLoading, error } = useQuery({
  queryKey: ['accounts'],
  queryFn: () => client.accountAll()
})

const accounts = computed(() => data.value ?? [])
const accountError = computed(() => (error.value ? getErrorMessage(error.value) : null))

const tagOptions = computed<TargetOption[]>(() => tagsStore.options.value)
const tagLookup = tagsStore.lookup

const targetKindOptions: SelectOption<TargetKind>[] = Object.values(TargetKind).map((value) => ({ label: value, value }))
const authKindOptions: SelectOption<AuthKind>[] = Object.values(AuthKind).map((value) => ({ label: value, value }))
const privilegeOptions = Object.values(Privilege).map((value) => ({ label: value, value }))

const isCreateDialogOpen = ref(false)
const isEditDialogOpen = ref(false)
const selectedAccount = ref<Account | null>(null)

const emptyAccountForm = (): AccountFormModel => ({
  targetKind: TargetKind.Application,
  targetId: null,
  authKind: AuthKind.None,
  userName: '',
  secretRef: '',
  parameters: [],
  tagIds: [],
  grants: []
})

let createForm = reactive<AccountFormModel>(emptyAccountForm())
let editForm = reactive<AccountFormModel & { id: string | null }>({ id: null, ...emptyAccountForm() })

type GrantDialogMode = 'create' | 'edit'

const isGrantDialogOpen = ref(false)
const grantDialogMode = ref<GrantDialogMode>('edit')
const createGrantIndex = ref<number | null>(null)
const editingGrant = ref<Grant | null>(null)
const grantForm = reactive<GrantForm>({ database: '', schema: '', privileges: [] })

const grantDialogTitle = computed(() => {
  if (grantDialogMode.value === 'create') {
    return createGrantIndex.value !== null ? 'Edit Grant' : 'Add Grant'
  }
  return editingGrant.value ? 'Edit Grant' : 'Add Grant'
})

const grantDialogSubmitLabel = computed(() => {
  const isEditing =
    grantDialogMode.value === 'create' ? createGrantIndex.value !== null : editingGrant.value !== null
  return isEditing ? 'Save' : 'Create'
})

const createTargetOptions = computed<TargetOption[]>(() => targetOptions(createForm.targetKind))
const editTargetOptions = computed<TargetOption[]>(() => targetOptions(editForm.targetKind))

function openCreateDialog() {
  Object.assign(createForm, emptyAccountForm())
  createForm.targetId = defaultTargetFor(createForm.targetKind)
  isCreateDialogOpen.value = true
}

function openEditDialog(account: Account) {
  if (!account.id) return
  selectedAccount.value = account
  Object.assign(editForm, {
    id: account.id ?? null,
    targetKind: account.targetKind ?? TargetKind.Application,
    targetId: account.targetId ?? null,
    authKind: account.authKind ?? AuthKind.None,
    userName: account.userName ?? '',
    secretRef: account.secretRef ?? '',
    parameters: convertParametersToPairs(account.parameters),
    tagIds: [...(account.tagIds ?? [])],
    grants: (account.grants ?? []).map(cloneGrant)
  })
  ensureTarget(editForm)
  isEditDialogOpen.value = true
}

function closeEditDialog() {
  selectedAccount.value = null
  isEditDialogOpen.value = false
}

function targetOptions(kind: TargetKind | null): TargetOption[] {
  const k = kind ?? TargetKind.Application
  if (k === TargetKind.Application) {
    // Use application instances as selectable targets
    const apps = applicationsQuery.data.value ?? []
    const options: TargetOption[] = []
    for (const app of apps) {
      const appName = app.name ?? app.id ?? 'Application'
      for (const inst of app.instances ?? []) {
        if (!inst?.id) continue
        options.push({ label: `${appName} — ${inst.environmentId ? inst.environmentId : '—'}` , value: inst.id })
      }
    }
    return options
  }
  if (k === TargetKind.DataStore) {
    return (dataStoresQuery.data.value ?? [])
      .filter((item) => !!item.id)
      .map((item) => ({ label: item.name ?? item.id!, value: item.id! }))
  }
  return (externalResourcesQuery.data.value ?? [])
    .filter((item) => !!item.id)
    .map((item) => ({ label: item.name ?? item.id!, value: item.id! }))
}

function defaultTargetFor(kind: TargetKind) {
  const options = targetOptions(kind)
  return options[0]?.value ?? null
}

watch(
  () => createForm.targetKind,
  (kind) => {
    createForm.targetId = defaultTargetFor(kind)
  }
)

watch(
  () => editForm.targetKind,
  (kind) => {
    ensureTarget(editForm, kind)
  }
)

watch(
  () => [applicationsQuery.data.value, dataStoresQuery.data.value, externalResourcesQuery.data.value],
  () => {
    ensureTarget(createForm)
    if (isEditDialogOpen.value) {
      ensureTarget(editForm)
    }
  }
)

watch(data, (accounts) => {
  if (!accounts || !selectedAccount.value?.id) {
    return
  }
  const latest = accounts.find((account) => account.id === selectedAccount.value?.id)
  if (latest) {
    selectedAccount.value = latest
    editForm.grants = (latest.grants ?? []).map(cloneGrant)
  }
})

function ensureTarget(form: { targetKind: TargetKind; targetId: string | null }, newKind?: TargetKind) {
  const kind = newKind ?? form.targetKind
  const options = targetOptions(kind)
  if (!form.targetId || !options.some((option) => option.value === form.targetId)) {
    form.targetId = options[0]?.value ?? null
  }
}

function convertParametersToPairs(parameters?: { [key: string]: string }) {
  if (!parameters) return []
  return Object.entries(parameters).map(([key, value]) => ({ key, value }))
}

function buildParameters(list: KeyValuePair[]) {
  const result: Record<string, string> = {}
  for (const pair of list) {
    if (pair.key) {
      result[pair.key] = pair.value
    }
  }
  return Object.keys(result).length ? result : undefined
}

function cloneGrant(grant: Grant): Grant {
  return Object.assign(new Grant(), {
    id: grant.id ?? undefined,
    database: grant.database ?? undefined,
    schema: grant.schema ?? undefined,
    privileges: grant.privileges ? [...grant.privileges] : undefined
  })
}

function buildGrantPayload(grant: Grant) {
  return Object.assign(new Grant(), {
    id: grant.id ?? undefined,
    database: grant.database || undefined,
    schema: grant.schema || undefined,
    privileges: grant.privileges && grant.privileges.length ? [...grant.privileges] : undefined
  })
}

const createMutation = useMutation({
  mutationFn: (payload: CreateAccount) => client.accountPOST(payload),
  onSuccess: () => {
    queryClient.invalidateQueries({ queryKey: ['accounts'] })
    Notify.create({ type: 'positive', message: 'Account created' })
    isCreateDialogOpen.value = false
  },
  onError: (err) => {
    Notify.create({ type: 'negative', message: getErrorMessage(err, 'Unable to create account') })
  }
})

const updateMutation = useMutation({
  mutationFn: ({ id, payload }: { id: string; payload: UpdateAccount }) => client.accountPUT(id, payload),
  onSuccess: () => {
    queryClient.invalidateQueries({ queryKey: ['accounts'] })
    Notify.create({ type: 'positive', message: 'Account updated' })
    closeEditDialog()
  },
  onError: (err) => {
    Notify.create({ type: 'negative', message: getErrorMessage(err, 'Unable to update account') })
  }
})

const deleteMutation = useMutation({
  mutationFn: (id: string) => client.accountDELETE(id),
  onSuccess: () => {
    queryClient.invalidateQueries({ queryKey: ['accounts'] })
    Notify.create({ type: 'positive', message: 'Account deleted' })
  },
  onError: (err) => {
    Notify.create({ type: 'negative', message: getErrorMessage(err, 'Unable to delete account') })
  }
})

function submitCreate() {
  const payload = Object.assign(new CreateAccount(), {
    targetKind: createForm.targetKind,
    targetId: createForm.targetId ?? undefined,
    authKind: createForm.authKind,
    userName: createForm.userName || undefined,
    secretRef: createForm.secretRef || undefined,
    parameters: buildParameters(createForm.parameters),
    grants: createForm.grants.map(buildGrantPayload),
    tagIds: createForm.tagIds.length ? [...createForm.tagIds] : undefined
  })
  createMutation.mutate(payload)
}

function submitEdit() {
  if (!editForm.id) return
  const payload = Object.assign(new UpdateAccount(), {
    targetKind: editForm.targetKind,
    targetId: editForm.targetId ?? undefined,
    authKind: editForm.authKind,
    userName: editForm.userName || undefined,
    secretRef: editForm.secretRef || undefined,
    parameters: buildParameters(editForm.parameters),
    grants: (selectedAccount.value?.grants ?? []).map(buildGrantPayload),
    tagIds: editForm.tagIds.length ? [...editForm.tagIds] : undefined
  })
  updateMutation.mutate({ id: editForm.id, payload })
}

function confirmDelete(account: Account) {
  if (!account.id) return
  Dialog.create({
    title: 'Delete account',
    message: `Delete account for "${resolveTargetName(account)}"?`,
    cancel: true,
    persistent: true
  }).onOk(() => deleteMutation.mutate(account.id!))
}

const createGrantMutation = useMutation({
  mutationFn: ({ accountId, payload }: { accountId: string; payload: CreateAccountGrant }) =>
    client.grantPOST(accountId, payload),
  onSuccess: () => {
    queryClient.invalidateQueries({ queryKey: ['accounts'] })
    Notify.create({ type: 'positive', message: 'Grant created' })
    closeGrantDialog()
  },
  onError: (err) => {
    Notify.create({ type: 'negative', message: getErrorMessage(err, 'Unable to create grant') })
  }
})

const updateGrantMutation = useMutation({
  mutationFn: ({ accountId, grantId, payload }: { accountId: string; grantId: string; payload: UpdateAccountGrant }) =>
    client.grantPUT(accountId, grantId, payload),
  onSuccess: () => {
    queryClient.invalidateQueries({ queryKey: ['accounts'] })
    Notify.create({ type: 'positive', message: 'Grant updated' })
    closeGrantDialog()
  },
  onError: (err) => {
    Notify.create({ type: 'negative', message: getErrorMessage(err, 'Unable to update grant') })
  }
})

const deleteGrantMutation = useMutation({
  mutationFn: ({ accountId, grantId }: { accountId: string; grantId: string }) => client.grantDELETE(accountId, grantId),
  onSuccess: () => {
    queryClient.invalidateQueries({ queryKey: ['accounts'] })
    Notify.create({ type: 'positive', message: 'Grant removed' })
  },
  onError: (err) => {
    Notify.create({ type: 'negative', message: getErrorMessage(err, 'Unable to delete grant') })
  }
})

const grantMutationPending = computed(
  () => createGrantMutation.isPending.value || updateGrantMutation.isPending.value
)

const grantDialogLoading = computed(() => grantDialogMode.value === 'edit' && grantMutationPending.value)

function openCreateGrantDialog(payload?: { grant: Grant; index: number }) {
  grantDialogMode.value = 'create'
  createGrantIndex.value = payload?.index ?? null
  editingGrant.value = null
  if (payload?.grant) {
    const grant = payload.grant
    Object.assign(grantForm, {
      database: grant.database ?? '',
      schema: grant.schema ?? '',
      privileges: [...(grant.privileges ?? [])]
    })
  } else {
    Object.assign(grantForm, { database: '', schema: '', privileges: [] })
  }
  isGrantDialogOpen.value = true
}

function openGrantDialog(grant?: Grant) {
  if (!selectedAccount.value?.id) {
    Notify.create({ type: 'warning', message: 'Select an account first' })
    return
  }
  grantDialogMode.value = 'edit'
  createGrantIndex.value = null
  if (grant) {
    editingGrant.value = grant
    Object.assign(grantForm, {
      database: grant.database ?? '',
      schema: grant.schema ?? '',
      privileges: [...(grant.privileges ?? [])]
    })
  } else {
    editingGrant.value = null
    Object.assign(grantForm, { database: '', schema: '', privileges: [] })
  }
  isGrantDialogOpen.value = true
}

function closeGrantDialog() {
  isGrantDialogOpen.value = false
  grantDialogMode.value = 'edit'
  createGrantIndex.value = null
  editingGrant.value = null
}

function submitGrant() {
  if (grantDialogMode.value === 'create') {
    const existingId =
      createGrantIndex.value !== null ? createForm.grants[createGrantIndex.value]?.id : undefined
    const draft = Object.assign(new Grant(), {
      id: existingId ?? undefined,
      database: grantForm.database || undefined,
      schema: grantForm.schema || undefined,
      privileges: grantForm.privileges.length ? [...grantForm.privileges] : undefined
    })
    if (createGrantIndex.value !== null) {
      createForm.grants.splice(createGrantIndex.value, 1, draft)
    } else {
      createForm.grants.push(draft)
    }
    closeGrantDialog()
    return
  }

  if (!selectedAccount.value?.id) return
  if (editingGrant.value?.id) {
    const payload = Object.assign(new UpdateAccountGrant(), {
      database: grantForm.database || undefined,
      schema: grantForm.schema || undefined,
      privileges: grantForm.privileges.length ? [...grantForm.privileges] : undefined
    })
    updateGrantMutation.mutate({
      accountId: selectedAccount.value.id!,
      grantId: editingGrant.value.id!,
      payload
    })
  } else {
    const payload = Object.assign(new CreateAccountGrant(), {
      database: grantForm.database || undefined,
      schema: grantForm.schema || undefined,
      privileges: grantForm.privileges.length ? [...grantForm.privileges] : undefined
    })
    createGrantMutation.mutate({ accountId: selectedAccount.value.id!, payload })
  }
}

function confirmCreateGrantDelete(index: number) {
  if (index < 0 || index >= createForm.grants.length) return
  Dialog.create({
    title: 'Remove grant',
    message: 'Are you sure you want to remove this grant?',
    cancel: true,
    persistent: true
  }).onOk(() => {
    createForm.grants.splice(index, 1)
  })
}

function confirmGrantDelete(grant: Grant) {
  if (!selectedAccount.value?.id || !grant.id) return
  Dialog.create({
    title: 'Delete grant',
    message: 'Are you sure you want to remove this grant?',
    cancel: true,
    persistent: true
  }).onOk(() => deleteGrantMutation.mutate({ accountId: selectedAccount.value!.id!, grantId: grant.id! }))
}

function resolveTargetName(account: Account) {
  const targetId = account.targetId
  if (!targetId) return '—'
  switch (account.targetKind) {
    case TargetKind.Application: {
      // Treat targetId as instance id; fallback to legacy application id
      const apps = applicationsQuery.data.value ?? []
      for (const app of apps) {
        const inst = (app.instances ?? []).find((i) => i.id === targetId)
        if (inst) {
          const appName = app.name ?? app.id ?? 'Application'
          return `${appName} — ${inst.environmentId ?? '—'}`
        }
      }
      return apps.find((a) => a.id === targetId)?.name ?? targetId
    }
    case TargetKind.DataStore:
      return dataStoresQuery.data.value?.find((item) => item.id === targetId)?.name ?? targetId
    case TargetKind.External:
      return externalResourcesQuery.data.value?.find((item) => item.id === targetId)?.name ?? targetId
    default:
      return targetId
  }
}
</script>

<style scoped>
@import '../styles/pages.css';

.form-dialog {
  min-width: 540px;
}

.form-dialog.large {
  min-width: 760px;
}
</style>
