<template>
  <div class="page-container">
    <div class="page-header">
      <div style="display: flex; align-items: center; gap: 1rem;">
        <q-btn flat round dense icon="arrow_back" @click="router.push({ name: 'secretProviders' })" />
        <div>
          <h1>Vault Explorer</h1>
          <p class="subtitle">
            <span v-if="provider">{{ provider.name }} — {{ provider.vaultUri }}</span>
            <span v-else>Loading provider…</span>
          </p>
        </div>
      </div>
      <q-btn
        v-if="canCreateSecret"
        color="primary"
        label="Create Secret"
        icon="add"
        @click="openCreateDialog"
      />
    </div>

    <q-banner v-if="!fuseStore.canRead" dense class="bg-orange-1 text-orange-9 q-mb-md">
      You do not have permission to view this vault. Please log in with appropriate credentials.
    </q-banner>

    <q-banner v-if="secretsError" dense class="bg-red-1 text-negative q-mb-md">
      <template #avatar><q-icon name="error" color="negative" /></template>
      {{ secretsError }}
      <template #action>
        <q-btn flat label="Retry" @click="() => refetch()" />
      </template>
    </q-banner>

    <q-banner v-if="provider && !hasCheckCapability" dense class="bg-orange-1 text-orange-9 q-mb-md">
      <template #avatar><q-icon name="warning" color="orange" /></template>
      This secret provider does not have the <strong>Check</strong> capability enabled. Secret listing is unavailable.
    </q-banner>

    <q-card v-if="fuseStore.canRead && hasCheckCapability" class="content-card">
      <q-card-section>
        <q-input
          v-model="searchQuery"
          dense
          outlined
          placeholder="Search secrets by name…"
          clearable
        >
          <template #prepend>
            <q-icon name="search" />
          </template>
        </q-input>
      </q-card-section>

      <q-table
        flat
        bordered
        :rows="filteredSecrets"
        :columns="columns"
        row-key="name"
        :loading="isLoading"
        :pagination="pagination"
      >
        <template #body-cell-enabled="props">
          <q-td :props="props">
            <q-badge
              :color="props.row.enabled ? 'positive' : 'grey'"
              :label="props.row.enabled ? 'Enabled' : 'Disabled'"
            />
          </q-td>
        </template>

        <template #body-cell-updatedOn="props">
          <q-td :props="props">
            <span v-if="props.row.updatedOn">{{ formatDate(props.row.updatedOn) }}</span>
            <span v-else class="text-grey">—</span>
          </q-td>
        </template>

        <template #body-cell-contentType="props">
          <q-td :props="props">
            <span v-if="props.row.contentType">{{ props.row.contentType }}</span>
            <span v-else class="text-grey">—</span>
          </q-td>
        </template>

        <template #body-cell-actions="props">
          <q-td :props="props" class="text-right">
            <q-btn
              v-if="canRevealSecret"
              flat
              dense
              round
              icon="visibility"
              color="secondary"
              @click="openRevealDialog(props.row)"
            >
              <q-tooltip>Reveal Value</q-tooltip>
            </q-btn>
            <q-btn
              v-if="canUpdateSecret"
              flat
              dense
              round
              icon="edit"
              color="primary"
              class="q-ml-xs"
              @click="openUpdateDialog(props.row)"
            >
              <q-tooltip>Update Value</q-tooltip>
            </q-btn>
          </q-td>
        </template>

        <template #no-data>
          <div class="q-pa-md text-grey-7">
            <span v-if="searchQuery">No secrets match "{{ searchQuery }}".</span>
            <span v-else>No secrets found in this vault.</span>
          </div>
        </template>
      </q-table>
    </q-card>

    <!-- Reveal Dialog -->
    <q-dialog v-model="isRevealDialogOpen" persistent>
      <q-card class="form-dialog">
        <q-card-section class="dialog-header">
          <div class="text-h6">Secret Value — {{ selectedSecret?.name }}</div>
          <q-btn flat round dense icon="close" @click="closeRevealDialog" />
        </q-card-section>
        <q-separator />
        <q-card-section>
          <div v-if="revealLoading" class="text-center q-pa-md">
            <q-spinner color="primary" size="2em" />
            <div class="text-grey-7 q-mt-sm">Retrieving secret value…</div>
          </div>
          <div v-else-if="revealError" class="text-negative">
            <q-icon name="error" class="q-mr-xs" />{{ revealError }}
          </div>
          <div v-else-if="revealedValue !== null">
            <div class="text-caption text-grey-7 q-mb-xs">Value</div>
            <q-input
              :model-value="revealedValue"
              readonly
              outlined
              dense
              :type="showRevealedValue ? 'text' : 'password'"
            >
              <template #append>
                <q-btn
                  flat
                  round
                  dense
                  :icon="showRevealedValue ? 'visibility_off' : 'visibility'"
                  @click="showRevealedValue = !showRevealedValue"
                />
                <q-btn
                  flat
                  round
                  dense
                  icon="content_copy"
                  @click="copyToClipboard(revealedValue ?? '')"
                >
                  <q-tooltip>Copy to clipboard</q-tooltip>
                </q-btn>
              </template>
            </q-input>
          </div>
        </q-card-section>
        <q-card-actions align="right">
          <q-btn flat label="Close" @click="closeRevealDialog" />
        </q-card-actions>
      </q-card>
    </q-dialog>

    <!-- Create Secret Dialog -->
    <q-dialog v-model="isCreateDialogOpen" persistent>
      <q-card class="form-dialog">
        <q-card-section class="dialog-header">
          <div class="text-h6">Create Secret</div>
          <q-btn flat round dense icon="close" @click="closeCreateDialog" />
        </q-card-section>
        <q-separator />
        <q-form @submit.prevent="handleCreateSubmit">
          <q-card-section class="q-gutter-md">
            <q-input
              v-model="createForm.secretName"
              label="Secret Name*"
              dense
              outlined
              required
              :rules="[val => !!val || 'Secret name is required']"
            />
            <q-input
              v-model="createForm.secretValue"
              label="Secret Value*"
              dense
              outlined
              required
              type="password"
              :rules="[val => !!val || 'Secret value is required']"
            />
          </q-card-section>
          <q-separator />
          <q-card-actions align="right">
            <q-btn flat label="Cancel" @click="closeCreateDialog" />
            <q-btn
              color="primary"
              type="submit"
              label="Create"
              :loading="createLoading"
            />
          </q-card-actions>
        </q-form>
      </q-card>
    </q-dialog>

    <!-- Update Secret Dialog -->
    <q-dialog v-model="isUpdateDialogOpen" persistent>
      <q-card class="form-dialog">
        <q-card-section class="dialog-header">
          <div class="text-h6">Update Secret — {{ selectedSecret?.name }}</div>
          <q-btn flat round dense icon="close" @click="closeUpdateDialog" />
        </q-card-section>
        <q-separator />
        <q-form @submit.prevent="handleUpdateSubmit">
          <q-card-section class="q-gutter-md">
            <q-banner dense class="bg-blue-1 text-blue-9">
              <template #avatar><q-icon name="info" color="blue" /></template>
              Updating a secret creates a new version in Azure Key Vault. The previous value is retained as an older version.
            </q-banner>
            <q-input
              v-model="updateForm.newValue"
              label="New Secret Value*"
              dense
              outlined
              required
              type="password"
              :rules="[val => !!val || 'New secret value is required']"
            />
          </q-card-section>
          <q-separator />
          <q-card-actions align="right">
            <q-btn flat label="Cancel" @click="closeUpdateDialog" />
            <q-btn
              color="primary"
              type="submit"
              label="Update"
              :loading="updateLoading"
            />
          </q-card-actions>
        </q-form>
      </q-card>
    </q-dialog>
  </div>
