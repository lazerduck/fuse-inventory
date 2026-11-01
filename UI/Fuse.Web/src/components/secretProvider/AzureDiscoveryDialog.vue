<template>
  <q-card class="discovery-dialog">
    <q-card-section class="dialog-header">
      <div>
        <div class="text-h6">Discover Azure resources</div>
        <div class="text-caption text-grey-7">Find Key Vaults and App Configuration stores, then choose which to add.</div>
      </div>
      <q-btn flat round dense icon="close" aria-label="Close discovery" :disable="importing" @click="emit('close')" />
    </q-card-section>
    <q-separator />
    <div class="discovery-scroll">
    <q-card-section>
      <q-banner dense class="bg-blue-1 text-primary q-mb-md">
        Discovery needs Azure resource read access for the selected identity. Access to secrets or configuration values alone may not allow discovery.
        <a href="https://learn.microsoft.com/en-us/azure/governance/resource-graph/overview#permissions-in-azure-resource-graph" target="_blank" rel="noopener noreferrer">Permission help</a>
      </q-banner>
      <div class="row q-col-gutter-md">
        <div class="col-12 col-sm-6">
          <q-select v-model="authMode" :options="authOptions" emit-value map-options outlined dense label="Discover using" :disable="busy" />
        </div>
        <div class="col-12 col-sm-6">
          <q-select v-model="subscriptionIds" :options="subscriptionOptions" multiple use-chips use-input new-value-mode="add-unique"
            emit-value map-options outlined dense label="Subscriptions" :loading="loadingSubscriptions" :disable="busy"
            hint="Select subscriptions or enter a subscription ID and press Enter." />
          <q-btn flat dense size="sm" label="Reload subscriptions" icon="refresh" :disable="busy || loadingSubscriptions" @click="loadSubscriptions" />
        </div>
        <div class="col-12 col-sm-6">
          <q-input v-model="resourceGroups" outlined dense label="Resource groups (optional)" hint="Comma-separated names. Leave empty for all accessible groups." :disable="busy" />
        </div>
        <div class="col-12 col-sm-6">
          <q-option-group v-model="kinds" :options="kindOptions" type="checkbox" inline :disable="busy" />
        </div>
      </div>
      <q-banner v-if="error" dense class="bg-red-1 text-negative q-mt-md" role="alert">{{ error }}</q-banner>
      <q-banner v-for="warning in warnings" :key="warning" dense class="bg-orange-1 text-orange-9 q-mt-sm">{{ warning }}</q-banner>
      <div class="row items-center q-gutter-sm q-mt-md">
        <q-btn color="primary" icon="travel_explore" :label="hasScanned ? 'Discover again' : 'Discover resources'" :loading="scanning"
          :disable="importing || !subscriptionIds.length || !kinds.length" @click="scan" />
        <span v-if="hasScanned" class="text-caption text-grey-7">{{ resources.length }} resources found · {{ alreadyAddedCount }} already added · {{ ignoredCount }} ignored</span>
      </div>
    </q-card-section>
    <template v-if="hasScanned">
      <q-separator />
      <q-card-section class="row items-center q-gutter-md">
        <q-input v-model="filter" outlined dense clearable placeholder="Search discovered resources" aria-label="Search discovered resources" class="col">
          <template #prepend><q-icon name="search" /></template>
        </q-input>
        <q-toggle v-model="includeIgnored" label="Include ignored resources" :disable="importing" />
      </q-card-section>
      <q-table flat :rows="filteredResources" :columns="columns" row-key="resourceId"
        :pagination="{ rowsPerPage: 10 }" :loading="scanning" selection="multiple" v-model:selected="selected">
        <template #header-selection>
          <q-checkbox :model-value="selectionState" aria-label="Select all matching new resources" :disable="importing || !selectableRows.length" @update:model-value="toggleAll" />
        </template>
        <template #body-selection="slotProps">
          <q-checkbox :model-value="slotProps.selected" :aria-label="`Include ${slotProps.row.name}`" :disable="slotProps.row.alreadyAdded || importing" @update:model-value="slotProps.selected = $event" />
        </template>
        <template #body-cell-kind="slotProps">
          <q-td :props="slotProps"><q-badge outline :color="slotProps.row.kind === 'KeyVault' ? 'deep-purple' : 'primary'">{{ kindLabel(slotProps.row.kind) }}</q-badge></q-td>
        </template>
        <template #body-cell-name="slotProps">
          <q-td :props="slotProps"><div class="text-weight-medium">{{ slotProps.row.name }}</div><div class="text-caption text-grey-7">{{ slotProps.row.endpoint }}</div></q-td>
        </template>
        <template #body-cell-state="slotProps">
          <q-td :props="slotProps">{{ slotProps.row.alreadyAdded ? 'Already added' : slotProps.row.ignored ? 'Ignored' : 'New' }}</q-td>
        </template>
        <template #no-data>
          <div class="q-pa-md text-grey-7">{{ resources.length ? 'No resources match the current filters.' : 'No discoverable resources in this scope. Check resource read permissions or add an integration manually using its endpoint.' }}</div>
        </template>
      </q-table>
      <q-card-section>
        <div class="text-caption text-grey-7">Added integrations start with browsing enabled. Test access afterwards; enable other capabilities when needed.</div>
        <q-banner v-if="importResults.length" dense class="q-mt-md" :class="failedCount ? 'bg-orange-1 text-orange-9' : 'bg-green-1 text-positive'" role="status">
          {{ addedCount }} added · {{ existingCount }} already added · {{ failedCount }} failed
          <div v-for="result in failedResults" :key="result.resourceId" class="q-mt-xs">{{ result.name }}: {{ result.error || 'Unable to add integration' }}</div>
        </q-banner>
        <div v-if="exclusionsSaved" class="text-caption text-positive q-mt-sm" role="status">Exclusions saved.</div>
      </q-card-section>
    </template>
    </div>
    <q-separator />
    <q-card-actions align="right">
      <span v-if="hasScanned" class="text-caption">{{ selected.length }} selected</span>
      <q-checkbox v-if="hasScanned" v-model="rememberExclusions" label="Remember unselected resources as exclusions" :disable="importing" class="q-mr-auto" />
      <q-btn flat :label="importResults.length ? 'Done' : 'Cancel'" :disable="importing" @click="emit('close')" />
      <q-btn v-if="hasScanned" color="primary" :label="selected.length ? `Add selected (${selected.length})` : 'Save exclusions'"
        :loading="importing" :disable="scanning || (!selected.length && (!rememberExclusions || !newExclusions.length))" @click="importSelected" />
    </q-card-actions>
  </q-card>
