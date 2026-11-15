<template>
  <div class="page-container">
    <div class="page-header">
      <div>
        <h1>Azure integrations</h1>
        <p class="subtitle">Manage Key Vault secrets and App Configuration stores.</p>
      </div>
      <div class="row q-gutter-sm">
        <q-btn outline color="primary" label="Discover Azure resources" icon="travel_explore" :disable="!canCreate" @click="isDiscoveryOpen = true" />
        <q-btn color="primary" :label="activeKind === 'KeyVault' ? 'Add Key Vault' : 'Add App Configuration'" icon="add" :disable="!canCreate" @click="openCreateDialog" />
      </div>
    </div>
    <q-banner v-if="secretProviderError" dense class="bg-red-1 text-negative q-mb-md">{{ secretProviderError }}</q-banner>
    <q-banner v-if="!fuseStore.canRead" dense class="bg-orange-1 text-orange-9 q-mb-md">
      You do not have permission to view Azure integrations. Please log in with appropriate credentials.
    </q-banner>

    <q-card v-if="fuseStore.canRead" class="content-card">
      <q-expansion-item icon="key" label="Shared Azure credentials" :caption="hasSharedClientSecretCredentials ? 'Configured · available to both services' : 'Not configured · managed identity is also supported'">
        <q-card-section>
          <q-banner v-if="azureManagerError" dense class="bg-red-1 text-negative q-mb-md">{{ azureManagerError }}</q-banner>
          <q-skeleton v-if="isAzureManagerLoading" type="QToolbar" />
          <template v-else>
            <p class="text-body2 text-grey-7">Integrations can use shared client secret credentials, custom credentials, or managed identity.</p>
            <div v-if="hasSharedClientSecretCredentials" class="text-body2 q-mb-md">
              <div><strong>Tenant ID:</strong> {{ azureManager?.tenantId }}</div>
              <div><strong>Client ID:</strong> {{ azureManager?.clientId }}</div>
              <div><strong>Last updated:</strong> {{ formatDate(azureManager?.updatedAt) }}</div>
            </div>
            <q-btn color="primary" :label="hasSharedClientSecretCredentials ? 'Update shared credentials' : 'Set shared credentials'" icon="key" :disable="!canUpdate" @click="openAzureManagerDialog" />
          </template>
        </q-card-section>
      </q-expansion-item>
    </q-card>

    <q-card v-if="fuseStore.canRead" class="content-card">
      <q-tabs v-model="activeKind" align="left" active-color="primary" indicator-color="primary" inline-label>
        <q-tab name="KeyVault" icon="vpn_key" :label="`Key Vaults (${keyVaultCount})`" />
        <q-tab name="AppConfiguration" icon="tune" :label="`App Configuration (${appConfigurationCount})`" />
      </q-tabs>
      <q-separator />
      <q-card-section class="row items-center q-col-gutter-md">
        <div class="col-12 col-md">
          <div class="text-subtitle1">{{ activeKind === 'KeyVault' ? 'Key Vaults' : 'App Configuration stores' }}</div>
          <div class="text-caption text-grey-7">{{ activeKind === 'KeyVault' ? 'Browse secret metadata and manage secret values.' : 'Browse and update configuration entries.' }}</div>
        </div>
        <div class="col-12 col-md-5">
          <q-input v-model="filter" dense outlined clearable placeholder="Search by name or endpoint" aria-label="Search Azure integrations">
            <template #prepend><q-icon name="search" /></template>
          </q-input>
        </div>
      </q-card-section>
      <q-table flat :rows="visibleProviders" :columns="columns" :filter="filter" row-key="id" :loading="isLoading" :pagination="pagination" @update:pagination="Object.assign(pagination, $event)">
        <template #body-cell-name="props">
          <q-td :props="props"><div class="text-weight-medium">{{ props.row.name }}</div><div class="text-caption text-grey-7">{{ props.row.vaultUri }}</div></q-td>
        </template>
        <template #body-cell-capabilities="props">
          <q-td :props="props"><div class="q-gutter-xs"><q-badge v-for="cap in capabilityLabels(props.row)" :key="cap" outline color="primary" :label="cap" /></div></q-td>
        </template>
        <template #body-cell-status="props">
          <q-td :props="props">
            <q-badge :color="props.row.lastCheckSucceeded == null ? 'grey-7' : props.row.lastCheckSucceeded ? 'positive' : 'negative'"
              :label="props.row.lastCheckSucceeded == null ? 'Not checked' : props.row.lastCheckSucceeded ? 'Browse access verified' : 'Access check failed'" />
            <div v-if="props.row.lastCheckedAt" class="text-caption text-grey-7 q-mt-xs">{{ formatDate(props.row.lastCheckedAt) }}</div>
            <q-tooltip v-if="props.row.lastCheckMessage">{{ props.row.lastCheckMessage }}</q-tooltip>
          </q-td>
        </template>
        <template #body-cell-actions="props">
          <q-td :props="props" class="text-right">
            <q-btn flat dense round icon="manage_search" color="secondary" :aria-label="isAppConfiguration(props.row) ? 'Explore App Configuration' : 'Explore Key Vault'" @click="openExplorer(props.row)"><q-tooltip>Open explorer</q-tooltip></q-btn>
            <q-btn flat dense round icon="network_check" color="primary" aria-label="Test integration access" :disable="!canCreate && !canUpdate" :loading="checkingIds.has(props.row.id)" @click="checkAccess(props.row)"><q-tooltip>Test browse access</q-tooltip></q-btn>
            <q-btn flat dense round icon="edit" color="primary" aria-label="Edit integration" :disable="!canUpdate" @click="openEditDialog(props.row)"><q-tooltip>Edit integration</q-tooltip></q-btn>
            <q-btn flat dense round icon="delete" color="negative" aria-label="Delete integration" :disable="!fuseStore.hasPermission(Permission.AzureKeyVaultConnectionsDelete)" @click="confirmDelete(props.row)"><q-tooltip>Delete integration</q-tooltip></q-btn>
          </q-td>
        </template>
        <template #no-data>
          <div class="q-pa-md text-grey-7">{{ filter ? 'No integrations match your search.' : activeKind === 'KeyVault' ? 'No Key Vaults configured. Add a Key Vault or discover Azure resources.' : 'No App Configuration stores configured. Add a store or discover Azure resources.' }}</div>
        </template>
      </q-table>
    </q-card>

    <q-dialog v-model="isDiscoveryOpen" persistent>
      <AzureDiscoveryDialog v-if="isDiscoveryOpen" :has-shared-credentials="hasSharedClientSecretCredentials" @close="isDiscoveryOpen = false" @imported="refreshProviders" />
    </q-dialog>
    <q-dialog v-model="isFormDialogOpen" persistent>
      <SecretProviderForm ref="providerFormRef" :mode="selectedProvider ? 'edit' : 'create'" :initial-value="selectedProvider" :integration-kind="activeKind"
        :loading="formLoading" :disabled="selectedProvider ? !canUpdate : !canCreate" :allow-shared-client-secret-credentials="hasSharedClientSecretCredentials"
        @submit="handleFormSubmit" @cancel="closeFormDialog" @access-checked="refreshProviders" />
    </q-dialog>
    <q-dialog v-model="isAzureManagerDialogOpen" persistent>
      <q-card class="form-dialog">
        <q-card-section class="dialog-header">
          <div class="text-h6">Shared Azure credentials</div>
          <q-btn aria-label="Close dialog" flat round dense icon="close" :disable="isUpdatingAzureManager" @click="closeAzureManagerDialog" />
        </q-card-section>
        <q-separator />
        <q-form @submit.prevent="submitAzureManagerCredentials">
          <q-card-section class="form-grid">
            <q-input v-model="azureManagerForm.tenantId" label="Tenant ID*" dense outlined :rules="[val => !!val || 'Tenant ID is required']" />
            <q-input v-model="azureManagerForm.clientId" label="Client ID*" dense outlined :rules="[val => !!val || 'Client ID is required']" />
            <q-input v-model="azureManagerForm.clientSecret" label="Client Secret*" type="password" dense outlined :rules="[val => !!val || 'Client Secret is required']" />
          </q-card-section>
          <q-separator />
          <q-card-actions align="right">
            <q-btn flat label="Cancel" :disable="isUpdatingAzureManager" @click="closeAzureManagerDialog" />
            <q-btn color="primary" type="submit" label="Save shared credentials" :loading="isUpdatingAzureManager" :disable="!canUpdate" />
          </q-card-actions>
        </q-form>
      </q-card>
    </q-dialog>
  </div>
