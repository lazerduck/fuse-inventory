<template>
  <div class="dashboard-page">
    <q-banner v-if="showOnboardingBanner" class="bg-primary text-white" inline-actions rounded>
      <template #avatar><q-icon name="school" color="white" /></template>
      Continue setting up your first application inventory.
      <template #action>
        <q-btn flat color="white" label="Open guide" @click="startOnboardingTour" />
        <q-btn flat color="white" label="Skip for now" @click="skipOnboarding" />
      </template>
    </q-banner>

    <section class="page-header">
      <div>
        <h1>Estate overview</h1>
        <p class="subtitle">Operational health, documentation gaps, risks, and recent inventory changes.</p>
      </div>
      <q-btn flat round icon="refresh" :loading="isFetching" aria-label="Refresh dashboard" @click="refreshDashboard">
        <q-tooltip>Refresh dashboard</q-tooltip>
      </q-btn>
    </section>

    <q-banner v-if="errorMessage" rounded class="bg-red-1 text-negative">
      <template #avatar><q-icon name="error" /></template>
      {{ errorMessage }} Some dashboard information may be unavailable.
    </q-banner>

    <section class="summary-grid" aria-label="Estate summary">
      <q-card v-for="summary in summaries" :key="summary.label" flat bordered class="summary-card" clickable @click="$router.push(summary.to)">
        <q-card-section class="summary-content">
          <q-avatar :color="summary.color" text-color="white" rounded size="42px"><q-icon :name="summary.icon" /></q-avatar>
          <div class="summary-copy">
            <div class="summary-value">{{ summary.value }}</div>
            <div class="summary-label">{{ summary.label }}</div>
            <div class="summary-detail">{{ summary.detail }}</div>
          </div>
          <q-icon name="chevron_right" color="grey-6" />
        </q-card-section>
      </q-card>
    </section>

    <section class="dashboard-grid">
      <q-card flat bordered class="attention-card">
        <q-card-section class="card-heading">
          <div>
            <h2>Attention required</h2>
            <p>Items most likely to need action, ordered by severity.</p>
          </div>
          <q-chip v-if="attentionItems.length" color="negative" text-color="white" dense>{{ attentionItems.length }}</q-chip>
        </q-card-section>
        <q-separator />
        <q-list v-if="attentionItems.length" separator>
          <q-item v-for="item in visibleAttentionItems" :key="item.key" clickable :to="item.to">
            <q-item-section avatar><q-icon :name="item.icon" :color="item.color" size="26px" /></q-item-section>
            <q-item-section>
              <q-item-label>{{ item.title }}</q-item-label>
              <q-item-label caption>{{ item.detail }}</q-item-label>
            </q-item-section>
            <q-item-section side><q-chip dense outline :color="item.color">{{ item.label }}</q-chip></q-item-section>
          </q-item>
        </q-list>
        <q-card-section v-else class="empty-panel">
          <q-icon name="check_circle" color="positive" size="36px" />
          <span>No current health, risk, or required documentation alerts.</span>
        </q-card-section>
        <q-card-actions v-if="attentionItems.length > ATTENTION_LIMIT" align="right">
          <q-btn flat color="primary" :label="showAllAttention ? 'Show less' : `Show all ${attentionItems.length}`" @click="showAllAttention = !showAllAttention" />
        </q-card-actions>
      </q-card>

      <q-card flat bordered class="activity-card">
        <q-card-section class="card-heading">
          <div><h2>Recent changes</h2><p>Latest changes across the inventory.</p></div>
          <q-btn flat dense no-caps color="primary" label="View all" to="/activities" />
        </q-card-section>
        <q-separator />
        <q-list v-if="recentActivity.length" separator>
          <q-item v-for="item in recentActivity" :key="item.versionId">
            <q-item-section avatar><q-icon name="history" color="primary" /></q-item-section>
            <q-item-section>
              <q-item-label>{{ activityDescription(item) }}</q-item-label>
              <q-item-label caption>{{ item.userName || 'System' }} · {{ formatRelativeDate(item.timestamp) }}</q-item-label>
            </q-item-section>
          </q-item>
        </q-list>
        <q-card-section v-else class="empty-panel">
          <q-spinner v-if="activityLoading" color="primary" size="28px" />
          <q-icon v-else name="history" color="grey-5" size="36px" />
          <span>{{ canReadActivity ? 'No recent inventory changes.' : 'Activity access is not available.' }}</span>
        </q-card-section>
      </q-card>
    </section>

    <section class="explore-section">
      <div class="section-header">
        <div><h2>Explore inventory</h2><p>{{ filteredInventoryItems.length }} matching items</p></div>
        <q-btn flat dense no-caps :icon="explorerExpanded ? 'expand_less' : 'expand_more'" :label="explorerExpanded ? 'Hide explorer' : 'Show explorer'" @click="explorerExpanded = !explorerExpanded" />
      </div>

      <q-slide-transition>
        <div v-show="explorerExpanded" class="explorer-content">
          <div class="filters">
            <q-input v-model="searchText" dense outlined clearable debounce="150" class="filter-control" placeholder="Search names, URIs, platforms, tags…">
              <template #prepend><q-icon name="search" /></template>
            </q-input>
            <q-select v-model="selectedEnvironments" dense outlined multiple emit-value map-options clearable :options="environmentOptions" class="filter-control" label="Environments" :display-value="environmentDisplay" />
            <q-select v-model="selectedItemTypes" dense outlined multiple emit-value map-options clearable :options="itemTypeOptions" class="filter-control" label="Item types" :display-value="itemTypeDisplay" />
          </div>

          <div v-if="isInventoryLoading" class="empty-panel"><q-spinner color="primary" size="36px" /><span>Loading inventory…</span></div>
          <div v-else-if="pagedInventoryItems.length" class="inventory-grid">
            <template v-for="item in pagedInventoryItems" :key="item.key">
              <InventoryInstanceCard v-if="item.type === 'instance'" :instance="item.data.instance" :application-id="item.data.applicationId" :application-name="item.data.applicationName" :application-icon="item.data.applicationIcon" :environment-name="item.data.environmentName" :platform-name="item.data.platformName" :dependency-formatter="formatDependencyLabel" />
              <InventoryDataStoreCard v-else-if="item.type === 'datastore'" :data-store="item.data.dataStore" :environment-name="item.data.environmentName" :platform-name="item.data.platformName" :tag-info-lookup="tagInfoLookup" />
              <InventoryExternalResourceCard v-else :external-resource="item.data.externalResource" :tag-info-lookup="tagInfoLookup" />
            </template>
          </div>
          <div v-else class="empty-panel"><q-icon name="search_off" color="grey-5" size="36px" /><span>No inventory items match these filters.</span></div>

          <q-pagination v-if="pageCount > 1" v-model="inventoryPage" :max="pageCount" :max-pages="7" boundary-numbers direction-links class="pagination" />
        </div>
      </q-slide-transition>
    </section>

  </div>
