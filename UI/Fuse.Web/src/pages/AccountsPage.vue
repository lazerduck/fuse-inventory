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

    <q-card class="content-card">
      <q-table
        flat
        bordered
        :rows="accounts"
        :columns="columns"
        row-key="id"
        :loading="isLoading"
        :pagination="pagination"
      >
        <template #body-cell-target="props">
          <q-td :props="props">
            {{ resolveTargetName(props.row) }}
          </q-td>
        </template>
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
        <template #body-cell-grants="props">
          <q-td :props="props">
            <q-badge color="secondary" :label="`${props.row.grants?.length ?? 0} grants`" />
          </q-td>
        </template>
        <template #body-cell-actions="props">
          <q-td :props="props" class="text-right">
            <q-btn flat dense round icon="edit" color="primary" @click="openEditDialog(props.row)" />
            <q-btn
              flat
              dense
              round
              icon="delete"
              color="negative"
              class="q-ml-xs"
              @click="confirmDelete(props.row)"
            />
          </q-td>
        </template>
        <template #no-data>
          <div class="q-pa-md text-grey-7">No accounts configured.</div>
        </template>
      </q-table>
    </q-card>

    <q-dialog v-model="isCreateDialogOpen" persistent>
      <q-card class="form-dialog">
        <q-card-section class="dialog-header">
          <div class="text-h6">Create Account</div>
          <q-btn flat round dense icon="close" @click="isCreateDialogOpen = false" />
        </q-card-section>
        <q-separator />
        <q-form @submit.prevent="submitCreate">
          <q-card-section>
            <div class="form-grid">
              <q-select
                v-model="createForm.targetKind"
                label="Target Kind"
                dense
                outlined
                emit-value
                map-options
                :options="targetKindOptions"
              />
              <q-select
                v-model="createForm.targetId"
                label="Target"
                dense
                outlined
                emit-value
                map-options
                :options="targetOptions(createForm.targetKind)"
              />
              <q-select
                v-model="createForm.authKind"
                label="Auth Kind"
                dense
                outlined
                emit-value
                map-options
                :options="authKindOptions"
              />
              <q-input v-model="createForm.userName" label="Username" dense outlined />
              <q-input v-model="createForm.secretRef" label="Secret Reference" dense outlined />
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
            </div>
            <div class="parameters-section">
              <div class="section-header q-mt-md">
                <div class="text-subtitle1">Parameters</div>
                <q-btn dense flat icon="add" label="Add" @click="addParameter(createForm.parameters)" />
              </div>
              <div v-if="createForm.parameters.length" class="parameter-grid">
                <div v-for="(pair, index) in createForm.parameters" :key="index" class="parameter-row">
                  <q-input v-model="pair.key" label="Key" dense outlined />
                  <q-input v-model="pair.value" label="Value" dense outlined />
                  <q-btn flat dense round icon="delete" color="negative" @click="removeParameter(createForm.parameters, index)" />
                </div>
              </div>
              <div v-else class="text-grey">No parameters.</div>
            </div>
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
            <div class="form-grid">
              <q-select
                v-model="editForm.targetKind"
                label="Target Kind"
                dense
                outlined
                emit-value
                map-options
                :options="targetKindOptions"
              />
              <q-select
                v-model="editForm.targetId"
                label="Target"
                dense
                outlined
                emit-value
                map-options
                :options="targetOptions(editForm.targetKind)"
              />
              <q-select
                v-model="editForm.authKind"
                label="Auth Kind"
                dense
                outlined
                emit-value
                map-options
                :options="authKindOptions"
              />
              <q-input v-model="editForm.userName" label="Username" dense outlined />
              <q-input v-model="editForm.secretRef" label="Secret Reference" dense outlined />
              <q-select
                v-model="editForm.tagIds"
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
            <div class="parameters-section">
              <div class="section-header q-mt-md">
                <div class="text-subtitle1">Parameters</div>
                <q-btn dense flat icon="add" label="Add" @click="addParameter(editForm.parameters)" />
              </div>
              <div v-if="editForm.parameters.length" class="parameter-grid">
                <div v-for="(pair, index) in editForm.parameters" :key="index" class="parameter-row">
                  <q-input v-model="pair.key" label="Key" dense outlined />
                  <q-input v-model="pair.value" label="Value" dense outlined />
                  <q-btn flat dense round icon="delete" color="negative" @click="removeParameter(editForm.parameters, index)" />
                </div>
              </div>
              <div v-else class="text-grey">No parameters.</div>
            </div>

            <q-expansion-item dense expand-icon="expand_more" icon="security" label="Grants" class="q-mt-lg">
              <template #default>
                <div class="section-header">
                  <div>
                    <div class="text-subtitle1">Account Grants</div>
                    <div class="text-caption text-grey-7">Document permissions granted to this account.</div>
                  </div>
                  <q-btn color="primary" label="Add Grant" dense icon="add" @click="openGrantDialog()" />
                </div>
                <q-table
                  flat
                  bordered
                  dense
                  :rows="selectedAccount?.grants ?? []"
                  :columns="grantColumns"
                  row-key="id"
                  class="q-mt-md"
                >
                  <template #body-cell-privileges="props">
                    <q-td :props="props">
                      <div v-if="props.row.privileges?.length" class="tag-list">
                        <q-badge
                          v-for="privilege in props.row.privileges"
                          :key="privilege"
                          outline
                          color="secondary"
                          :label="privilege"
                        />
                      </div>
                      <span v-else class="text-grey">—</span>
                    </q-td>
                  </template>
                  <template #body-cell-actions="props">
                    <q-td :props="props" class="text-right">
                      <q-btn dense flat round icon="edit" color="primary" @click="openGrantDialog(props.row)" />
                      <q-btn
                        dense
                        flat
                        round
                        icon="delete"
                        color="negative"
                        class="q-ml-xs"
                        @click="confirmGrantDelete(props.row)"
                      />
                    </q-td>
                  </template>
                  <template #no-data>
                    <div class="q-pa-sm text-grey-7">No grants for this account.</div>
                  </template>
                </q-table>
              </template>
            </q-expansion-item>
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
          <div class="text-h6">{{ editingGrant ? 'Edit Grant' : 'Add Grant' }}</div>
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
              :label="editingGrant ? 'Save' : 'Create'"
              :loading="grantMutationPending"
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
import type { QTableColumn } from 'quasar'
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
import { useFuseClient } from '../composables/useFuseClient'
import { useTags } from '../composables/useTags'
import { useApplications } from '../composables/useApplications'
import { useDataStores } from '../composables/useDataStores'
import { useExternalResources } from '../composables/useExternalResources'
import { getErrorMessage } from '../utils/error'

