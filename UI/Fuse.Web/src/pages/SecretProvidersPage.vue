<template>
  <div class="page-container">
    <div class="page-header">
      <div>
        <h1>Azure Integrations</h1>
        <p class="subtitle">Manage Azure Key Vault and Azure App Configuration integrations.</p>
      </div>
      <q-btn 
        color="primary" 
        label="Add Provider" 
        icon="add" 
        :disable="!fuseStore.hasPermission(Permission.AzureKeyVaultConnectionsCreate)"
        @click="openCreateDialog" 
      />
    </div>

    <q-banner v-if="secretProviderError" dense class="bg-red-1 text-negative q-mb-md">
      {{ secretProviderError }}
    </q-banner>

    <q-banner v-if="!fuseStore.canRead" dense class="bg-orange-1 text-orange-9 q-mb-md">
      You do not have permission to view secret providers. Please log in with appropriate credentials.
    </q-banner>

    <q-card v-if="fuseStore.canRead" class="content-card">
      <q-card-section class="section-header">
        <div>
          <div class="text-h6">Azure Integration Manager</div>
          <p class="text-body2 text-grey-7 q-mt-xs q-mb-none">
            Manage shared Azure Client Secret credentials used across key vault providers.
          </p>
        </div>
        <q-btn
          color="primary"
          :label="hasSharedClientSecretCredentials ? 'Update Shared Credentials' : 'Set Shared Credentials'"
          icon="key"
          :disable="!fuseStore.hasPermission(Permission.AzureKeyVaultConnectionsCreate)"
          @click="openAzureManagerDialog"
        />
      </q-card-section>
      <q-separator />
      <q-card-section>
        <q-banner
          v-if="azureManagerError"
          dense
          class="bg-red-1 text-negative q-mb-md"
        >
          {{ azureManagerError }}
        </q-banner>

        <q-skeleton v-if="isAzureManagerLoading" type="QToolbar" />

        <template v-else>
          <div class="row q-col-gutter-md items-center">
            <div class="col-auto">
              <q-chip
                square
                :color="hasSharedClientSecretCredentials ? 'positive' : 'grey-6'"
                text-color="white"
                :label="hasSharedClientSecretCredentials ? 'Shared credentials configured' : 'No shared credentials configured'"
              />
            </div>
            <div class="col text-body2 text-grey-7">
              Providers using Client Secret can omit per-provider credentials and inherit from this shared manager.
            </div>
          </div>
          <div v-if="hasSharedClientSecretCredentials" class="q-mt-md text-body2 text-grey-8">
            <div><strong>Tenant ID:</strong> {{ azureManager?.tenantId ?? 'Not set' }}</div>
            <div><strong>Client ID:</strong> {{ azureManager?.clientId ?? 'Not set' }}</div>
            <div><strong>Last Updated:</strong> {{ formatUpdatedAt(azureManager?.updatedAt) }}</div>
          </div>
        </template>
      </q-card-section>
    </q-card>

    <q-card v-if="fuseStore.canRead" class="content-card">
      <q-card-section>
        <p class="text-body2 text-grey-7">
          Secret providers allow Fuse to securely manage credentials through Azure Key Vault.
          Configure providers with appropriate capabilities (Check, Create, Rotate, Read) based on your security requirements.
        </p>
      </q-card-section>

      <q-table 
        flat 
        bordered 
        :rows="secretProviders" 
        :columns="columns" 
        row-key="id" 
        :loading="isLoading" 
        :pagination="pagination"
      >
        <template #body-cell-capabilities="props">
          <q-td :props="props">
            <div v-if="props.row.capabilities" class="q-gutter-xs">
              <q-badge
                v-for="cap in parseCapabilities(props.row.capabilities)"
                :key="cap"
                outline
                color="primary"
                :label="cap"
              />
            </div>
            <span v-else class="text-grey">—</span>
          </q-td>
        </template>
        <template #body-cell-actions="props">
          <q-td :props="props" class="text-right">
             <q-btn
               flat
               dense
               round
               icon="manage_search"
               color="secondary"
               @click="openExplorer(props.row)"
             >
               <q-tooltip>{{ isAppConfiguration(props.row) ? 'Explore App Configuration' : 'Explore Vault' }}</q-tooltip>
             </q-btn>
            <q-btn 
              flat 
              dense 
              round 
              icon="edit" 
              color="primary" 
              class="q-ml-xs"
              :disable="!fuseStore.hasPermission(Permission.AzureKeyVaultConnectionsCreate)"
              @click="openEditDialog(props.row)" 
            >
              <q-tooltip>Edit Provider</q-tooltip>
            </q-btn>
            <q-btn
              flat
              dense
              round
              icon="delete"
              color="negative"
              class="q-ml-xs"
              :disable="!fuseStore.hasPermission(Permission.AzureKeyVaultConnectionsDelete)"
              @click="confirmDelete(props.row)"
            >
              <q-tooltip>Delete Provider</q-tooltip>
            </q-btn>
          </q-td>
        </template>
        <template #no-data>
          <div class="q-pa-md text-grey-7">
              No integrations configured. Click "Add Provider" to configure Azure Key Vault or App Configuration.
          </div>
        </template>
      </q-table>
    </q-card>

    <q-dialog v-model="isFormDialogOpen" persistent>
      <SecretProviderForm
        :mode="selectedProvider ? 'edit' : 'create'"
        :initial-value="selectedProvider"
        :loading="formLoading"
        :disabled="!fuseStore.hasPermission(Permission.AzureKeyVaultConnectionsCreate)"
        :allow-shared-client-secret-credentials="hasSharedClientSecretCredentials"
        @submit="handleFormSubmit"
        @cancel="closeFormDialog"
      />
    </q-dialog>

    <q-dialog v-model="isAzureManagerDialogOpen" persistent>
      <q-card class="form-dialog">
        <q-card-section class="dialog-header">
          <div class="text-h6">Shared Azure Credentials</div>
          <q-btn flat round dense icon="close" @click="closeAzureManagerDialog" />
        </q-card-section>
        <q-separator />
        <q-form @submit.prevent="submitAzureManagerCredentials">
          <q-card-section class="form-grid">
            <q-input
              v-model="azureManagerForm.tenantId"
              label="Tenant ID*"
              dense
              outlined
              :rules="[val => !!val || 'Tenant ID is required']"
            />
            <q-input
              v-model="azureManagerForm.clientId"
              label="Client ID*"
              dense
              outlined
              :rules="[val => !!val || 'Client ID is required']"
            />
            <q-input
              v-model="azureManagerForm.clientSecret"
              label="Client Secret*"
              type="password"
              dense
              outlined
              :rules="[val => !!val || 'Client Secret is required']"
            />
          </q-card-section>
          <q-separator />
          <q-card-actions align="right">
            <q-btn flat label="Cancel" @click="closeAzureManagerDialog" />
            <q-btn
              color="primary"
              type="submit"
              label="Save Shared Credentials"
              :loading="isUpdatingAzureManager"
              :disable="!fuseStore.hasPermission(Permission.AzureKeyVaultConnectionsCreate)"
            />
          </q-card-actions>
        </q-form>
      </q-card>
    </q-dialog>
  </div>
