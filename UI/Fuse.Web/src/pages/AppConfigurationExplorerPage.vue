<template>
  <div class="page-container">
    <div class="page-header">
      <div style="display: flex; align-items: center; gap: 1rem;">
        <q-btn flat round dense icon="arrow_back" @click="router.push({ name: 'secretProviders' })" />
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
            <span v-else>{{ props.row.value ?? '—' }}</span>
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
            <q-btn
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
  </div>
</template>

<script setup lang="ts">
import { computed, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { Notify, Dialog } from 'quasar'
import type { QTableColumn } from 'quasar'
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
const fuseStore = useFuseStore()

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

function handleFormSubmit(values: { key: string; label: string; value: string }) {
  const isCreate = selectedEntry.value === null

  // Show confirmation dialog with before/after preview
  const oldValue = selectedEntry.value?.value ?? null
  const confirmMessage = isCreate
    ? `Create new entry <strong>${values.key}</strong>${values.label ? ` [${values.label}]` : ''}?`
    : `Update <strong>${values.key}</strong>${values.label ? ` [${values.label}]` : ''}?<br>
       <div class="q-mt-sm text-caption">
         <div><strong>Current value:</strong> <code>${oldValue ?? '(empty)'}</code></div>
         <div><strong>New value:</strong> <code>${values.value}</code></div>
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
