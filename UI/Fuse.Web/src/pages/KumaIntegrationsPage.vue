<template>
  <div class="page-container">
    <div class="page-header">
      <div>
        <h1>Kuma Integrations</h1>
        <p class="subtitle">Manage Uptime Kuma integration endpoints and credentials.</p>
      </div>
      <q-btn color="primary" label="Add Integration" icon="add" :disable="!fuseStore.canModify"
        @click="openCreateDialog" />
    </div>

    <q-banner v-if="integrationError" dense class="bg-red-1 text-negative q-mb-md">
      {{ integrationError }}
    </q-banner>

    <q-banner v-if="!fuseStore.canRead" dense class="bg-orange-1 text-orange-9 q-mb-md">
      You do not have permission to view Kuma integrations. Please log in with appropriate credentials.
    </q-banner>

    <q-card v-if="fuseStore.canRead" class="content-card">
      <q-table flat bordered :rows="integrations" :columns="columns" row-key="id" :loading="isLoading"
        :pagination="pagination">
        <template #body-cell-environments="props">
          <q-td :props="props">
            <div v-if="props.row.environmentIds?.length">
              <q-badge v-for="envId in props.row.environmentIds" :key="envId" outline color="primary"
                :label="environmentLookup[envId] ?? envId" />
            </div>
            <span v-else class="text-grey">—</span>
          </q-td>
        </template>
        <template #body-cell-platform="props">
          <q-td :props="props">
            {{ platformLookup[props.row.platformId ?? ''] ?? '—' }}
          </q-td>
        </template>
        <template #body-cell-account="props">
          <q-td :props="props">
            {{ accountLookup[props.row.accountId ?? ''] ?? '—' }}
          </q-td>
        </template>
        <template #body-cell-actions="props">
          <q-td :props="props" class="text-right">
            <q-btn flat dense round icon="edit" color="primary" :disable="!fuseStore.canModify"
              @click="openEditDialog(props.row)" />
            <q-btn flat dense round icon="delete" color="negative" class="q-ml-xs" :disable="!fuseStore.canModify"
              @click="confirmDelete(props.row)" />
          </q-td>
        </template>
        <template #no-data>
          <div class="q-pa-md text-grey-7">No Kuma integrations added.</div>
        </template>
      </q-table>
    </q-card>

    <q-dialog v-model="isFormDialogOpen" persistent>
      <KumaIntegrationForm :mode="selectedIntegration ? 'edit' : 'create'" :initial-value="selectedIntegration"
        :loading="formLoading" @submit="handleFormSubmit" @cancel="closeFormDialog" />
    </q-dialog>
  </div>
</template>

<script setup lang="ts">
import { computed, ref } from 'vue'
import { useMutation, useQueryClient } from '@tanstack/vue-query'
import { Notify, Dialog } from 'quasar'
import type { QTableColumn } from 'quasar'
import { KumaIntegrationResponse, CreateKumaIntegration, UpdateKumaIntegration } from '../api/client'
import { useFuseClient } from '../composables/useFuseClient'
import { useFuseStore } from '../stores/FuseStore'
import { useEnvironments } from '../composables/useEnvironments'
import { usePlatforms } from '../composables/usePlatforms'
import { useAccounts } from '../composables/useAccounts'
import { getErrorMessage } from '../utils/error'
import KumaIntegrationForm from '../components/kuma/KumaIntegrationForm.vue'
import { useKumaIntegrations } from '../composables/useKumaIntegrations'

interface KumaIntegrationFormModel {
  name: string
  environmentIds: string[]
  platformId: string | null
  accountId: string | null
  uri: string
  apiKey: string
}

const client = useFuseClient()
const queryClient = useQueryClient()
const fuseStore = useFuseStore()
const environmentsStore = useEnvironments()
const platformsStore = usePlatforms()
const accountsStore = useAccounts()

const pagination = { rowsPerPage: 10 }

const { data, isLoading, error } = useKumaIntegrations()

const integrations = computed(() => data.value ?? [])
const integrationError = computed(() => (error.value ? getErrorMessage(error.value) : null))