</template>

<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue'
import { useQuery } from '@tanstack/vue-query'
import { Notify } from 'quasar'
import { HealthCheckProvider, InstanceHealthState, RiskImpact, RiskStatus, TargetKind, type ActivityFeedItem } from 'api/client'
import { Permission } from 'permissions'
import { useApplications } from '../composables/useApplications'
import { usePlatforms } from '../composables/usePlatforms'
import { useEnvironments } from '../composables/useEnvironments'
import { useExternalResources } from '../composables/useExternalResources'
import { useDataStores } from '../composables/useDataStores'
import { useTags } from '../composables/useTags'
import { useHealthMonitoring } from '../composables/useHealthMonitoring'
import { useActivityFeed } from '../composables/useActivityFeed'
import { useFuseClient } from '../composables/useFuseClient'
import InventoryInstanceCard from '../components/home/InventoryInstanceCard.vue'
import InventoryDataStoreCard from '../components/home/InventoryDataStoreCard.vue'
import InventoryExternalResourceCard from '../components/home/InventoryExternalResourceCard.vue'
import { useOnboardingStore } from '../stores/OnboardingStore'
import { useOnboardingTour } from '../composables/useOnboardingTour'
import { getErrorMessage } from '../utils/error'
import { useFuseStore } from '../stores/FuseStore'

type InventoryType = 'instance' | 'datastore' | 'external'
interface InventoryItem { key: string; type: InventoryType; name: string; searchText: string; data: any }
interface AttentionItem { key: string; severity: number; title: string; detail: string; label: string; icon: string; color: string; to: string }

