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
          :label="isEditMode ? 'Save Changes' : 'Create Account'" 
          :loading="isSaving"
          @click="handleSave"
        />
      </div>
    </div>

    <q-banner v-if="loadError" dense class="bg-red-1 text-negative q-mb-md">
      {{ loadError }}
    </q-banner>

    <q-banner v-if="!fuseStore.canModify" dense class="bg-orange-1 text-orange-9 q-mb-md">
      You do not have permission to {{ isEditMode ? 'edit' : 'create' }} accounts.
    </q-banner>

    <q-card v-if="fuseStore.canModify && !loadError" class="content-card">
      <q-card-section>
        <div class="text-h6 q-mb-md">Account Details</div>
        <AccountForm
          v-model="form"
          :target-kind-options="targetKindOptions"
          :target-options="targetOptions"
          :auth-kind-options="authKindOptions"
          :tag-options="tagOptions"
        />
      </q-card-section>

      <q-separator />

      <q-card-section>
        <div class="text-h6 q-mb-md">Grants & Permissions</div>
        <AccountGrantsSection
          :grants="isEditMode ? (account?.grants ?? []) : form.grants"
          :disable-actions="grantMutationPending"
          @add="openGrantDialog()"
          @edit="({ grant }) => openGrantDialog(grant)"
          @delete="({ grant }) => confirmGrantDelete(grant)"
        />
      </q-card-section>

      <q-separator v-if="showSecretOperations" />

      <q-card-section v-if="showSecretOperations">
        <div class="text-h6 q-mb-md">Secret Operations</div>
        <div class="q-gutter-sm">
          <q-btn
            v-if="canRotateSecret"
            flat
            icon="autorenew"
            label="Rotate Secret"
            color="primary"
            @click="openRotateDialog"
          />
          <q-btn
            v-if="canRevealSecret"
            flat
            icon="visibility"
            label="Reveal Secret"
            color="orange"
            @click="openRevealDialog"
          />
        </div>
      </q-card-section>
    </q-card>

    <!-- Grant Dialog -->
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
              :label="editingGrant ? 'Save' : 'Create'"
              :loading="grantDialogLoading"
            />
          </q-card-actions>
        </q-form>
      </q-card>
    </q-dialog>

    <!-- Rotate Secret Dialog -->
    <q-dialog v-model="isRotateDialogOpen" persistent>
      <q-card style="min-width: 400px">
        <q-card-section class="dialog-header">
          <div class="text-h6">Rotate Secret</div>
          <q-btn flat round dense icon="close" @click="closeRotateDialog" />
        </q-card-section>
        <q-separator />
        <q-form @submit.prevent="handleRotateSecret">
          <q-card-section>
            <q-banner dense class="bg-blue-1 text-blue-9 q-mb-md">
              <template #avatar>
                <q-icon name="info" color="blue" />
              </template>
              Generate a new value for this secret. The operation will be audited.
            </q-banner>
            <q-input
              v-model="newSecretValue"
              label="New Secret Value"
              type="password"
              dense
              outlined
              required
            />
          </q-card-section>
          <q-separator />
          <q-card-actions align="right">
            <q-btn flat label="Cancel" @click="closeRotateDialog" />
            <q-btn
              color="primary"
              type="submit"
              label="Rotate"
              :loading="rotateSecretMutation.isPending.value"
            />
          </q-card-actions>
        </q-form>
      </q-card>
    </q-dialog>

    <!-- Reveal Secret Dialog -->
    <q-dialog v-model="isRevealDialogOpen" persistent>
      <q-card style="min-width: 500px">
        <q-card-section class="dialog-header">
          <div class="text-h6">Reveal Secret</div>
          <q-btn flat round dense icon="close" @click="closeRevealDialog" />
        </q-card-section>
        <q-separator />
        <q-card-section>
          <q-banner dense class="bg-red-1 text-negative q-mb-md">
            <template #avatar>
              <q-icon name="warning" color="red" />
            </template>
            <div class="text-weight-bold">Security Warning</div>
            <div class="text-body2">
              You are about to reveal a secret value. This operation:
              <ul class="q-pl-md q-my-sm">
                <li>Will be audited with your username and timestamp</li>
                <li>Should only be done when absolutely necessary</li>
                <li>Exposes sensitive credential information</li>
              </ul>
              Only proceed if you understand the security implications.
            </div>
          </q-banner>

          <q-input
            v-if="revealedSecret"
            v-model="revealedSecret"
            label="Secret Value"
            :type="showRevealedValue ? 'text' : 'password'"
            readonly
            dense
            outlined
          >
            <template #append>
              <q-icon
                :name="showRevealedValue ? 'visibility_off' : 'visibility'"
                class="cursor-pointer"
                @click="showRevealedValue = !showRevealedValue"
              />
              <q-icon
                name="content_copy"
                class="cursor-pointer q-ml-sm"
                @click="copyToClipboard"
              />
            </template>
          </q-input>

          <div v-else class="text-body2 text-grey-7">
            Click "Reveal" to display the secret value.
          </div>
        </q-card-section>
        <q-separator />
        <q-card-actions align="right">
          <q-btn flat label="Close" @click="closeRevealDialog" />
          <q-btn
            v-if="!revealedSecret"
            color="negative"
            label="Reveal"
            :loading="revealSecretMutation.isPending.value"
            @click="handleRevealSecret"
          />
        </q-card-actions>
      </q-card>
    </q-dialog>
  </div>