</template>

<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, ref, watch } from 'vue'
import type { QTableColumn } from 'quasar'
import { SecretProviderAuthMode } from '../../api/client'
import {
  discoverAzureResources, importAzureResources, listAzureSubscriptions, setAzureDiscoveryExclusions,
  type AzureDiscoveryScope, type AzureImportResult, type AzureIntegrationKind, type AzureSubscription, type DiscoveredAzureResource
} from '../../api/azureDiscovery'
import { getErrorMessage } from '../../utils/error'

const props = defineProps<{ hasSharedCredentials: boolean }>()
const emit = defineEmits<{ close: []; imported: [] }>()
const authMode = ref(props.hasSharedCredentials ? SecretProviderAuthMode.ClientSecret : SecretProviderAuthMode.ManagedIdentity)
const authOptions = computed(() => [
  { label: 'Shared credentials', value: SecretProviderAuthMode.ClientSecret, disable: !props.hasSharedCredentials },
  { label: 'Managed identity', value: SecretProviderAuthMode.ManagedIdentity }
])
const kindOptions = [{ label: 'Key Vaults', value: 'KeyVault' }, { label: 'App Configuration', value: 'AppConfiguration' }]
const kinds = ref<AzureIntegrationKind[]>(['KeyVault', 'AppConfiguration'])
const subscriptions = ref<AzureSubscription[]>([])
const subscriptionOptions = computed(() => subscriptions.value.map(s => ({ label: `${s.name} (${s.id})`, value: s.id })))
const subscriptionIds = ref<string[]>([])
const resourceGroups = ref('')
const loadingSubscriptions = ref(false)
const scanning = ref(false)
const importing = ref(false)
const busy = computed(() => scanning.value || importing.value)
const error = ref('')
const warnings = ref<string[]>([])
const hasScanned = ref(false)
const resources = ref<DiscoveredAzureResource[]>([])
const selected = ref<DiscoveredAzureResource[]>([])
const filter = ref('')
const includeIgnored = ref(false)
const rememberExclusions = ref(true)
const exclusionsSaved = ref(false)
const importResults = ref<AzureImportResult[]>([])
const scanScope = ref<AzureDiscoveryScope | null>(null)
let subscriptionRequest: AbortController | undefined
let discoveryRequest: AbortController | undefined
const visibleResources = computed(() => resources.value.filter(r => includeIgnored.value || !r.ignored))
const filteredResources = computed(() => visibleResources.value.filter(r =>
  [r.name, r.endpoint, kindLabel(r.kind), r.subscriptionId, subscriptions.value.find(s => s.id === r.subscriptionId)?.name ?? '',
    r.resourceGroup, r.location, r.alreadyAdded ? 'Already added' : r.ignored ? 'Ignored' : 'New']
    .some(v => v.toLowerCase().includes((filter.value ?? '').toLowerCase()))))