const INVENTORY_PAGE_SIZE = 12
const ATTENTION_LIMIT = 6
const ALL_ITEM_TYPES: InventoryType[] = ['instance', 'datastore', 'external']
const client = useFuseClient()
const fuseStore = useFuseStore()
const onboardingStore = useOnboardingStore()
const { startTour } = useOnboardingTour()

const searchText = ref('')
const selectedEnvironments = ref<string[]>([])
const selectedItemTypes = ref<InventoryType[]>([])
const inventoryPage = ref(1)
const explorerExpanded = ref(true)
const showAllAttention = ref(false)

const applicationsQuery = useApplications()
const platformsQuery = usePlatforms()
const environmentsQuery = useEnvironments()
const externalResourcesQuery = useExternalResources()
const dataStoresQuery = useDataStores()
const tagsQuery = useTags()
const healthQuery = useHealthMonitoring()
const completenessQuery = useQuery({ queryKey: ['applicationCompleteness'], queryFn: () => client.applicationHealth(), enabled: computed(() => fuseStore.hasPermission(Permission.ApplicationsRead)) })
const risksQuery = useQuery({ queryKey: ['risks'], queryFn: () => client.riskAll(), enabled: computed(() => fuseStore.hasPermission(Permission.RisksRead)) })
const { items: recentActivity, loading: activityLoading, error: activityError, queryActivity } = useActivityFeed()

const canReadActivity = computed(() => fuseStore.hasPermission(Permission.ActivityRead))
const showOnboardingBanner = computed(() => fuseStore.hasPermission(Permission.ApplicationsCreate) && !onboardingStore.hasCompletedTour && !onboardingStore.dismissedBanner)
const applicationCount = computed(() => applicationsQuery.data.value?.length ?? 0)
const instanceCount = computed(() => (applicationsQuery.data.value ?? []).reduce((total, app) => total + (app.instances?.length ?? 0), 0))
const healthEndpointCount = computed(() => (applicationsQuery.data.value ?? []).reduce((total, app) => total + (app.instances ?? []).filter(instance => !!instance.healthUri).length, 0))
const healthMonitoringEnabled = computed(() => !!fuseStore.appSettings && fuseStore.appSettings.healthCheckProvider !== HealthCheckProvider.None)
const unhealthyCount = computed(() => healthQuery.data.value?.unhealthy ?? 0)
const unknownCount = computed(() => healthQuery.data.value?.unknown ?? 0)
const requiredGapCount = computed(() => documentationGaps.value.length)
const openHighRiskCount = computed(() => (risksQuery.data.value ?? []).filter(r => ![RiskStatus.Closed, RiskStatus.Mitigated].includes(r.status!) && [RiskImpact.High, RiskImpact.Critical].includes(r.impact!)).length)

const summaries = computed(() => [
  { label: 'Applications', value: applicationCount.value, detail: `${instanceCount.value} deployed instances`, icon: 'apps', color: 'primary', to: '/applications' },
  healthMonitoringEnabled.value
    ? { label: 'Service health', value: unhealthyCount.value, detail: `${unknownCount.value} unknown · ${healthQuery.data.value?.healthy ?? 0} healthy`, icon: 'monitor_heart', color: unhealthyCount.value ? 'negative' : 'positive', to: '/health' }
    : { label: 'Health monitoring', value: 'Off', detail: `${healthEndpointCount.value} of ${instanceCount.value} endpoints ready · Configure`, icon: 'monitor_heart', color: 'grey-7', to: '/appsettings' },
  { label: 'Required gaps', value: requiredGapCount.value, detail: 'Missing required documentation', icon: 'fact_check', color: requiredGapCount.value ? 'warning' : 'positive', to: '/insights/documentation-completeness' },
  { label: 'High risks', value: openHighRiskCount.value, detail: 'Open high or critical risks', icon: 'warning', color: openHighRiskCount.value ? 'negative' : 'positive', to: '/risks' }
])

const documentationGaps = computed(() => {
  if (completenessQuery.isLoading.value || completenessQuery.error.value) return []
  const byApp = new Map((completenessQuery.data.value ?? []).map(item => [item.applicationId, item]))
  const gaps: Array<{ key: string; title: string; detail: string; to: string }> = []
  for (const app of applicationsQuery.data.value ?? []) {
    if (!app.id) continue
    const health = byApp.get(app.id)
    const missing = [!health?.versionSet && 'version', !health?.descriptionSet && 'description', !health?.ownerSet && 'owner', !health?.frameworkSet && 'framework'].filter(Boolean)
    if (!health || missing.length) gaps.push({ key: `docs-${app.id}`, title: app.name ?? 'Application', detail: !health ? 'Documentation assessment unavailable' : `Missing ${missing.join(', ')}`, to: `/applications/${app.id}` })
  }
  return gaps
})