</template>

<script setup lang="ts">
import { computed, reactive, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useMutation, useQuery, useQueryClient } from '@tanstack/vue-query'
import { Notify, Dialog, copyToClipboard as qCopyToClipboard } from 'quasar'
import {
  Account,
  AuthKind,
  AzureKeyVaultBinding,
  CreateAccount,
  CreateAccountGrant,
  Grant,
  Privilege,
  RotateSecret,
  SecretBinding,
  SecretBindingKind,
  TargetKind,
  UpdateAccount,
  UpdateAccountGrant
} from '../api/client'
import AccountForm from '../components/accounts/AccountForm.vue'
import AccountGrantsSection from '../components/accounts/AccountGrantsSection.vue'
import type {
  AccountFormModel,
  AccountSecretFields,
  KeyValuePair,
  TargetOption,
  SelectOption
} from '../components/accounts/types'
import { useFuseClient } from '../composables/useFuseClient'
import { useFuseStore } from '../stores/FuseStore'
import { useTags } from '../composables/useTags'
import { useApplications } from '../composables/useApplications'
import { useDataStores } from '../composables/useDataStores'
import { useExternalResources } from '../composables/useExternalResources'
import { useEnvironments } from '../composables/useEnvironments'
import { useSecretProviders } from '../composables/useSecretProviders'
import { getErrorMessage } from '../utils/error'
import { hasCapability } from '../utils/secretProviders'

interface GrantForm {
  database: string
  schema: string
  privileges: Privilege[]
}

const route = useRoute()
const router = useRouter()
const client = useFuseClient()
const queryClient = useQueryClient()
const fuseStore = useFuseStore()
const tagsStore = useTags()
const applicationsQuery = useApplications()
const dataStoresQuery = useDataStores()
const externalResourcesQuery = useExternalResources()
const environmentsQuery = useEnvironments()
const secretProvidersQuery = useSecretProviders()

const accountId = computed(() => route.params.id as string | undefined)
const isEditMode = computed(() => !!accountId.value)

const pageTitle = computed(() => (isEditMode.value ? 'Edit Account' : 'Create Account'))
const pageSubtitle = computed(() =>
  isEditMode.value
    ? 'Update account credentials and permissions'
    : 'Create a new account with credentials and grants'
)

const { data: account, error: loadError } = useQuery({
  queryKey: computed(() => ['account', accountId.value]),
  queryFn: () => client.accountGET(accountId.value!),
  enabled: computed(() => !!accountId.value),
  retry: false
})

const tagOptions = computed<TargetOption[]>(() => tagsStore.options.value)
const targetKindOptions: SelectOption<TargetKind>[] = Object.values(TargetKind).map((value) => ({ label: value, value }))
const authKindOptions: SelectOption<AuthKind>[] = Object.values(AuthKind).map((value) => ({ label: value, value }))
const privilegeOptions = Object.values(Privilege).map((value) => ({ label: value, value }))

