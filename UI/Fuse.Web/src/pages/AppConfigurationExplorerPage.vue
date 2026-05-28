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
        <template #no-data>
          <div class="q-pa-md text-grey-7">No matching configuration entries found.</div>
        </template>
      </q-table>
    </q-card>
  </div>
</template>

<script setup lang="ts">
import { computed, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import type { QTableColumn } from 'quasar'
import { useSecretProviders } from '../composables/useSecretProviders'
import { useAppConfigurationEntries } from '../composables/useAppConfigurationEntries'
import { getErrorMessage } from '../utils/error'
import { isAppConfigurationEndpoint } from '../utils/secretProviders'

const route = useRoute()
const router = useRouter()
const providerId = computed(() => route.params.id as string)

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

const columns: QTableColumn[] = [
  { name: 'key', label: 'Key', field: 'key', align: 'left', sortable: true },
  { name: 'value', label: 'Value / Type', field: 'value', align: 'left' },
  { name: 'label', label: 'Label', field: 'label', align: 'left', sortable: true },
  { name: 'contentType', label: 'Content Type', field: 'contentType', align: 'left' },
  { name: 'lastModified', label: 'Last Modified', field: 'lastModified', align: 'left', sortable: true },
  { name: 'isLocked', label: 'Status', field: 'isLocked', align: 'left' }
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