const attentionItems = computed<AttentionItem[]>(() => {
  const items: AttentionItem[] = []
  for (const result of healthQuery.data.value?.results ?? []) {
    if (result.state === InstanceHealthState.Healthy) continue
    items.push({ key: `health-${result.instanceId}`, severity: result.state === InstanceHealthState.Unhealthy ? 100 : 70, title: result.applicationName ?? 'Application instance', detail: `${result.environmentName || 'Unknown environment'} · ${result.state ?? 'Unknown health'}`, label: result.state === InstanceHealthState.Unhealthy ? 'Down' : 'Unknown', icon: result.state === InstanceHealthState.Unhealthy ? 'cancel' : 'help', color: result.state === InstanceHealthState.Unhealthy ? 'negative' : 'warning', to: '/health' })
  }
  for (const risk of risksQuery.data.value ?? []) {
    if ([RiskStatus.Closed, RiskStatus.Mitigated].includes(risk.status!) || ![RiskImpact.High, RiskImpact.Critical].includes(risk.impact!)) continue
    items.push({ key: `risk-${risk.id}`, severity: risk.impact === RiskImpact.Critical ? 90 : 80, title: risk.title ?? 'Untitled risk', detail: `${risk.impact} impact · ${risk.status}`, label: risk.impact ?? 'Risk', icon: 'warning', color: risk.impact === RiskImpact.Critical ? 'negative' : 'deep-orange', to: risk.id ? `/risks/${risk.id}/edit` : '/risks' })
  }
  for (const gap of documentationGaps.value) items.push({ ...gap, severity: 50, label: 'Documentation', icon: 'description', color: 'warning' })
  return items.sort((a, b) => b.severity - a.severity || a.title.localeCompare(b.title))
})
const visibleAttentionItems = computed(() => showAllAttention.value ? attentionItems.value : attentionItems.value.slice(0, ATTENTION_LIMIT))

function makeLookup<T>(arr: T[] | undefined, getId: (item: T) => string | undefined, getLabel: (item: T) => string | undefined, fallback: string) {
  return (arr ?? []).reduce<Record<string, string>>((map, item) => { map[getId(item) ?? ''] = getLabel(item) ?? fallback; return map }, {})
}
const environmentLookup = computed(() => makeLookup(environmentsQuery.data.value, e => e.id, e => e.name, 'Environment'))
const platformLookup = computed(() => makeLookup(platformsQuery.data.value, p => p.id, p => p.displayName, 'Platform'))
const externalResourceLookup = computed(() => makeLookup(externalResourcesQuery.data.value, r => r.id, r => r.name, 'External resource'))
const dataStoreLookup = computed(() => makeLookup(dataStoresQuery.data.value, s => s.id, s => s.name, 'Data store'))
const applicationLookup = computed(() => makeLookup(applicationsQuery.data.value, a => a.id, a => a.name, 'Application'))
const tagInfoLookup = tagsQuery.tagInfoLookup
const environmentOptions = computed(() => (environmentsQuery.data.value ?? []).map(e => ({ label: e.name ?? 'Environment', value: e.id ?? '' })))
const itemTypeOptions = [{ label: 'Instances', value: 'instance' }, { label: 'Data Stores', value: 'datastore' }, { label: 'External Resources', value: 'external' }]
const environmentDisplay = computed(() => selectedEnvironments.value.length ? `${selectedEnvironments.value.length} environment${selectedEnvironments.value.length === 1 ? '' : 's'}` : 'All environments')
const itemTypeDisplay = computed(() => selectedItemTypes.value.length ? `${selectedItemTypes.value.length} item type${selectedItemTypes.value.length === 1 ? '' : 's'}` : 'All item types')