const emptySecretFields = (): AccountSecretFields => ({
  providerId: null,
  secretName: null,
  plainReference: ''
})

const emptyAccountForm = (): AccountFormModel => ({
  targetKind: TargetKind.Application,
  targetId: null,
  authKind: AuthKind.None,
  userName: '',
  secret: emptySecretFields(),
  parameters: [],
  tagIds: [],
  grants: []
})

const form = reactive<AccountFormModel>(emptyAccountForm())

const targetOptions = computed<TargetOption[]>(() => {
  const kind = form.targetKind ?? TargetKind.Application
  if (kind === TargetKind.Application) {
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
  }
  if (kind === TargetKind.DataStore) {
    return (dataStoresQuery.data.value ?? [])
      .filter((item) => !!item.id)
      .map((item) => ({ label: item.name ?? item.id!, value: item.id! }))
  }
  return (externalResourcesQuery.data.value ?? [])
    .filter((item) => !!item.id)
    .map((item) => ({ label: item.name ?? item.id!, value: item.id! }))
})

// Grant management
const isGrantDialogOpen = ref(false)
const editingGrant = ref<Grant | null>(null)
const grantForm = reactive<GrantForm>({ database: '', schema: '', privileges: [] })

const grantDialogTitle = computed(() => (editingGrant.value ? 'Edit Grant' : 'Add Grant'))

// Secret operations
const selectedProvider = computed(() =>
  secretProvidersQuery.data.value?.find((p) => p.id === form.secret.providerId) ?? null
)

const showSecretOperations = computed(
  () => isEditMode.value && form.secret.providerId && form.secret.secretName
)

const canRotateSecret = computed(
  () => showSecretOperations.value && hasCapability(selectedProvider.value, 'Rotate') && fuseStore.userRole === 'Admin'
)

const canRevealSecret = computed(
  () => showSecretOperations.value && hasCapability(selectedProvider.value, 'Read') && fuseStore.userRole === 'Admin'
)

const isRotateDialogOpen = ref(false)
const newSecretValue = ref('')

const isRevealDialogOpen = ref(false)
const revealedSecret = ref('')
const showRevealedValue = ref(false)

// Initialize form from account data
watch(
  account,
  (acc) => {
    if (acc) {
      Object.assign(form, {
        targetKind: acc.targetKind ?? TargetKind.Application,
        targetId: acc.targetId ?? null,
        authKind: acc.authKind ?? AuthKind.None,
        userName: acc.userName ?? '',
        secret: mapSecretBindingToForm(acc),
        parameters: convertParametersToPairs(acc.parameters),
        tagIds: [...(acc.tagIds ?? [])],
        grants: (acc.grants ?? []).map(cloneGrant)
      })
      ensureTarget()
    }
  },
  { immediate: true }
)

watch(
  () => form.targetKind,
  () => ensureTarget()
)

watch(
  () => [applicationsQuery.data.value, dataStoresQuery.data.value, externalResourcesQuery.data.value, environmentsQuery.data.value],
  () => ensureTarget()
)

function ensureTarget() {
  const options = targetOptions.value
  if (!form.targetId || !options.some((option) => option.value === form.targetId)) {
    form.targetId = options[0]?.value ?? null
  }
}