const selectableRows = computed(() => filteredResources.value.filter(r => !r.alreadyAdded))
const selectionState = computed(() => {
  const ids = new Set(selected.value.map(r => r.resourceId))
  const count = selectableRows.value.filter(r => ids.has(r.resourceId)).length
  return count === 0 ? false : count === selectableRows.value.length ? true : null
})
const alreadyAddedCount = computed(() => resources.value.filter(r => r.alreadyAdded).length)
const ignoredCount = computed(() => resources.value.filter(r => r.ignored).length)
const newExclusions = computed(() => {
  const ids = new Set(selected.value.map(r => r.resourceId))
  return resources.value.filter(r => !r.alreadyAdded && !r.ignored && !ids.has(r.resourceId)).map(r => r.resourceId)
})
const failedResults = computed(() => importResults.value.filter(r => r.status === 'Failed'))
const failedCount = computed(() => failedResults.value.length)
const addedCount = computed(() => importResults.value.filter(r => r.status === 'Added').length)
const existingCount = computed(() => importResults.value.filter(r => r.status === 'AlreadyAdded').length)
const columns: QTableColumn<DiscoveredAzureResource>[] = [
  { name: 'name', label: 'Resource', field: 'name', align: 'left', sortable: true },
  { name: 'kind', label: 'Type', field: r => kindLabel(r.kind), align: 'left', sortable: true },
  { name: 'resourceGroup', label: 'Resource group', field: 'resourceGroup', align: 'left', sortable: true },
  { name: 'subscriptionId', label: 'Subscription', field: r => subscriptions.value.find(s => s.id === r.subscriptionId)?.name ?? r.subscriptionId, align: 'left' },
  { name: 'location', label: 'Region', field: 'location', align: 'left' },
  { name: 'state', label: 'Status', field: r => r.alreadyAdded ? 'Already added' : r.ignored ? 'Ignored' : 'New', align: 'left' }
]
function kindLabel(kind: AzureIntegrationKind) { return kind === 'KeyVault' ? 'Key Vault' : 'App Configuration' }
function toggleAll(value: boolean) {
  const ids = new Set(selectableRows.value.map(r => r.resourceId))
  selected.value = [...selected.value.filter(r => !ids.has(r.resourceId)), ...(value ? selectableRows.value : [])]
}
async function loadSubscriptions() {
  subscriptionRequest?.abort()
  const controller = subscriptionRequest = new AbortController()
  loadingSubscriptions.value = true
  error.value = ''
  try {
    const response = await listAzureSubscriptions(authMode.value, controller.signal)
    if (controller.signal.aborted) return
    subscriptions.value = response.subscriptions
    warnings.value = response.warnings
    if (response.subscriptions.length === 1 && !subscriptionIds.value.length) subscriptionIds.value = [response.subscriptions[0]!.id]
  } catch (err) {
    if (!controller.signal.aborted) error.value = getErrorMessage(err, 'Unable to list subscriptions. You can enter a subscription ID manually.')
  } finally { if (!controller.signal.aborted) loadingSubscriptions.value = false }
}
async function scan() {
  if (busy.value) return
  scanning.value = true
  error.value = ''
  warnings.value = []
  importResults.value = []
  exclusionsSaved.value = false
  hasScanned.value = false
  resources.value = []
  selected.value = []
  scanScope.value = null
  const scope: AzureDiscoveryScope = {
    authMode: authMode.value, subscriptionIds: [...subscriptionIds.value], kinds: [...kinds.value],
    resourceGroups: resourceGroups.value.split(',').map(s => s.trim()).filter(Boolean)
  }
  const controller = discoveryRequest = new AbortController()
  try {
    const response = await discoverAzureResources(scope, controller.signal)
    if (controller.signal.aborted) return
    resources.value = response.resources
    selected.value = response.resources.filter(r => !r.alreadyAdded && !r.ignored)
    warnings.value = response.warnings
    scanScope.value = scope
    hasScanned.value = true
  } catch (err) {
    if (!controller.signal.aborted) error.value = getErrorMessage(err, 'Azure discovery failed')
  } finally { if (discoveryRequest === controller) scanning.value = false }
}
async function importSelected() {
  if (busy.value || !scanScope.value) return
  importing.value = true
  error.value = ''
  exclusionsSaved.value = false
  try {
    if (rememberExclusions.value && newExclusions.value.length) {
      const excludedIds = [...newExclusions.value]
      await setAzureDiscoveryExclusions(excludedIds, true)
      const ids = new Set(excludedIds)
      resources.value.forEach(r => { if (ids.has(r.resourceId)) r.ignored = true })
      exclusionsSaved.value = true
    }
    if (selected.value.length) {
      const response = await importAzureResources(scanScope.value, selected.value.map(r => r.resourceId))
      importResults.value = response.results
      warnings.value = response.warnings
      const added = new Set(response.results.filter(r => r.status !== 'Failed').map(r => r.resourceId.toLowerCase()))
      resources.value.forEach(r => { if (added.has(r.resourceId.toLowerCase())) { r.alreadyAdded = true; r.ignored = false } })
      selected.value = selected.value.filter(r => !r.alreadyAdded)
      emit('imported')
    }
  } catch (err) { error.value = getErrorMessage(err, 'Unable to add selected integrations') }
  finally { importing.value = false }
}
// Changing scope invalidates results so importing can never use a different identity or scope.
watch([authMode, subscriptionIds, resourceGroups, kinds], () => {
  discoveryRequest?.abort()
  discoveryRequest = undefined
  scanning.value = false
  hasScanned.value = false
  resources.value = []
  selected.value = []
  scanScope.value = null
  importResults.value = []
}, { deep: true })
watch(authMode, () => { subscriptionIds.value = []; subscriptions.value = []; void loadSubscriptions() })
onMounted(loadSubscriptions)
onBeforeUnmount(() => { subscriptionRequest?.abort(); discoveryRequest?.abort() })
</script>

<style scoped>
.discovery-dialog { width: 1120px; max-width: 96vw; max-height: calc(100vh - 48px); display: flex; flex-direction: column; overflow: hidden; }
.discovery-scroll { min-height: 0; overflow: auto; }
.dialog-header { display: flex; align-items: center; justify-content: space-between; gap: 16px; flex-shrink: 0; }
.q-card__actions { flex-shrink: 0; }
</style>
