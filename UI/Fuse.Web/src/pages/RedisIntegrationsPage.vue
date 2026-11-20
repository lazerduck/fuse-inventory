<template>
  <div class="page-container">
    <div class="page-header">
      <div><h1>Redis Integrations</h1><p class="subtitle">Connect Redis endpoints and inspect their users, access rules and server metadata.</p></div>
      <q-btn label="Add Integration" icon="add" color="primary" :disable="!canRead || !canCreate" @click="openCreate" />
    </div>
    <q-banner v-if="!canRead" dense class="bg-orange-1 text-orange-9 q-mb-md">You do not have permission to view Redis integrations.</q-banner>
    <q-banner v-if="integrationError" dense class="bg-red-1 text-negative q-mb-md">{{ integrationError }}</q-banner>
    <q-card v-if="canRead" class="content-card">
      <q-table flat bordered :rows="integrations" :columns="columns" row-key="id" :loading="query.isLoading.value"
        :pagination="pagination" @update:pagination="Object.assign(pagination, $event)" :filter="filter">
        <template #top-right><q-input v-model="filter" dense outlined placeholder="Search Redis integrations" aria-label="Search Redis integrations" /></template>
        <template #body-cell-dataStore="props"><q-td :props="props">{{ dataStoreLookup[props.row.dataStoreId] || props.row.dataStoreId }}</q-td></template>
        <template #body-cell-test="props"><q-td :props="props">
          <q-badge v-if="probeResults[props.row.id]" :color="redisStatusColor(probeResults[props.row.id]?.status)"
            :label="redisStatusLabel(probeResults[props.row.id]?.status)" />
          <span v-else class="text-grey-7">Not checked this session</span>
        </q-td></template>
        <template #body-cell-actions="props"><q-td :props="props" class="text-right">
          <IntegrationPasswordButton provider="redis" :integration-id="props.row.id" @saved="query.refetch()" />
            <q-btn v-if="store.hasPermission('integrationreviews:read')" flat dense round color="secondary" icon="rule" aria-label="Redis reviews"
            :to="{ name: 'integrationReviews', query: { provider: 'redis', integration: props.row.id } }"><q-tooltip>Reviews</q-tooltip></q-btn>
          <q-btn flat dense round color="secondary" icon="manage_search" aria-label="Inspect Redis access"
            :to="{ name: 'redisIntegrationAccess', params: { id: props.row.id } }"><q-tooltip>Inspect access</q-tooltip></q-btn>
          <q-btn flat dense round color="secondary" icon="cable" aria-label="Test Redis connection"
            :loading="activeProbes.has(props.row.id)" :disable="!canValidate" @click="testSaved(props.row)"><q-tooltip>Test connection</q-tooltip></q-btn>
          <q-btn flat dense round color="primary" icon="edit" aria-label="Edit Redis integration"
            :disable="!canUpdate" @click="openEdit(props.row)"><q-tooltip>Edit integration</q-tooltip></q-btn>
          <q-btn flat dense round color="negative" icon="delete" aria-label="Delete Redis integration"
            :disable="!canDelete" @click="confirmDelete(props.row)"><q-tooltip>Delete integration</q-tooltip></q-btn>
        </q-td></template>
        <template #no-data><div class="q-pa-md text-grey-7">No Redis integrations configured. Add an integration for an existing Redis data store.</div></template>
      </q-table>
    </q-card>
    <q-dialog v-model="formOpen" persistent>
      <RedisIntegrationForm ref="formRef" :key="selected?.id || 'new'" :mode="selected ? 'edit' : 'create'"
        :initial-value="selected" :data-stores="dataStores" :loading="saving"
        :data-stores-loading="dataStoresQuery.isLoading.value" :data-stores-error="dataStoresError"
        :disabled="selected ? !canUpdate : !canCreate" :can-validate="canValidate"
        @submit="save" @cancel="formOpen = false" />
    </q-dialog>
  </div>
</template>

<script setup lang="ts">
import IntegrationPasswordButton from '../components/integrations/IntegrationPasswordButton.vue'
import { computed, onBeforeUnmount, reactive, ref } from 'vue'
import { useMutation, useQuery, useQueryClient } from '@tanstack/vue-query'
import { Dialog, Notify, type QTableColumn } from 'quasar'
import { Permission } from 'permissions'
import { useFuseStore } from '../stores/FuseStore'
import { useFuseClient } from '../composables/useFuseClient'
import { useRedisIntegrations } from '../composables/useRedisIntegrations'
import { usePersistedTableState } from '../composables/usePersistedTableState'
import { createRedisIntegration, updateRedisIntegration, deleteRedisIntegration, validateSavedRedisConnection,
  type RedisIntegrationResponse, type RedisIntegrationFormModel, type RedisConnectionTestResult } from '../api/redisIntegrations'
import { getErrorMessage } from '../utils/error'
import { redisStatusColor, redisStatusLabel } from '../utils/redisIntegration'
import RedisIntegrationForm from '../components/redisIntegration/RedisIntegrationForm.vue'

const store = useFuseStore()
const client = useFuseClient()
const queryClient = useQueryClient()
const canRead = computed(() => store.hasPermission(Permission.RedisIntegrationsRead))
const canCreate = computed(() => store.hasPermission(Permission.RedisIntegrationsCreate))
const canUpdate = computed(() => store.hasPermission(Permission.RedisIntegrationsUpdate))
const canDelete = computed(() => store.hasPermission(Permission.RedisIntegrationsDelete))
const canValidate = computed(() => store.hasPermission(Permission.RedisIntegrationsValidate))
const canReadDataStores = computed(() => store.hasPermission(Permission.DataStoresRead))
const query = useRedisIntegrations(canRead)
const dataStoresQuery = useQuery({ queryKey: ['dataStores'], queryFn: () => client.dataStoreAll(),
  enabled: computed(() => canRead.value && canReadDataStores.value) })