function convertParametersToPairs(parameters?: { [key: string]: string }): KeyValuePair[] {
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

function mapSecretBindingToForm(account: Account | null): AccountSecretFields {
  if (!account?.secretBinding) {
    return {
      providerId: null,
      secretName: null,
      plainReference: account?.secretRef ?? ''
    }
  }

  const binding = account.secretBinding
  if (binding.kind === SecretBindingKind.AzureKeyVault) {
    return {
      providerId: binding.azureKeyVault?.providerId ?? null,
      secretName: binding.azureKeyVault?.secretName ?? null,
      plainReference: ''
    }
  }

  if (binding.kind === SecretBindingKind.PlainReference) {
    return {
      providerId: null,
      secretName: null,
      plainReference: binding.plainReference ?? account.secretRef ?? ''
    }
  }

  return emptySecretFields()
}

function buildSecretBindingPayload(secret: AccountSecretFields) {
  if (secret.providerId && secret.secretName) {
    return Object.assign(new SecretBinding(), {
      kind: SecretBindingKind.AzureKeyVault,
      azureKeyVault: Object.assign(new AzureKeyVaultBinding(), {
        providerId: secret.providerId,
        secretName: secret.secretName
      })
    })
  }

  if (!secret.plainReference?.trim()) {
    return Object.assign(new SecretBinding(), {
      kind: SecretBindingKind.None
    })
  }

  return Object.assign(new SecretBinding(), {
    kind: SecretBindingKind.PlainReference,
    plainReference: secret.plainReference.trim()
  })
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

// Mutations
const createMutation = useMutation({
  mutationFn: (payload: CreateAccount) => client.accountPOST(payload),
  onSuccess: () => {
    queryClient.invalidateQueries({ queryKey: ['accounts'] })
    Notify.create({ type: 'positive', message: 'Account created' })
    router.push('/accounts')
  },
  onError: (err) => {
    Notify.create({ type: 'negative', message: getErrorMessage(err, 'Unable to create account') })
  }
})

const updateMutation = useMutation({
  mutationFn: ({ id, payload }: { id: string; payload: UpdateAccount }) => client.accountPUT(id, payload),
  onSuccess: () => {
    queryClient.invalidateQueries({ queryKey: ['accounts'] })
    queryClient.invalidateQueries({ queryKey: ['account', accountId.value] })
    Notify.create({ type: 'positive', message: 'Account updated' })
    router.push('/accounts')
  },
  onError: (err) => {
    Notify.create({ type: 'negative', message: getErrorMessage(err, 'Unable to update account') })
  }
})

const createGrantMutation = useMutation({
  mutationFn: ({ accountId, payload }: { accountId: string; payload: CreateAccountGrant }) =>
    client.grantPOST(accountId, payload),
  onSuccess: () => {
    queryClient.invalidateQueries({ queryKey: ['accounts'] })
    queryClient.invalidateQueries({ queryKey: ['account', accountId.value] })
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
    queryClient.invalidateQueries({ queryKey: ['account', accountId.value] })
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
    queryClient.invalidateQueries({ queryKey: ['account', accountId.value] })
    Notify.create({ type: 'positive', message: 'Grant removed' })
  },
  onError: (err) => {
    Notify.create({ type: 'negative', message: getErrorMessage(err, 'Unable to delete grant') })
  }
})

const rotateSecretMutation = useMutation({
  mutationFn: ({ providerId, secretName, newValue }: { providerId: string; secretName: string; newValue: string }) => {
    const payload = Object.assign(new RotateSecret(), {
      newSecretValue: newValue
    })
    return client.rotate(providerId, secretName, payload)
  },
  onSuccess: () => {
    Notify.create({ type: 'positive', message: 'Secret rotated successfully' })
    closeRotateDialog()
  },
  onError: (err) => {
    Notify.create({ type: 'negative', message: getErrorMessage(err, 'Unable to rotate secret') })
  }
})

const revealSecretMutation = useMutation({
  mutationFn: ({ providerId, secretName }: { providerId: string; secretName: string }) =>
    client.reveal(providerId, secretName, undefined),
  onSuccess: (response) => {
    revealedSecret.value = response.value ?? ''
  },
  onError: (err) => {
    Notify.create({ type: 'negative', message: getErrorMessage(err, 'Unable to reveal secret') })
  }
})

const grantMutationPending = computed(
  () => createGrantMutation.isPending.value || updateGrantMutation.isPending.value
)

const grantDialogLoading = computed(() => grantMutationPending.value)

const isSaving = computed(() => createMutation.isPending.value || updateMutation.isPending.value)

function handleCancel() {
  router.push('/accounts')
}

function handleSave() {
  if (form.secret.providerId && !form.secret.secretName) {
    Notify.create({ type: 'warning', message: 'Select a secret name before saving.' })
    return
  }

  if (isEditMode.value && accountId.value) {
    const payload = Object.assign(new UpdateAccount(), {
      targetKind: form.targetKind,
      targetId: form.targetId ?? undefined,
      authKind: form.authKind,
      userName: form.userName || undefined,
      secretBinding: buildSecretBindingPayload(form.secret),
      parameters: buildParameters(form.parameters),
      grants: (account.value?.grants ?? []).map(buildGrantPayload),
      tagIds: form.tagIds.length ? [...form.tagIds] : undefined
    })
    updateMutation.mutate({ id: accountId.value, payload })
  } else {
    const payload = Object.assign(new CreateAccount(), {
      targetKind: form.targetKind,
      targetId: form.targetId ?? undefined,
      authKind: form.authKind,
      userName: form.userName || undefined,
      secretBinding: buildSecretBindingPayload(form.secret),
      parameters: buildParameters(form.parameters),
      grants: form.grants.map(buildGrantPayload),
      tagIds: form.tagIds.length ? [...form.tagIds] : undefined
    })
    createMutation.mutate(payload)
  }
}

function openGrantDialog(grant?: Grant) {
  if (!isEditMode.value && !grant) {
    Notify.create({ type: 'warning', message: 'Save the account first before adding grants' })
    return
  }

  if (!accountId.value && !grant) {
    Notify.create({ type: 'warning', message: 'Account ID is required' })
    return
  }

  editingGrant.value = grant ?? null
  if (grant) {
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

function closeGrantDialog() {
  isGrantDialogOpen.value = false
  editingGrant.value = null
}

function submitGrant() {
  if (!accountId.value) return

  if (editingGrant.value?.id) {
    const payload = Object.assign(new UpdateAccountGrant(), {
      database: grantForm.database || undefined,
      schema: grantForm.schema || undefined,
      privileges: grantForm.privileges.length ? [...grantForm.privileges] : undefined
    })
    updateGrantMutation.mutate({ accountId: accountId.value, grantId: editingGrant.value.id, payload })
  } else {
    const payload = Object.assign(new CreateAccountGrant(), {
      accountId: accountId.value,
      database: grantForm.database || undefined,
      schema: grantForm.schema || undefined,
      privileges: grantForm.privileges.length ? [...grantForm.privileges] : undefined
    })
    createGrantMutation.mutate({ accountId: accountId.value, payload })
  }
}

function confirmGrantDelete(grant: Grant) {
  if (!accountId.value || !grant.id) return
  Dialog.create({
    title: 'Delete grant',
    message: `Delete grant for "${grant.database ?? 'this database'}"?`,
    cancel: true,
    persistent: true
  }).onOk(() => deleteGrantMutation.mutate({ accountId: accountId.value!, grantId: grant.id! }))
}

function openRotateDialog() {
  newSecretValue.value = ''
  isRotateDialogOpen.value = true
}

function closeRotateDialog() {
  isRotateDialogOpen.value = false
  newSecretValue.value = ''
}

function handleRotateSecret() {
  if (!form.secret.providerId || !form.secret.secretName || !newSecretValue.value) return
  rotateSecretMutation.mutate({
    providerId: form.secret.providerId,
    secretName: form.secret.secretName,
    newValue: newSecretValue.value
  })
}

function openRevealDialog() {
  revealedSecret.value = ''
  showRevealedValue.value = false
  isRevealDialogOpen.value = true
}

function closeRevealDialog() {
  isRevealDialogOpen.value = false
  revealedSecret.value = ''
  showRevealedValue.value = false
}

function handleRevealSecret() {
  if (!form.secret.providerId || !form.secret.secretName) return
  revealSecretMutation.mutate({
    providerId: form.secret.providerId,
    secretName: form.secret.secretName
  })
}

function copyToClipboard() {
  qCopyToClipboard(revealedSecret.value)
    .then(() => {
      Notify.create({ type: 'positive', message: 'Secret copied to clipboard' })
    })
    .catch(() => {
      Notify.create({ type: 'negative', message: 'Failed to copy to clipboard' })
    })
}
</script>

<style scoped>
@import '../styles/pages.css';

.form-grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(220px, 1fr));
  gap: 1rem;
}
</style>