const environmentLookup = environmentsStore.lookup
const platformLookup = platformsStore.lookup
const accountLookup = accountsStore.lookup

const columns: QTableColumn<KumaIntegrationResponse>[] = [
  { name: 'name', label: 'Name', field: 'name', align: 'left', sortable: true },
  { name: 'uri', label: 'URI', field: 'uri', align: 'left' },
  { name: 'environments', label: 'Environments', field: 'environmentIds', align: 'left' },
  { name: 'platform', label: 'Platform', field: 'platformId', align: 'left' },
  { name: 'account', label: 'Account', field: 'accountId', align: 'left' },
  { name: 'actions', label: '', field: (row) => row.id, align: 'right' }
]

const isFormDialogOpen = ref(false)
const selectedIntegration = ref<KumaIntegrationResponse | null>(null)

function openCreateDialog() {
  selectedIntegration.value = null
  isFormDialogOpen.value = true
}

function openEditDialog(integration: KumaIntegrationResponse) {
  if (!integration.id) return
  selectedIntegration.value = integration
  isFormDialogOpen.value = true
}

function closeFormDialog() {
  selectedIntegration.value = null
  isFormDialogOpen.value = false
}

const createMutation = useMutation({
  mutationFn: (payload: CreateKumaIntegration) => client.kumaIntegrationPOST(payload),
  onSuccess: () => {
    queryClient.invalidateQueries({ queryKey: ['kumaIntegrations'] })
    Notify.create({ type: 'positive', message: 'Kuma integration added' })
    closeFormDialog()
  },
  onError: (err) => {
    Notify.create({ type: 'negative', message: getErrorMessage(err, 'Unable to add integration') })
  }
})

const updateMutation = useMutation({
  mutationFn: ({ id, payload }: { id: string; payload: UpdateKumaIntegration }) => client.kumaIntegrationPUT(id, payload),
  onSuccess: () => {
    queryClient.invalidateQueries({ queryKey: ['kumaIntegrations'] })
    Notify.create({ type: 'positive', message: 'Kuma integration updated' })
    closeFormDialog()
  },
  onError: (err) => {
    Notify.create({ type: 'negative', message: getErrorMessage(err, 'Unable to update integration') })
  }
})

const deleteMutation = useMutation({
  mutationFn: (id: string) => client.kumaIntegrationDELETE(id),
  onSuccess: () => {
    queryClient.invalidateQueries({ queryKey: ['kumaIntegrations'] })
    Notify.create({ type: 'positive', message: 'Kuma integration deleted' })
  },
  onError: (err) => {
    Notify.create({ type: 'negative', message: getErrorMessage(err, 'Unable to delete integration') })
  }
})

const formLoading = computed(() =>
  selectedIntegration.value ? updateMutation.isPending.value : createMutation.isPending.value
)

function handleFormSubmit(values: KumaIntegrationFormModel) {
  if (selectedIntegration.value?.id) {
    const payload = Object.assign(new UpdateKumaIntegration(), {
      name: values.name || undefined,
      environmentIds: values.environmentIds.length ? [...values.environmentIds] : undefined,
      platformId: values.platformId || undefined,
      accountId: values.accountId || undefined,
      uri: values.uri || undefined,
      apiKey: values.apiKey || undefined
    })
    updateMutation.mutate({ id: selectedIntegration.value.id, payload })
  } else {
    const payload = Object.assign(new CreateKumaIntegration(), {
      name: values.name || undefined,
      environmentIds: values.environmentIds.length ? [...values.environmentIds] : undefined,
      platformId: values.platformId || undefined,
      accountId: values.accountId || undefined,
      uri: values.uri || undefined,
      apiKey: values.apiKey || undefined
    })
    createMutation.mutate(payload)
  }
}

function confirmDelete(integration: KumaIntegrationResponse) {
  if (!integration.id) return
  Dialog.create({
    title: 'Delete integration',
    message: `Delete "${integration.name ?? 'this integration'}"?`,
    cancel: true,
    persistent: true
  }).onOk(() => deleteMutation.mutate(integration.id!))
}
</script>

<style scoped>
@import '../styles/pages.css';

.form-dialog {
  min-width: 520px;
}
</style>