const filteredInventoryItems = computed<InventoryItem[]>(() => {
  const items: InventoryItem[] = []
  const types = selectedItemTypes.value.length ? selectedItemTypes.value : ALL_ITEM_TYPES
  const envs = selectedEnvironments.value
  const tags = tagInfoLookup.value
  if (types.includes('instance')) for (const app of applicationsQuery.data.value ?? []) for (const instance of app.instances ?? []) {
    if (envs.length && !envs.includes(instance.environmentId ?? '')) continue
    const environmentName = environmentLookup.value[instance.environmentId ?? ''] ?? 'Unknown'
    const platformName = platformLookup.value[instance.platformId ?? ''] ?? 'Unknown'
    const dependencies = (instance.dependencies ?? []).map(formatDependencyLabel).join(' ')
    const tagNames = (instance.tagIds ?? []).map(id => tags[id]?.name ?? id).join(' ')
    items.push({ key: `instance-${app.id}-${instance.id}`, type: 'instance', name: `${app.name ?? 'Unknown'} — ${environmentName}`, searchText: `${app.name} ${environmentName} ${platformName} ${instance.baseUri} ${instance.healthUri} ${instance.openApiUri} ${instance.version} ${dependencies} ${tagNames}`, data: { instance, applicationId: app.id ?? '', applicationName: app.name ?? 'Unknown', applicationIcon: app.icon, environmentName, platformName } })
  }
  if (types.includes('datastore')) for (const store of dataStoresQuery.data.value ?? []) {
    if (envs.length && !envs.includes(store.environmentId ?? '')) continue
    const environmentName = environmentLookup.value[store.environmentId ?? ''] ?? 'Unknown'
    const platformName = platformLookup.value[store.platformId ?? ''] ?? 'Unknown'
    const tagNames = (store.tagIds ?? []).map(id => tags[id]?.name ?? id).join(' ')
    items.push({ key: `datastore-${store.id}`, type: 'datastore', name: store.name ?? 'Unknown', searchText: `${store.name} ${store.description} ${store.connectionUri} ${store.kind} ${environmentName} ${platformName} ${tagNames}`, data: { dataStore: store, environmentName, platformName } })
  }
  if (types.includes('external')) for (const resource of externalResourcesQuery.data.value ?? []) {
    const tagNames = (resource.tagIds ?? []).map(id => tags[id]?.name ?? id).join(' ')
    items.push({ key: `external-${resource.id}`, type: 'external', name: resource.name ?? 'Unknown', searchText: `${resource.name} ${resource.description} ${resource.resourceUri} ${tagNames}`, data: { externalResource: resource } })
  }
  const term = searchText.value.toLowerCase().trim()
  return items.filter(item => !term || item.searchText.toLowerCase().includes(term)).sort((a, b) => a.name.localeCompare(b.name))
})
const pageCount = computed(() => Math.ceil(filteredInventoryItems.value.length / INVENTORY_PAGE_SIZE))
const pagedInventoryItems = computed(() => filteredInventoryItems.value.slice((inventoryPage.value - 1) * INVENTORY_PAGE_SIZE, inventoryPage.value * INVENTORY_PAGE_SIZE))
watch([searchText, selectedEnvironments, selectedItemTypes], () => { inventoryPage.value = 1 }, { deep: true })

const inventoryQueries = [applicationsQuery, platformsQuery, environmentsQuery, externalResourcesQuery, dataStoresQuery]
const isInventoryLoading = computed(() => inventoryQueries.some(query => query.isLoading.value))
const isFetching = computed(() => inventoryQueries.some(query => query.isFetching.value) || healthQuery.isFetching.value || completenessQuery.isFetching.value || risksQuery.isFetching.value || activityLoading.value)
const errorMessage = computed(() => {
  const error = [...inventoryQueries, healthQuery, completenessQuery, risksQuery].map(query => query.error.value).find(Boolean)
  return error ? getErrorMessage(error, 'Unable to load dashboard data.') : activityError.value
})