interface KeyValuePair {
  key: string
  value: string
}

interface AccountForm {
  targetKind: TargetKind
  targetId: string | null
  authKind: AuthKind
  userName: string
  secretRef: string
  parameters: KeyValuePair[]
  tagIds: string[]
}

interface GrantForm {
  database: string
  schema: string
  privileges: Privilege[]
}

type TargetOption = { label: string; value: string }

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

const tagOptions = tagsStore.options
const tagLookup = tagsStore.lookup

const targetKindOptions = Object.values(TargetKind).map((value) => ({ label: value, value }))
const authKindOptions = Object.values(AuthKind).map((value) => ({ label: value, value }))
const privilegeOptions = Object.values(Privilege).map((value) => ({ label: value, value }))

const columns: QTableColumn<Account>[] = [
  { name: 'target', label: 'Target', field: 'targetId', align: 'left', sortable: true },
  { name: 'authKind', label: 'Auth Kind', field: 'authKind', align: 'left' },
  { name: 'userName', label: 'Username', field: 'userName', align: 'left' },
  { name: 'grants', label: 'Grants', field: 'grants', align: 'left' },
  { name: 'tags', label: 'Tags', field: 'tagIds', align: 'left' },
  { name: 'actions', label: '', field: (row) => row.id, align: 'right' }
]

const grantColumns: QTableColumn<Grant>[] = [
  { name: 'database', label: 'Database', field: 'database', align: 'left' },
  { name: 'schema', label: 'Schema', field: 'schema', align: 'left' },
  { name: 'privileges', label: 'Privileges', field: 'privileges', align: 'left' },
  { name: 'actions', label: '', field: (row) => row.id, align: 'right' }
]

const isCreateDialogOpen = ref(false)
const isEditDialogOpen = ref(false)
const selectedAccount = ref<Account | null>(null)

const emptyAccountForm = (): AccountForm => ({
  targetKind: TargetKind.Application,
  targetId: null,
  authKind: AuthKind.None,
  userName: '',
  secretRef: '',
  parameters: [],
  tagIds: []
})

const createForm = reactive<AccountForm>(emptyAccountForm())
const editForm = reactive<AccountForm & { id: string | null }>({ id: null, ...emptyAccountForm() })

const isGrantDialogOpen = ref(false)
const editingGrant = ref<Grant | null>(null)
const grantForm = reactive<GrantForm>({ database: '', schema: '', privileges: [] })

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
    tagIds: [...(account.tagIds ?? [])]
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
    return (applicationsQuery.data.value ?? [])
      .filter((item) => !!item.id)
      .map((item) => ({ label: item.name ?? item.id!, value: item.id! }))
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

function ensureTarget(form: { targetKind: TargetKind; targetId: string | null }, newKind?: TargetKind) {
  const kind = newKind ?? form.targetKind
  const options = targetOptions(kind)
  if (!form.targetId || !options.some((option) => option.value === form.targetId)) {
    form.targetId = options[0]?.value ?? null
  }
}

function addParameter(list: KeyValuePair[]) {
  list.push({ key: '', value: '' })
}

function removeParameter(list: KeyValuePair[], index: number) {
  list.splice(index, 1)
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

function openGrantDialog(grant?: Grant) {
  if (!selectedAccount.value?.id) {
    Notify.create({ type: 'warning', message: 'Select an account first' })
    return
  }
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
  editingGrant.value = null
}

function submitGrant() {
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
    case TargetKind.Application:
      return applicationsQuery.data.value?.find((item) => item.id === targetId)?.name ?? targetId
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
.page-container {
  padding: 2rem;
  display: flex;
  flex-direction: column;
  gap: 1rem;
}

.page-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
}

.subtitle {
  margin: 0;
  color: #6c757d;
}

.content-card {
  flex: 1;
}

.form-dialog {
  min-width: 540px;
}

.form-dialog.large {
  min-width: 760px;
}

.dialog-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
}

.form-grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(220px, 1fr));
  gap: 1rem;
}

.parameters-section {
  margin-top: 1.5rem;
}

.section-header {
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
  gap: 0.5rem;
}

.parameter-grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(220px, 1fr));
  gap: 1rem;
}

.parameter-row {
  display: contents;
}

.parameter-row > *:nth-child(3) {
  align-self: center;
}

.tag-list {
  display: flex;
  flex-wrap: wrap;
  gap: 0.25rem;
}
</style>