</template>

<script setup lang="ts">
import { computed, ref } from 'vue'
import { useRouter } from 'vue-router'
import { useMutation, useQueryClient } from '@tanstack/vue-query'
import { Notify, Dialog } from 'quasar'
import type { QTableColumn } from 'quasar'
import { 
  SecretProviderResponse, 
  CreateSecretProvider, 
  UpdateSecretProvider,
  SecretProviderAuthMode,
  SecretProviderCapabilities,
  SecretProviderCredentials
} from 'api/client'
import { Permission } from 'permissions'
import { useFuseClient } from '../composables/useFuseClient'
import { useFuseStore } from '../stores/FuseStore'
import { useSecretProviders } from '../composables/useSecretProviders'
import { useAzureIntegrationManager } from '../composables/useAzureIntegrationManager'
import { getErrorMessage } from '../utils/error'
import { isAppConfigurationEndpoint } from '../utils/secretProviders'
import SecretProviderForm from '../components/secretProvider/SecretProviderForm.vue'

interface SecretProviderFormModel {
  name: string
  vaultUri: string
  authMode: SecretProviderAuthMode | null
  credentials: {
    tenantId: string
    clientId: string
    clientSecret: string
  }
  capabilities: {
    check: boolean
    create: boolean
    rotate: boolean
    read: boolean
  }
}

