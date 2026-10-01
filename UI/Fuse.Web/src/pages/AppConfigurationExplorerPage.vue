<template>
  <div class="page-container">
    <div class="page-header">
      <div style="display: flex; align-items: center; gap: 1rem;">
        <q-btn aria-label="Go back" flat round dense icon="arrow_back" @click="router.push({ name: 'secretProviders' })" />
        <div>
          <h1>App Configuration Explorer</h1>
          <p class="subtitle">
            <span v-if="provider">{{ provider.name }} — {{ provider.vaultUri }}</span>
            <span v-else>Loading integration…</span>
          </p>
        </div>
      </div>
      <q-btn
        v-if="isAppConfigurationProvider"
        color="primary"
        label="Add Entry"
        icon="add"
        :disable="!fuseStore.hasPermission(Permission.AppConfigCreate)"
        @click="openCreateDialog"
      />
    </div>

    <q-banner v-if="errorMessage" dense class="bg-red-1 text-negative q-mb-md">
      <template #avatar><q-icon name="error" color="negative" /></template>
      {{ errorMessage }}
      <template #action>
        <q-btn flat label="Retry" @click="refetch()" />
      </template>
    </q-banner>

    <q-banner v-if="provider && !isAppConfigurationProvider" dense class="bg-orange-1 text-orange-9 q-mb-md">
      <template #avatar><q-icon name="warning" color="orange" /></template>
      This integration points to Azure Key Vault. Use the Vault Explorer for this endpoint.
    </q-banner>

    <q-card v-if="isAppConfigurationProvider" class="content-card">
      <q-card-section class="row q-col-gutter-md">
        <div class="col-12 col-md-4">
          <q-input v-model="keySearch" dense outlined clearable label="Search key" placeholder="Shared:ApiUrl">
            <template #prepend><q-icon name="search" /></template>
          </q-input>
        </div>
        <div class="col-12 col-md-4">
          <q-input v-model="keyPrefix" dense outlined clearable label="Key prefix/section" placeholder="Shared:" />
        </div>
        <div class="col-12 col-md-4">
          <q-input v-model="label" dense outlined clearable label="Label filter" placeholder="prod" />
        </div>
      </q-card-section>

      <q-table
        flat
        bordered
        :rows="entries ?? []"
        :columns="columns"
        row-key="key"
        :loading="isLoading"
        :pagination="{ rowsPerPage: 15 }"
      >
        <template #body-cell-value="props">
          <q-td :props="props">
            <template v-if="props.row.isKeyVaultReference">
              <q-badge color="warning" text-color="black" label="Key Vault reference" />
              <div class="text-caption text-grey-8 q-mt-xs">
                {{ props.row.keyVaultReferenceUri || 'Reference URI unavailable' }}
              </div>
            </template>
            <span v-else :title="props.row.value || '—'">{{ truncate(props.row.value) }}</span>
          </q-td>
        </template>
        <template #body-cell-label="props">
          <q-td :props="props">{{ props.row.label || '—' }}</q-td>
        </template>
        <template #body-cell-contentType="props">
          <q-td :props="props">{{ props.row.contentType || '—' }}</q-td>
        </template>
        <template #body-cell-lastModified="props">
          <q-td :props="props">{{ formatDate(props.row.lastModified) }}</q-td>
        </template>
        <template #body-cell-isLocked="props">
          <q-td :props="props">
            <q-badge :color="props.row.isLocked ? 'negative' : 'positive'" :label="props.row.isLocked ? 'Locked' : 'Unlocked'" />
          </q-td>
        </template>
        <template #body-cell-actions="props">
          <q-td :props="props" class="text-right">
            <q-btn aria-label="Reveal secret"
              v-if="props.row.isKeyVaultReference && canRevealReferencedSecret"
              flat
              dense
              round
              icon="visibility"
              color="secondary"
              class="q-mr-xs"
              @click="openRevealReferenceDialog(props.row)"
            >
              <q-tooltip>Reveal referenced secret value</q-tooltip>
            </q-btn>
            <q-btn aria-label="Edit configuration entry"
              flat
              dense
              round
              icon="edit"
              color="primary"
              :disable="props.row.isLocked || props.row.isKeyVaultReference || !fuseStore.hasPermission(Permission.AppConfigUpdate)"
              @click="openEditDialog(props.row)"
            >
              <q-tooltip v-if="props.row.isLocked">This entry is locked and cannot be edited</q-tooltip>
              <q-tooltip v-else-if="props.row.isKeyVaultReference">Key Vault references cannot be edited directly</q-tooltip>
              <q-tooltip v-else-if="!fuseStore.hasPermission(Permission.AppConfigUpdate)">You do not have permission to edit App Configuration entries</q-tooltip>
              <q-tooltip v-else>Edit entry</q-tooltip>
            </q-btn>
          </q-td>
        </template>
        <template #no-data>
          <div class="q-pa-md text-grey-7">No matching configuration entries found.</div>
        </template>
      </q-table>
    </q-card>

    <!-- Create / Edit dialog -->
    <q-dialog v-model="isFormDialogOpen" persistent>
      <AppConfigurationEntryForm
        :initial-entry="selectedEntry"
        :loading="upsertMutation.isPending.value"
        @submit="handleFormSubmit"
        @cancel="closeFormDialog"
      />
    </q-dialog>

    <q-dialog v-model="isRevealDialogOpen" persistent>
      <q-card class="form-dialog">
        <q-card-section class="dialog-header">
          <div class="text-h6">Referenced Secret Value</div>
          <q-btn aria-label="Close dialog" flat round dense icon="close" @click="closeRevealDialog" />
        </q-card-section>
        <q-separator />
        <q-card-section>
          <div class="text-caption text-grey-7 q-mb-xs">
            {{ revealDialogContext || 'Secret reference' }}
          </div>

          <div v-if="isRevealLoading" class="text-center q-pa-md">
            <q-spinner color="primary" size="2em" />
            <div class="text-grey-7 q-mt-sm">Retrieving referenced secret value…</div>
          </div>

          <div v-else-if="revealError" class="text-negative">
            <q-icon name="error" class="q-mr-xs" />{{ revealError }}
          </div>

          <div v-else-if="revealedValue !== null">
            <q-input
              :model-value="revealedValue"
              readonly
              outlined
              dense
              :type="showRevealedValue ? 'text' : 'password'"
            >
              <template #append>
                <q-btn :aria-label="showRevealedValue ? 'Hide secret' : 'Show secret'"
                  flat
                  round
                  dense
                  :icon="showRevealedValue ? 'visibility_off' : 'visibility'"
                  @click="showRevealedValue = !showRevealedValue"
                />
              </template>
            </q-input>
          </div>
        </q-card-section>
        <q-card-actions align="right">
          <q-btn flat label="Close" @click="closeRevealDialog" />
        </q-card-actions>
      </q-card>
    </q-dialog>
  </div>