</template>

<script setup lang="ts">
import { computed, ref, reactive, watch } from 'vue'
import { useRouter } from 'vue-router'
import { useMutation, useQueryClient } from '@tanstack/vue-query'
import { useLocalStorage } from '@vueuse/core'
import { Notify, Dialog, type QTableColumn } from 'quasar'
import { SecretProviderResponse, CreateSecretProvider, UpdateSecretProvider, SecretProviderAuthMode, SecretProviderCapabilities, SecretProviderCredentials } from 'api/client'
import { testSavedIntegration, type AzureIntegrationKind } from '../api/azureDiscovery'
import { Permission } from 'permissions'
import { usePersistedTableState } from '../composables/usePersistedTableState'
import { useFuseClient } from '../composables/useFuseClient'
import { useFuseStore } from '../stores/FuseStore'
import { useSecretProviders } from '../composables/useSecretProviders'
import { useAzureIntegrationManager } from '../composables/useAzureIntegrationManager'
import { useUnsavedChanges } from '../composables/useUnsavedChanges'
import { getErrorMessage } from '../utils/error'
import { isAppConfiguration, parseCapabilities } from '../utils/secretProviders'
import SecretProviderForm from '../components/secretProvider/SecretProviderForm.vue'
import AzureDiscoveryDialog from '../components/secretProvider/AzureDiscoveryDialog.vue'