</template>

<script setup lang="ts">
import { computed, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useQueryClient } from '@tanstack/vue-query'
import { Notify, copyToClipboard as quasarCopy } from 'quasar'
import type { QTableColumn } from 'quasar'
import { CreateSecret, RotateSecret, SecretMetadataResponse } from 'api/client'
import { Permission } from 'permissions'
import { useFuseClient } from '../composables/useFuseClient'
import { useFuseStore } from '../stores/FuseStore'
import { useSecretProviders } from '../composables/useSecretProviders'
import { useSecretProviderSecrets } from '../composables/useSecretProviderSecrets'
import { getErrorMessage } from '../utils/error'

const route = useRoute()
const router = useRouter()
const client = useFuseClient()
const fuseStore = useFuseStore()
const queryClient = useQueryClient()

// Matches SecretProviderCapabilities flags on the backend
const Capability = { Check: 1, Create: 2, Rotate: 4, Read: 8 } as const

const providerId = computed(() => route.params.id as string)

const { data: providers } = useSecretProviders()
const provider = computed(() => providers.value?.find(p => p.id === providerId.value) ?? null)

const { data: secrets, isLoading, error, refetch } = useSecretProviderSecrets(providerId)

const secretsError = computed(() => error.value ? getErrorMessage(error.value) : null)

const hasCheckCapability = computed(() => {
  if (!provider.value) return true // don't show warning before provider loads
  return hasCapability(Capability.Check)
})

const canCreateSecret = computed(() =>
  fuseStore.hasPermission(Permission.AzureKeyVaultSecretsCreate) &&
  hasCapability(Capability.Create)
)

const canRevealSecret = computed(() =>
  fuseStore.hasPermission(Permission.AzureKeyVaultSecretsReveal) &&
  hasCapability(Capability.Read)
)