const dataStores = computed(() => canReadDataStores.value ? dataStoresQuery.data.value ?? [] : [])
const dataStoresError = computed(() => !canReadDataStores.value ? 'Data Stores read permission is required to choose a different data store.'
  : dataStoresQuery.error.value ? getErrorMessage(dataStoresQuery.error.value, 'Data store details are unavailable.') : null)
const dataStoreLookup = computed(() => Object.fromEntries(dataStores.value.map(item => [item.id, item.name])))
const integrations = computed(() => query.data.value ?? [])
const integrationError = computed(() => query.error.value ? getErrorMessage(query.error.value) : null)
const pagination = reactive({ rowsPerPage: 10, page: 1 })
const filter = ref('')
usePersistedTableState({ filter, pagination, filterStorageKey: 'RedisIntegrationsPage_filter', pageStorageKey: 'RedisIntegrationsPage_page' })
const columns: QTableColumn<RedisIntegrationResponse>[] = [
  { name: 'name', label: 'Name', field: 'name', align: 'left', sortable: true },
  { name: 'dataStore', label: 'Data Store', field: 'dataStoreId', align: 'left' },
  { name: 'endpoint', label: 'Endpoint', field: 'endpoint', align: 'left' },
  { name: 'username', label: 'Redis username', field: 'username', align: 'left' },
  { name: 'test', label: 'Connection check', field: 'id', align: 'left' },
  { name: 'actions', label: '', field: 'id', align: 'right' },
]
const selected = ref<RedisIntegrationResponse | null>(null)
const formOpen = ref(false)
const formRef = ref<InstanceType<typeof RedisIntegrationForm> | null>(null)
const activeProbes = reactive(new Set<string>())
const probeResults = reactive<Record<string, RedisConnectionTestResult>>({})
const probeVersions = new Map<string, number>()
let mounted = true
onBeforeUnmount(() => { mounted = false })
function invalidateProbe(id: string) { delete probeResults[id]; probeVersions.set(id, (probeVersions.get(id) ?? 0) + 1) }
function openCreate() { if (canCreate.value && canRead.value) { selected.value = null; formOpen.value = true } }
function openEdit(item: RedisIntegrationResponse) { if (canUpdate.value) { selected.value = item; formOpen.value = true } }
const saveMutation = useMutation({
  mutationFn: ({ id, value }: { id?: string; value: RedisIntegrationFormModel }) => id
    ? updateRedisIntegration(id, value) : createRedisIntegration(value),
  onSuccess: result => {
    if (result.id) invalidateProbe(result.id)
    formRef.value?.markSaved()
    formOpen.value = false
    void queryClient.invalidateQueries({ queryKey: ['redisIntegrations'] })
    if (result.id) void queryClient.invalidateQueries({ queryKey: ['redisIntegration', result.id] })
    Notify.create({ type: 'positive', message: 'Redis integration saved' })
  },
  onError: error => Notify.create({ type: 'negative', message: getErrorMessage(error, 'Unable to save Redis integration') }),
})
const saving = computed(() => saveMutation.isPending.value)
function save(value: RedisIntegrationFormModel) {
  if (selected.value ? !canUpdate.value : !canCreate.value) return
  saveMutation.mutate({ id: selected.value?.id, value })
}
const deleteMutation = useMutation({
  mutationFn: deleteRedisIntegration,
  onSuccess: (_, id) => {
    invalidateProbe(id)
    void queryClient.invalidateQueries({ queryKey: ['redisIntegrations'] })
    queryClient.removeQueries({ queryKey: ['redisIntegration', id] })
    queryClient.removeQueries({ queryKey: ['redisDiscovery', id] })
    Notify.create({ type: 'positive', message: 'Redis integration removed' })
  },
  onError: error => Notify.create({ type: 'negative', message: getErrorMessage(error, 'Unable to remove Redis integration') }),
})
function confirmDelete(item: RedisIntegrationResponse) {
  if (!canDelete.value || !item.id) return
  const id = item.id
  Dialog.create({ title: 'Delete Redis integration', message: `Remove ${item.name} from Fuse? Redis users and data will stay in place.`,
    cancel: true, persistent: true, ok: { label: 'Delete integration', color: 'negative' } })
    .onOk(() => { if (canDelete.value) deleteMutation.mutate(id) })
}
async function testSaved(item: RedisIntegrationResponse) {
  if (!canValidate.value || !item.id || activeProbes.has(item.id)) return
  const id = item.id
  const version = (probeVersions.get(id) ?? 0) + 1
  probeVersions.set(id, version)
  activeProbes.add(id)
  try {
    const result = await validateSavedRedisConnection(id)
    if (!mounted || probeVersions.get(id) !== version) return
    probeResults[id] = result
    Notify.create({ type: result.isSuccessful ? 'positive' : 'warning',
      message: result.isSuccessful ? `${item.name}: connection successful` : result.errorMessage || redisStatusLabel(result.status) })
  } catch (error) {
    if (mounted && probeVersions.get(id) === version) Notify.create({ type: 'negative', message: getErrorMessage(error, 'Unable to test Redis connection') })
  } finally { activeProbes.delete(id) }
}
</script>

<style scoped>
@import '../styles/pages.css';
</style>