interface SecretProviderFormModel {
  name: string; vaultUri: string; authMode: SecretProviderAuthMode | null
  credentials: { tenantId: string; clientId: string; clientSecret: string }
  capabilities: { check: boolean; create: boolean; rotate: boolean; read: boolean }
}
const client = useFuseClient()
const queryClient = useQueryClient()
const fuseStore = useFuseStore()
const router = useRouter()
const canCreate = computed(() => fuseStore.hasPermission(Permission.AzureKeyVaultConnectionsCreate))
const canUpdate = computed(() => fuseStore.hasPermission(Permission.AzureKeyVaultConnectionsUpdate))
const activeKind = useLocalStorage<AzureIntegrationKind>('AzureIntegrations_kind', 'KeyVault')
if (!['KeyVault', 'AppConfiguration'].includes(activeKind.value)) activeKind.value = 'KeyVault'
const pagination = reactive({ rowsPerPage: 10, page: 1 })
const filter = ref('')
usePersistedTableState({ filter, pagination, filterStorageKey: 'SecretProvidersPage_filter', pageStorageKey: 'SecretProvidersPage_page' })
watch(activeKind, () => { pagination.page = 1 })
const { data, isLoading, error } = useSecretProviders()
const { data: azureManagerData, isLoading: isAzureManagerLoading, error: azureManagerQueryError, updateMutation: updateAzureManagerMutation } = useAzureIntegrationManager()
const secretProviders = computed(() => data.value ?? [])
const visibleProviders = computed(() => secretProviders.value.filter(p => isAppConfiguration(p) === (activeKind.value === 'AppConfiguration')))
const keyVaultCount = computed(() => secretProviders.value.filter(p => !isAppConfiguration(p)).length)
const appConfigurationCount = computed(() => secretProviders.value.length - keyVaultCount.value)
const secretProviderError = computed(() => error.value ? getErrorMessage(error.value) : null)
const azureManager = computed(() => azureManagerData.value)
const azureManagerError = computed(() => azureManagerQueryError.value ? getErrorMessage(azureManagerQueryError.value) : null)
const hasSharedClientSecretCredentials = computed(() => !!azureManager.value?.hasClientSecretCredentials)
const columns: QTableColumn<SecretProviderResponse>[] = [
  { name: 'name', label: 'Name / endpoint', field: p => `${p.name ?? ''} ${p.vaultUri ?? ''}`, align: 'left', sortable: true },
  { name: 'credentialSource', label: 'Credentials', field: credentialLabel, align: 'left', sortable: true },
  { name: 'capabilities', label: 'Enabled capabilities', field: p => capabilityLabels(p).join(', '), align: 'left' },
  { name: 'status', label: 'Access', field: p => p.lastCheckSucceeded == null ? 'Not checked' : p.lastCheckSucceeded ? 'Browse access verified' : 'Access check failed', align: 'left' },
  { name: 'actions', label: '', field: 'id', align: 'right' }
]
const isFormDialogOpen = ref(false)
const isDiscoveryOpen = ref(false)
const providerFormRef = ref<InstanceType<typeof SecretProviderForm> | null>(null)
const selectedProvider = ref<SecretProviderResponse | null>(null)
const checkingIds = reactive(new Set<string>())
const isAzureManagerDialogOpen = ref(false)
const azureManagerForm = ref({ tenantId: '', clientId: '', clientSecret: '' })
const managerDraft = useUnsavedChanges(azureManagerForm, isAzureManagerDialogOpen)
const refreshProviders = () => queryClient.invalidateQueries({ queryKey: ['secretProviders'] })
function openCreateDialog() { selectedProvider.value = null; isFormDialogOpen.value = true }
function openEditDialog(provider: SecretProviderResponse) { if (provider.id) { selectedProvider.value = provider; isFormDialogOpen.value = true } }
function closeFormDialog() { isFormDialogOpen.value = false; selectedProvider.value = null }
function openAzureManagerDialog() {
  azureManagerForm.value = { tenantId: azureManager.value?.tenantId ?? '', clientId: azureManager.value?.clientId ?? '', clientSecret: '' }
  managerDraft.markClean()
  isAzureManagerDialogOpen.value = true
}
function closeAzureManagerDialog() { if (!isUpdatingAzureManager.value) void managerDraft.discard(() => { isAzureManagerDialogOpen.value = false; azureManagerForm.value.clientSecret = '' }) }
function formatDate(value?: Date | string | null) { return value ? new Date(value).toLocaleString() : 'Not checked' }
function credentialLabel(provider: SecretProviderResponse) {
  if (provider.authMode === SecretProviderAuthMode.ManagedIdentity) return 'Managed identity'
  if (provider.credentialSource === 'CustomCredentials') return 'Custom credentials'
  if (provider.credentialSource === 'SharedCredentials') return 'Shared credentials'
  return 'Client secret'
}
function capabilityLabels(provider: SecretProviderResponse) {
  const config = isAppConfiguration(provider)
  return parseCapabilities(provider.capabilities).filter(c => !config || c !== 'Read').map(c => ({ Check: config ? 'Browse configuration' : 'Browse metadata', Create: 'Create', Rotate: config ? 'Update' : 'Rotate', Read: 'Reveal values' })[c])
}
async function checkAccess(provider: SecretProviderResponse) {
  if (!provider.id || checkingIds.has(provider.id) || (!canCreate.value && !canUpdate.value)) return
  checkingIds.add(provider.id)
  try {
    const result = await testSavedIntegration(provider.id)
    await refreshProviders()
    Notify.create({ type: result.success ? 'positive' : 'negative', message: result.message })
  } catch (err) { Notify.create({ type: 'negative', message: getErrorMessage(err, 'Unable to test access') }) }
  finally { checkingIds.delete(provider.id) }
}
const createMutation = useMutation({
  mutationFn: (payload: CreateSecretProvider) => client.secretProviderPOST(payload),
  onSuccess: () => { refreshProviders(); Notify.create({ type: 'positive', message: 'Integration added' }); closeFormDialog() },
  onError: (err) => Notify.create({ type: 'negative', message: getErrorMessage(err, 'Unable to add integration') })
})
const updateMutation = useMutation({
  mutationFn: ({ id, payload }: { id: string; payload: UpdateSecretProvider; close: boolean }) => client.secretProviderPUT(id, payload),
  onSuccess: (provider, variables) => {
    providerFormRef.value?.markSaved()
    selectedProvider.value = provider
    refreshProviders()
    Notify.create({ type: 'positive', message: 'Integration saved' })
    if (variables.close) closeFormDialog()
  },
  onError: (err) => Notify.create({ type: 'negative', message: getErrorMessage(err, 'Unable to save integration') })
})
const deleteMutation = useMutation({
  mutationFn: (id: string) => client.secretProviderDELETE(id),
  onSuccess: () => { refreshProviders(); Notify.create({ type: 'positive', message: 'Integration deleted' }) },
  onError: (err) => Notify.create({ type: 'negative', message: getErrorMessage(err, 'Unable to delete integration') })
})
const formLoading = computed(() => selectedProvider.value ? updateMutation.isPending.value : createMutation.isPending.value)
const isUpdatingAzureManager = computed(() => updateAzureManagerMutation.isPending.value)
function submitAzureManagerCredentials() {
  if (!canUpdate.value || isUpdatingAzureManager.value) return
  managerDraft.captureSave()
  updateAzureManagerMutation.mutate({ credentials: { ...azureManagerForm.value } }, {
    onSuccess: () => { managerDraft.markSaved(); refreshProviders(); isAzureManagerDialogOpen.value = false; azureManagerForm.value.clientSecret = ''; Notify.create({ type: 'positive', message: 'Shared Azure credentials updated' }) },
    onError: (err) => Notify.create({ type: 'negative', message: getErrorMessage(err, 'Unable to update shared Azure credentials') })
  })
}
function handleFormSubmit(values: SecretProviderFormModel, close = true) {
  if (formLoading.value || (selectedProvider.value ? !canUpdate.value : !canCreate.value)) return
  let flags = 0
  if (values.capabilities.check) flags |= 1
  if (values.capabilities.create) flags |= 2
  if (values.capabilities.rotate) flags |= 4
  if (values.capabilities.read) flags |= 8
  const fields = {
    name: values.name, vaultUri: values.vaultUri, authMode: values.authMode ?? undefined,
    credentials: values.authMode === SecretProviderAuthMode.ClientSecret && values.credentials.tenantId && values.credentials.clientId && values.credentials.clientSecret
      ? new SecretProviderCredentials({ ...values.credentials }) : undefined,
    capabilities: flags as unknown as SecretProviderCapabilities
  }
  if (selectedProvider.value?.id) updateMutation.mutate({ id: selectedProvider.value.id, payload: new UpdateSecretProvider(fields), close })
  else createMutation.mutate(new CreateSecretProvider(fields))
}
function confirmDelete(provider: SecretProviderResponse) {
  if (!provider.id) return
  Dialog.create({ title: 'Delete integration', message: `Delete "${provider.name ?? 'this integration'}"?`, cancel: true, persistent: true }).onOk(() => deleteMutation.mutate(provider.id!))
}
function openExplorer(provider: SecretProviderResponse) {
  if (provider.id) router.push({ name: isAppConfiguration(provider) ? 'appConfigExplorer' : 'keyVaultExplorer', params: { id: provider.id } })
}
</script>

<style scoped>
@import '../styles/pages.css';
</style>