function formatDependencyLabel(dependency: { targetKind?: TargetKind | null; targetId?: string | null }) {
  if (!dependency.targetKind || !dependency.targetId) return 'Dependency'
  if (dependency.targetKind === TargetKind.Application) {
    for (const app of applicationsQuery.data.value ?? []) {
      const instance = app.instances?.find(i => i.id === dependency.targetId)
      if (instance) return `${app.name ?? 'Application'} — ${environmentLookup.value[instance.environmentId ?? ''] ?? 'Unknown'}`
    }
    return applicationLookup.value[dependency.targetId] ?? 'Application'
  }
  if (dependency.targetKind === TargetKind.DataStore) return dataStoreLookup.value[dependency.targetId] ?? 'Data store'
  if (dependency.targetKind === TargetKind.External) return externalResourceLookup.value[dependency.targetId] ?? 'External resource'
  return String(dependency.targetKind).replace(/([a-z])([A-Z])/g, '$1 $2')
}
function activityDescription(item: ActivityFeedItem) { return item.changeDescription || `${String(item.entityType ?? 'Inventory')} ${String(item.action ?? 'changed').toLowerCase()}` }
function formatRelativeDate(value?: Date) {
  if (!value) return 'Recently'
  const seconds = Math.round((value.getTime() - Date.now()) / 1000)
  const formatter = new Intl.RelativeTimeFormat(undefined, { numeric: 'auto' })
  if (Math.abs(seconds) < 3600) return formatter.format(Math.round(seconds / 60), 'minute')
  if (Math.abs(seconds) < 86400) return formatter.format(Math.round(seconds / 3600), 'hour')
  return formatter.format(Math.round(seconds / 86400), 'day')
}
async function refreshDashboard() {
  await Promise.all([...inventoryQueries, healthQuery, completenessQuery, risksQuery].map(query => query.refetch()))
  if (canReadActivity.value) await queryActivity({ page: 1, pageSize: 5 })
}
async function startOnboardingTour() {
  try { if (!(await startTour())) Notify.create({ type: 'info', message: 'All onboarding steps are already complete.' }) }
  catch (error) { Notify.create({ type: 'negative', message: getErrorMessage(error, 'Unable to start tour') }) }
}
function skipOnboarding() { onboardingStore.dismissBanner() }

onMounted(() => { if (canReadActivity.value) queryActivity({ page: 1, pageSize: 5 }) })
</script>

<style scoped>
.dashboard-page { padding: 1.5rem clamp(1.25rem, 3vw, 2.5rem); display: flex; flex-direction: column; gap: 1.5rem; max-width: 1440px; margin: 0 auto; }
.page-header, .section-header, .card-heading { display: flex; justify-content: space-between; align-items: center; gap: 1rem; }
h1 { font-size: 1.75rem; margin: 0 0 .375rem; font-weight: 600; }
h2 { font-size: 1.15rem; margin: 0; font-weight: 600; }
.subtitle, .section-header p, .card-heading p { margin: .25rem 0 0; color: var(--fuse-text-muted); }
.summary-grid { display: grid; grid-template-columns: repeat(4, minmax(0, 1fr)); gap: .875rem; }
.summary-card { cursor: pointer; border-radius: 12px; transition: box-shadow .2s ease, transform .2s ease; }
.summary-card:hover { box-shadow: var(--fuse-shadow-2); transform: translateY(-2px); }
.summary-content { display: flex; align-items: center; gap: .75rem; }
.summary-copy { flex: 1; min-width: 0; }
.summary-value { font-size: 1.5rem; font-weight: 600; line-height: 1.1; }
.summary-label { font-weight: 500; }
.summary-detail { color: var(--fuse-text-muted); font-size: .75rem; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
.dashboard-grid { display: grid; grid-template-columns: minmax(0, 3fr) minmax(320px, 2fr); gap: 1rem; align-items: start; }
.attention-card, .activity-card, .explore-section { border-radius: 12px; }
.card-heading { padding: 1rem; }
.empty-panel { min-height: 130px; display: flex; flex-direction: column; justify-content: center; align-items: center; text-align: center; gap: .6rem; color: var(--fuse-text-muted); }
.explore-section { border: 1px solid var(--fuse-panel-border); padding: 1rem; }
.explorer-content { padding-top: 1rem; }
.filters { display: grid; grid-template-columns: minmax(280px, 2fr) 1fr 1fr; gap: .625rem; margin-bottom: 1rem; }
.inventory-grid { display: grid; grid-template-columns: repeat(3, minmax(0, 1fr)); gap: 1rem; }
.pagination { justify-content: center; margin-top: 1rem; }
@media (max-width: 1100px) { .summary-grid { grid-template-columns: repeat(2, 1fr); } .inventory-grid { grid-template-columns: repeat(2, minmax(0, 1fr)); } }
@media (max-width: 800px) { .dashboard-grid { grid-template-columns: 1fr; } .filters { grid-template-columns: 1fr; } }
@media (max-width: 600px) { .summary-grid, .inventory-grid { grid-template-columns: 1fr; } .dashboard-page { padding: 1rem; } }
</style>