const client = useFuseClient()
const queryClient = useQueryClient()
const fuseStore = useFuseStore()
const router = useRouter()

const pagination = { rowsPerPage: 10 }

const { data, isLoading, error } = useSecretProviders()
const {
  data: azureManagerData,
  isLoading: isAzureManagerLoading,
  error: azureManagerQueryError,
  updateMutation: updateAzureManagerMutation
} = useAzureIntegrationManager()

const secretProviders = computed(() => data.value ?? [])
const secretProviderError = computed(() => (error.value ? getErrorMessage(error.value) : null))
const azureManager = computed(() => azureManagerData.value)
const azureManagerError = computed(() =>
  azureManagerQueryError.value ? getErrorMessage(azureManagerQueryError.value) : null
)
const hasSharedClientSecretCredentials = computed(() => !!azureManager.value?.hasClientSecretCredentials)

const columns: QTableColumn<SecretProviderResponse>[] = [
  { name: 'name', label: 'Name', field: 'name', align: 'left', sortable: true },
  { name: 'vaultUri', label: 'Endpoint URI', field: 'vaultUri', align: 'left' },
  { name: 'authMode', label: 'Auth Mode', field: 'authMode', align: 'left' },
  { name: 'capabilities', label: 'Capabilities', field: 'capabilities', align: 'left' },
  { name: 'actions', label: '', field: (row) => row.id, align: 'right' }
]

const isFormDialogOpen = ref(false)
const selectedProvider = ref<SecretProviderResponse | null>(null)
const isAzureManagerDialogOpen = ref(false)
const azureManagerForm = ref({
  tenantId: '',
  clientId: '',
  clientSecret: ''
})

function openCreateDialog() {
  selectedProvider.value = null
  isFormDialogOpen.value = true
}

function openEditDialog(provider: SecretProviderResponse) {
  if (!provider.id) return
  selectedProvider.value = provider
  isFormDialogOpen.value = true
}

function closeFormDialog() {
  selectedProvider.value = null
  isFormDialogOpen.value = false
}

function openAzureManagerDialog() {
  azureManagerForm.value = {
    tenantId: azureManager.value?.tenantId ?? '',
    clientId: azureManager.value?.clientId ?? '',
    clientSecret: ''
  }
  isAzureManagerDialogOpen.value = true
}

function closeAzureManagerDialog() {
  isAzureManagerDialogOpen.value = false
}

function formatUpdatedAt(value?: string | null): string {
  if (!value) return 'Not set'

  const parsed = new Date(value)
  if (Number.isNaN(parsed.getTime())) {
    return value
  }

  return parsed.toLocaleString()
}

function parseCapabilities(capabilities?: string | SecretProviderCapabilities): string[] {
  if (!capabilities) return []
  
  if (typeof capabilities === 'string') {
    return capabilities.split(',').map(c => c.trim()).filter(Boolean)
  }
  
  // If it's an enum value
  const caps: string[] = []
  const capValue = capabilities as any
  
  if (typeof capValue === 'number') {
    if (capValue & 1) caps.push('Check')
    if (capValue & 2) caps.push('Create')
    if (capValue & 4) caps.push('Rotate')
    if (capValue & 8) caps.push('Read')
  }
  
  return caps
}

function buildCapabilitiesEnum(caps: { check: boolean; create: boolean; rotate: boolean; read: boolean }): SecretProviderCapabilities {
  // Build flags enum value
  let value = 0
  if (caps.check) value |= 1
  if (caps.create) value |= 2
  if (caps.rotate) value |= 4
  if (caps.read) value |= 8
  return value as unknown as SecretProviderCapabilities
}

const createMutation = useMutation({
  mutationFn: (payload: CreateSecretProvider) => client.secretProviderPOST(payload),
  onSuccess: () => {
    queryClient.invalidateQueries({ queryKey: ['secretProviders'] })
    Notify.create({ type: 'positive', message: 'Secret provider created' })
    closeFormDialog()
  },
  onError: (err) => {
    Notify.create({ type: 'negative', message: getErrorMessage(err, 'Unable to create secret provider') })
  }
})