</template>

<script setup lang="ts">
import { computed, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { Notify, Dialog } from 'quasar'
import type { QTableColumn } from 'quasar'
import { useFuseClient } from '../composables/useFuseClient'
import { useSecretProviders } from '../composables/useSecretProviders'
import { useAppConfigurationEntries } from '../composables/useAppConfigurationEntries'
import type { AppConfigurationEntry } from '../composables/useAppConfigurationEntries'
import { useUpsertAppConfigurationEntry } from '../composables/useUpsertAppConfigurationEntry'
import { getErrorMessage } from '../utils/error'
import { isAppConfigurationEndpoint } from '../utils/secretProviders'
import { useFuseStore } from '../stores/FuseStore'
import { Permission } from '../permissions'
import AppConfigurationEntryForm from '../components/secretProvider/AppConfigurationEntryForm.vue'

const route = useRoute()
const router = useRouter()
const providerId = computed(() => route.params.id as string)
const client = useFuseClient()
const fuseStore = useFuseStore()
const canRevealReferencedSecret = computed(() =>
  fuseStore.hasPermission(Permission.AzureKeyVaultSecretsReveal)
)

const { data: providers } = useSecretProviders()
const provider = computed(() => providers.value?.find(p => p.id === providerId.value) ?? null)
const isAppConfigurationProvider = computed(() => isAppConfigurationEndpoint(provider.value?.vaultUri))

const keySearch = ref('')
const keyPrefix = ref('')
const label = ref('')

const { data: entries, isLoading, error, refetch } = useAppConfigurationEntries(providerId, {
  keySearch,
  keyPrefix,
  label
})

const errorMessage = computed(() => error.value ? getErrorMessage(error.value) : null)

const isFormDialogOpen = ref(false)
const selectedEntry = ref<AppConfigurationEntry | null>(null)
const upsertMutation = useUpsertAppConfigurationEntry()

const isRevealDialogOpen = ref(false)
const isRevealLoading = ref(false)
const revealError = ref<string | null>(null)
const revealedValue = ref<string | null>(null)
const showRevealedValue = ref(false)
const revealDialogContext = ref<string>('')

function openCreateDialog() {
  selectedEntry.value = null
  isFormDialogOpen.value = true
}

function openEditDialog(entry: AppConfigurationEntry) {
  selectedEntry.value = entry
  isFormDialogOpen.value = true
}

function closeFormDialog() {
  selectedEntry.value = null
  isFormDialogOpen.value = false
}

function closeRevealDialog() {
  isRevealDialogOpen.value = false
  isRevealLoading.value = false
  revealError.value = null
  revealedValue.value = null
  showRevealedValue.value = false
  revealDialogContext.value = ''
}

async function openRevealReferenceDialog(entry: AppConfigurationEntry) {
  if (!entry.key) return

  isRevealDialogOpen.value = true
  isRevealLoading.value = true
  revealError.value = null
  revealedValue.value = null
  showRevealedValue.value = false
  revealDialogContext.value = `${entry.key}${entry.label ? ` [${entry.label}]` : ''}`

  try {
    const response = await client.appConfigurationRevealReferencedSecret(
      providerId.value,
      entry.key,
      entry.label ?? undefined
    )
    revealedValue.value = response.value ?? ''
  } catch (err) {
    revealError.value = getErrorMessage(err, 'Unable to reveal referenced secret value')
  } finally {
    isRevealLoading.value = false
  }
}

function escapeHtml(str: string): string {
  return str
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/"/g, '&quot;')
    .replace(/'/g, '&#039;')
}

function truncate(value?: string | null, maxLength = 100): string {
  if (!value) return '—'
  return value.length > maxLength ? `${value.slice(0, maxLength)}…` : value
}

function handleFormSubmit(values: { key: string; label: string; value: string }) {
  const isCreate = selectedEntry.value === null

  // Show confirmation dialog with before/after preview.
  // All user-controlled values are HTML-escaped before interpolation to prevent XSS.
  const oldValue = selectedEntry.value?.value ?? null
  const safeKey = escapeHtml(values.key)
  const safeLabel = values.label ? escapeHtml(values.label) : ''
  const safeOldValue = oldValue !== null ? escapeHtml(oldValue) : '(empty)'
  const safeNewValue = escapeHtml(values.value)
  const labelSuffix = safeLabel ? ` [${safeLabel}]` : ''

  const confirmMessage = isCreate
    ? `Create new entry <strong>${safeKey}</strong>${labelSuffix}?`
    : `Update <strong>${safeKey}</strong>${labelSuffix}?<br>
       <div class="q-mt-sm text-caption">
         <div><strong>Current value:</strong> <code>${safeOldValue}</code></div>
         <div><strong>New value:</strong> <code>${safeNewValue}</code></div>
       </div>`

  Dialog.create({
    title: isCreate ? 'Confirm Create' : 'Confirm Update',
    message: confirmMessage,
    html: true,
    cancel: true,
    persistent: true
  }).onOk(() => {
    upsertMutation.mutate(
      {
        providerId: providerId.value,
        key: values.key,
        label: values.label || null,
        value: values.value,
        contentType: selectedEntry.value?.contentType ?? null
      },
      {
        onSuccess: () => {
          Notify.create({ type: 'positive', message: isCreate ? 'Entry created successfully' : 'Entry updated successfully' })
          closeFormDialog()
        },
        onError: (err) => {
          Notify.create({ type: 'negative', message: getErrorMessage(err, 'Failed to save App Configuration entry') })
        }
      }
    )
  })
}

const columns: QTableColumn[] = [
  { name: 'key', label: 'Key', field: 'key', align: 'left', sortable: true },
  { name: 'value', label: 'Value / Type', field: 'value', align: 'left' },
  { name: 'label', label: 'Label', field: 'label', align: 'left', sortable: true },
  { name: 'contentType', label: 'Content Type', field: 'contentType', align: 'left' },
  { name: 'lastModified', label: 'Last Modified', field: 'lastModified', align: 'left', sortable: true },
  { name: 'isLocked', label: 'Status', field: 'isLocked', align: 'left' },
  { name: 'actions', label: '', field: (row: AppConfigurationEntry) => row.key, align: 'right' }
]

function formatDate(value?: string | null): string {
  if (!value) return '—'
  const parsed = new Date(value)
  return Number.isNaN(parsed.getTime()) ? value : parsed.toLocaleString()
}
</script>

<style scoped>
@import '../styles/pages.css';
</style>