const canUpdateSecret = computed(() =>
  fuseStore.hasPermission(Permission.AzureKeyVaultSecretsRotate) &&
  hasCapability(Capability.Rotate)
)

function hasCapability(flag: number): boolean {
  if (!provider.value) return false
  const caps = provider.value.capabilities as unknown as number
  return typeof caps === 'number' ? (caps & flag) !== 0 : false
}

const searchQuery = ref('')

const filteredSecrets = computed(() => {
  const all = secrets.value ?? []
  const query = searchQuery.value.trim().toLowerCase()
  if (!query) return all
  return all.filter(s => s.name?.toLowerCase().includes(query))
})

const pagination = { rowsPerPage: 15 }

const columns: QTableColumn<SecretMetadataResponse>[] = [
  { name: 'name', label: 'Name', field: 'name', align: 'left', sortable: true },
  { name: 'enabled', label: 'Status', field: 'enabled', align: 'left' },
  { name: 'updatedOn', label: 'Last Updated', field: 'updatedOn', align: 'left', sortable: true },
  { name: 'contentType', label: 'Content Type', field: 'contentType', align: 'left' },
  { name: 'actions', label: '', field: (row) => row.name, align: 'right' }
]

function formatDate(value: Date | string | undefined): string {
  if (!value) return '—'
  return new Date(value).toLocaleString()
}

// Reveal dialog
const isRevealDialogOpen = ref(false)
const selectedSecret = ref<SecretMetadataResponse | null>(null)
const revealedValue = ref<string | null>(null)
const revealLoading = ref(false)
const revealError = ref<string | null>(null)
const showRevealedValue = ref(false)

async function openRevealDialog(secret: SecretMetadataResponse) {
  if (!secret.name) return
  selectedSecret.value = secret
  revealedValue.value = null
  revealError.value = null
  showRevealedValue.value = false
  isRevealDialogOpen.value = true

  revealLoading.value = true
  try {
    const result = await client.reveal(providerId.value, secret.name, undefined)
    revealedValue.value = result.value ?? null
  } catch (err) {
    revealError.value = getErrorMessage(err, 'Unable to retrieve secret value. You may not have permission.')
  } finally {
    revealLoading.value = false
  }
}

function closeRevealDialog() {
  isRevealDialogOpen.value = false
  selectedSecret.value = null
  revealedValue.value = null
  revealError.value = null
}

function copyToClipboard(value: string) {
  quasarCopy(value).then(() => {
    Notify.create({ type: 'positive', message: 'Copied to clipboard' })
  })
}

// Create dialog
const isCreateDialogOpen = ref(false)
const createForm = ref({ secretName: '', secretValue: '' })
const createLoading = ref(false)

function openCreateDialog() {
  createForm.value = { secretName: '', secretValue: '' }
  isCreateDialogOpen.value = true
}

function closeCreateDialog() {
  isCreateDialogOpen.value = false
}

async function handleCreateSubmit() {
  createLoading.value = true
  try {
    const payload = Object.assign(new CreateSecret(), {
      secretName: createForm.value.secretName,
      secretValue: createForm.value.secretValue
    })
    await client.secrets(providerId.value, payload)
    queryClient.invalidateQueries({ queryKey: ['secret-provider-secrets', providerId.value] })
    Notify.create({ type: 'positive', message: 'Secret created successfully' })
    closeCreateDialog()
  } catch (err) {
    Notify.create({ type: 'negative', message: getErrorMessage(err, 'Unable to create secret') })
  } finally {
    createLoading.value = false
  }
}

// Update dialog
const isUpdateDialogOpen = ref(false)
const updateForm = ref({ newValue: '' })
const updateLoading = ref(false)

function openUpdateDialog(secret: SecretMetadataResponse) {
  if (!secret.name) return
  selectedSecret.value = secret
  updateForm.value = { newValue: '' }
  isUpdateDialogOpen.value = true
}

function closeUpdateDialog() {
  isUpdateDialogOpen.value = false
  selectedSecret.value = null
}

async function handleUpdateSubmit() {
  if (!selectedSecret.value?.name) return
  updateLoading.value = true
  try {
    const payload = Object.assign(new RotateSecret(), {
      newSecretValue: updateForm.value.newValue
    })
    await client.rotate(providerId.value, selectedSecret.value.name, payload)
    queryClient.invalidateQueries({ queryKey: ['secret-provider-secrets', providerId.value] })
    Notify.create({ type: 'positive', message: 'Secret updated successfully' })
    closeUpdateDialog()
  } catch (err) {
    Notify.create({ type: 'negative', message: getErrorMessage(err, 'Unable to update secret') })
  } finally {
    updateLoading.value = false
  }
}
</script>

<style scoped>
@import '../styles/pages.css';
</style>