const updateMutation = useMutation({
  mutationFn: ({ id, payload }: { id: string; payload: UpdateSecretProvider }) => 
    client.secretProviderPUT(id, payload),
  onSuccess: () => {
    queryClient.invalidateQueries({ queryKey: ['secretProviders'] })
    Notify.create({ type: 'positive', message: 'Secret provider updated' })
    closeFormDialog()
  },
  onError: (err) => {
    Notify.create({ type: 'negative', message: getErrorMessage(err, 'Unable to update secret provider') })
  }
})

const deleteMutation = useMutation({
  mutationFn: (id: string) => client.secretProviderDELETE(id),
  onSuccess: () => {
    queryClient.invalidateQueries({ queryKey: ['secretProviders'] })
    Notify.create({ type: 'positive', message: 'Secret provider deleted' })
  },
  onError: (err) => {
    Notify.create({ type: 'negative', message: getErrorMessage(err, 'Unable to delete secret provider') })
  }
})

const formLoading = computed(() =>
  selectedProvider.value ? updateMutation.isPending.value : createMutation.isPending.value
)

const isUpdatingAzureManager = computed(() => updateAzureManagerMutation.isPending.value)

function submitAzureManagerCredentials() {
  updateAzureManagerMutation.mutate(
    {
      credentials: {
        tenantId: azureManagerForm.value.tenantId,
        clientId: azureManagerForm.value.clientId,
        clientSecret: azureManagerForm.value.clientSecret
      }
    },
    {
      onSuccess: () => {
        Notify.create({ type: 'positive', message: 'Shared Azure credentials updated' })
        closeAzureManagerDialog()
      },
      onError: (err) => {
        Notify.create({ type: 'negative', message: getErrorMessage(err, 'Unable to update shared Azure credentials') })
      }
    }
  )
}

function handleFormSubmit(values: SecretProviderFormModel) {
  const capabilities = buildCapabilitiesEnum(values.capabilities)
  
  if (selectedProvider.value?.id) {
    const payload = Object.assign(new UpdateSecretProvider(), {
      name: values.name || undefined,
      vaultUri: values.vaultUri || undefined,
      authMode: values.authMode || undefined,
      credentials: values.authMode === SecretProviderAuthMode.ClientSecret && 
                   values.credentials.tenantId && 
                   values.credentials.clientId && 
                   values.credentials.clientSecret
        ? Object.assign(new SecretProviderCredentials(), {
            tenantId: values.credentials.tenantId,
            clientId: values.credentials.clientId,
            clientSecret: values.credentials.clientSecret
          })
        : undefined,
      capabilities
    })
    updateMutation.mutate({ id: selectedProvider.value.id, payload })
  } else {
    const payload = Object.assign(new CreateSecretProvider(), {
      name: values.name || undefined,
      vaultUri: values.vaultUri || undefined,
      authMode: values.authMode || undefined,
      credentials: values.authMode === SecretProviderAuthMode.ClientSecret && 
                   values.credentials.tenantId && 
                   values.credentials.clientId && 
                   values.credentials.clientSecret
        ? Object.assign(new SecretProviderCredentials(), {
            tenantId: values.credentials.tenantId,
            clientId: values.credentials.clientId,
            clientSecret: values.credentials.clientSecret
          })
        : undefined,
      capabilities
    })
    createMutation.mutate(payload)
  }
}

function confirmDelete(provider: SecretProviderResponse) {
  if (!provider.id) return
  Dialog.create({
    title: 'Delete secret provider',
    message: `Delete "${provider.name ?? 'this secret provider'}"?`,
    cancel: true,
    persistent: true
  }).onOk(() => deleteMutation.mutate(provider.id!))
}

function isAppConfiguration(provider: SecretProviderResponse): boolean {
  return isAppConfigurationEndpoint(provider.vaultUri)
}

function openExplorer(provider: SecretProviderResponse) {
  if (!provider.id) return
  router.push({
    name: isAppConfiguration(provider) ? 'appConfigExplorer' : 'keyVaultExplorer',
    params: { id: provider.id }
  })
}
</script>

<style scoped>
@import '../styles/pages.css';
</style>
