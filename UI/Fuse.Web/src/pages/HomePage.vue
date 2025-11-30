<template>
  <div class="dashboard-page">
    <q-banner
      v-if="showOnboardingBanner"
      class="q-mb-lg bg-primary text-white"
      inline-actions
      rounded
    >
      <template #avatar>
        <q-icon name="school" color="white" />
      </template>
      Ready for a guided tour to set up Fuse Inventory?
      <template #action>
        <q-btn flat color="white" label="Start tutorial" @click="startOnboardingTour" />
        <q-btn flat color="white" label="Skip for now" @click="skipOnboarding" />
      </template>
    </q-banner>

    <section class="page-header">
      <div>
        <h1>Explore your estate</h1>
        <p class="subtitle">
          Start broad, then drill into application footprints, deployment targets, and connected services.
        </p>
      </div>
    </section>

    <section class="summary-grid">
      <StatCard
        :value="applicationCount"
        label="Applications"
        icon="apps"
        icon-class="bg-primary text-white"
        to="/applications"
        :health-breakdown="applicationHealthBreakdown"
      />
      <StatCard
        :value="platformCount"
        label="Platforms"
        icon="dns"
        icon-class="bg-secondary text-white"
        to="/platforms"
      />
      <StatCard
        :value="environmentCount"
        label="Environments"
        icon="layers"
        icon-class="bg-positive text-white"
        to="/environments"
      />
      <StatCard
        :value="externalResourceCount"
        label="External Resources"
        icon="hub"
        icon-class="bg-dark text-white"
        to="/external-resources"
      />
    </section>

    <section class="explore-section">
      <div class="section-header">
        <h2>Inventory</h2>
        <div class="filters">
          <q-input
            v-model="searchText"
            dense
            outlined
            clearable
            @clear="searchText = ''"
            class="filter-select"
            placeholder="Search by name..."
          >
            <template #prepend>
              <q-icon name="search" />
            </template>
          </q-input>
          <q-select
            v-model="selectedEnvironments"
            dense
            outlined
            multiple
            emit-value
            map-options
            clearable
            :options="environmentOptions"
            class="filter-select"
            placeholder="Filter by environment"
            popup-content-class="filter-popup"
            @clear="selectedEnvironments = []"
          >
            <template #option="scope">
              <q-item v-bind="scope.itemProps" @click="scope.toggleOption(scope.opt)">
                <q-item-section side>
                  <q-checkbox :model-value="scope.selected" @update:model-value="() => scope.toggleOption(scope.opt)" />
                </q-item-section>
                <q-item-section>
                  <q-item-label>{{ scope.opt.label }}</q-item-label>
                  <q-item-label caption>{{ scope.opt.caption }}</q-item-label>
                </q-item-section>
              </q-item>
            </template>
          </q-select>
          <q-select
            v-model="selectedItemTypes"
            dense
            outlined
            multiple
            emit-value
            map-options
            clearable
            :options="itemTypeOptions"
            class="filter-select"
            placeholder="Filter by item type"
            popup-content-class="filter-popup"
            @clear="selectedItemTypes = []"
          >
            <template #option="scope">
              <q-item v-bind="scope.itemProps" @click="scope.toggleOption(scope.opt)">
                <q-item-section side>
                  <q-checkbox :model-value="scope.selected" @update:model-value="() => scope.toggleOption(scope.opt)" />
                </q-item-section>
                <q-item-section>
                  <q-item-label>{{ scope.opt.label }}</q-item-label>
                </q-item-section>
              </q-item>
            </template>
          </q-select>
        </div>
      </div>

      <div class="inventory-grid" v-if="!isLoading">
        <template v-for="item in filteredInventoryItems" :key="item.key">
          <InventoryInstanceCard
            v-if="item.type === 'instance'"
            :instance="item.data.instance"
            :application-id="item.data.applicationId"
            :application-name="item.data.applicationName"
            :application-icon="item.data.applicationIcon"
            :environment-name="item.data.environmentName"
            :platform-name="item.data.platformName"
            :dependency-formatter="formatDependencyLabel"
          />
          <InventoryDataStoreCard
            v-else-if="item.type === 'datastore'"
            :data-store="item.data.dataStore"
            :environment-name="item.data.environmentName"
            :platform-name="item.data.platformName"
            :tag-info-lookup="tagInfoLookup"
          />
          <InventoryExternalResourceCard
            v-else-if="item.type === 'external'"
            :external-resource="item.data.externalResource"
            :tag-info-lookup="tagInfoLookup"
          />
        </template>

        <div v-if="!filteredInventoryItems.length" class="empty-results">
          <q-icon name="search_off" size="32px" class="text-grey-5" />
          <p>No items match your filters. Try adjusting the environment or item type.</p>
        </div>
      </div>

      <div v-else class="loading-state">
        <q-spinner color="primary" size="36px" />
        <p>Loading inventory…</p>
      </div>
    </section>
  </div>
</template>

<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { Notify } from 'quasar'
import { TargetKind } from '../api/client'
import { useApplications } from '../composables/useApplications'
import { usePlatforms } from '../composables/usePlatforms'
import { useEnvironments } from '../composables/useEnvironments'
import { useExternalResources } from '../composables/useExternalResources'
import { useDataStores } from '../composables/useDataStores'
import { useTags } from '../composables/useTags'
import { useApplicationHealth } from '../composables/useApplicationHealth'
import StatCard from '../components/home/StatCard.vue'
import InventoryInstanceCard from '../components/home/InventoryInstanceCard.vue'
import InventoryDataStoreCard from '../components/home/InventoryDataStoreCard.vue'
import InventoryExternalResourceCard from '../components/home/InventoryExternalResourceCard.vue'
import { useOnboardingStore } from '../stores/OnboardingStore'
import { useOnboardingTour } from '../composables/useOnboardingTour'
import { getErrorMessage } from '../utils/error'

const selectedEnvironments = ref<string[]>([])
const ALL_ITEM_TYPES: string[] = ['instance', 'datastore', 'external']
const selectedItemTypes = ref<string[]>([...ALL_ITEM_TYPES])
const searchText = ref<string>('')

const onboardingStore = useOnboardingStore()
const { startTour } = useOnboardingTour()

const showOnboardingBanner = computed(
  () => !onboardingStore.hasCompletedTour && !onboardingStore.dismissedBanner
)

const applicationsQuery = useApplications()
const platformsQuery = usePlatforms()
const environmentsQuery = useEnvironments()
const externalResourcesQuery = useExternalResources()
const dataStoresQuery = useDataStores()
const tagsQuery = useTags()
const applicationHealthQuery = useApplicationHealth()

const isLoading = computed(() => 
  applicationsQuery.isLoading.value || 
  applicationsQuery.isFetching.value ||
  dataStoresQuery.isLoading.value ||
  externalResourcesQuery.isLoading.value
)

const applicationCount = computed(() => applicationsQuery.data.value?.length ?? 0)
const platformCount = computed(() => platformsQuery.data.value?.length ?? 0)
const environmentCount = computed(() => environmentsQuery.data.value?.length ?? 0)
const externalResourceCount = computed(() => externalResourcesQuery.data.value?.length ?? 0)

// Compute health breakdown for applications
const applicationHealthBreakdown = computed(() => {
  const healthStats = applicationHealthQuery.data.value
  
  // Only show breakdown if Kuma integration is available and we have health data
  if (!healthStats?.hasKumaIntegration) {
    return undefined
  }
  
  // Only show if we have at least one application with health status
  if (healthStats.healthy === 0 && healthStats.unhealthy === 0) {
    return undefined
  }
  
  return {
    healthy: healthStats.healthy,
    unhealthy: healthStats.unhealthy
  }
})

function makeLookup<T>(
  arr: T[] | undefined | null,
  getId: (t: T) => string | undefined | null,
  getLabel: (t: T) => string | undefined | null,
  fallback: string
): Record<string, string> {
  return (arr ?? []).reduce((map, item) => {
    const id = getId(item) ?? ''
    const label = getLabel(item) ?? fallback
    map[id] = label
    return map
  }, {} as Record<string, string>)
}

const environmentLookup = computed<Record<string, string>>(() =>
  makeLookup(environmentsQuery.data.value, (e: any) => e.id, (e: any) => e.name, 'Environment')
)

const platformLookup = computed<Record<string, string>>(() =>
  makeLookup(platformsQuery.data.value, (p: any) => p.id, (p: any) => p.displayName, 'Platform')
)

const externalResourceLookup = computed<Record<string, string>>(() =>
  makeLookup(externalResourcesQuery.data.value, (r: any) => r.id, (r: any) => r.name, 'External resource')
)

const dataStoreLookup = computed<Record<string, string>>(() =>
  makeLookup(dataStoresQuery.data.value, (s: any) => s.id, (s: any) => s.name, 'Data store')
)

const tagInfoLookup = tagsQuery.tagInfoLookup

const applicationLookup = computed<Record<string, string>>(() =>
  makeLookup(applicationsQuery.data.value, (a: any) => a.id, (a: any) => a.name, 'Application')
)

const environmentOptions = computed(() => {
  return (environmentsQuery.data.value ?? []).map((environment) => ({
    label: environment.name ?? 'Environment',
    value: environment.id ?? '',
    caption: environment.description ?? undefined
  }))
})

// Keep selectedEnvironments in sync with all environment IDs by default
watch(
  () => environmentsQuery.data.value,
  (envs) => {
    const allIds = (envs ?? []).map((e: { id?: string | null }) => e.id ?? '')
    if (allIds.length && selectedEnvironments.value.length === 0) {
      selectedEnvironments.value = allIds
    }
  },
  { immediate: true }
)

const itemTypeOptions = [
  { label: 'Instances', value: 'instance' },
  { label: 'Data Stores', value: 'datastore' },
  { label: 'External Resources', value: 'external' }
]

const normalizedSearch = computed(() => (searchText.value ?? '').toString().toLowerCase().trim())
const inSelectedEnvs = (envId?: string | null) =>
  selectedEnvironments.value.length === 0 || selectedEnvironments.value.includes(envId ?? '')
const matchesSearch = (text: string) => {
  const s = normalizedSearch.value
  return !s || text.toLowerCase().includes(s)
}

// Build unified inventory list
const filteredInventoryItems = computed(() => {
  const items: Array<{
    key: string
    type: 'instance' | 'datastore' | 'external'
    environmentId?: string
    name: string
    data: any
  }> = []

  const types = selectedItemTypes.value.length > 0 ? selectedItemTypes.value : ALL_ITEM_TYPES

  // Add instances
  if (types.includes('instance')) {
    const applications = applicationsQuery.data.value ?? []
    for (const app of applications) {
      for (const instance of app.instances ?? []) {
        // Filter by environment
        if (!inSelectedEnvs(instance.environmentId)) {
          continue
        }

        const appName = app.name ?? 'Unknown'
        const envName = environmentLookup.value[instance.environmentId ?? ''] ?? 'Unknown'
        const instanceName = `${appName} — ${envName}`
        
        // Filter by search text
        if (!matchesSearch(appName)) {
          continue
        }

        items.push({
          key: `instance-${app.id}-${instance.id}`,
          type: 'instance',
          environmentId: instance.environmentId,
          name: instanceName,
          data: {
            instance,
            applicationId: app.id ?? '',
            applicationName: appName,
            applicationIcon: app.icon ?? undefined,
            environmentName: envName,
            platformName: platformLookup.value[instance.platformId ?? ''] ?? 'Unknown'
          }
        })
      }
    }
  }

  // Add data stores
  if (types.includes('datastore')) {
    const dataStores = dataStoresQuery.data.value ?? []
    for (const store of dataStores) {
      // Filter by environment
      if (!inSelectedEnvs(store.environmentId)) {
        continue
      }

      const storeName = store.name ?? 'Unknown'
      
      // Filter by search text
      if (!matchesSearch(storeName)) {
        continue
      }

      items.push({
        key: `datastore-${store.id}`,
        type: 'datastore',
        environmentId: store.environmentId,
        name: storeName,
        data: {
          dataStore: store,
          environmentName: environmentLookup.value[store.environmentId ?? ''] ?? 'Unknown',
          platformName: platformLookup.value[store.platformId ?? ''] ?? 'Unknown'
        }
      })
    }
  }

  // Add external resources
  if (types.includes('external')) {
    const resources = externalResourcesQuery.data.value ?? []
    for (const resource of resources) {
      const resourceName = resource.name ?? 'Unknown'
      
      // Filter by search text
      if (!matchesSearch(resourceName)) {
        continue
      }

      items.push({
        key: `external-${resource.id}`,
        type: 'external',
        name: resourceName,
        data: {
          externalResource: resource
        }
      })
    }
  }

  // Sort items alphabetically by name
  return items.sort((a, b) => a.name.localeCompare(b.name))
})

function formatDependencyLabel(dependency: { targetKind?: TargetKind | null; targetId?: string | null }) {
  if (!dependency?.targetKind || !dependency.targetId) {
    return 'Dependency'
  }

  if (dependency.targetKind === TargetKind.Application) {
    // Treat targetId as an application instance ID; fallback to legacy application ID
    const apps = applicationsQuery.data.value ?? []
    for (const app of apps) {
      const inst = (app.instances ?? []).find((i) => i.id === dependency.targetId)
      if (inst) {
        const envName = environmentLookup.value[inst.environmentId ?? ''] ?? '—'
        const appName = app.name ?? app.id ?? 'Application'
        return `${appName} — ${envName}`
      }
    }
    // Fallback to application id mapping if present
    return applicationLookup.value[dependency.targetId] ?? 'Application'
  }

  if (dependency.targetKind === TargetKind.DataStore) {
    return dataStoreLookup.value[dependency.targetId] ?? 'Data store'
  }

  if (dependency.targetKind === TargetKind.External) {
    return externalResourceLookup.value[dependency.targetId] ?? 'External resource'
  }

  return 'Dependency'
}

async function startOnboardingTour() {
  try {
    const started = await startTour()

    if (!started) {
      Notify.create({ type: 'info', message: 'All onboarding steps are already complete.' })
    }
  } catch (error) {
    Notify.create({ type: 'negative', message: getErrorMessage(error, 'Unable to start tour') })
  }
}

function skipOnboarding() {
  if (!onboardingStore.dismissedBanner) {
    onboardingStore.dismissBanner()
  }
}
</script>

<style scoped>
.dashboard-page {
  padding: 1.5rem clamp(1.25rem, 3vw, 2.5rem);
  display: flex;
  flex-direction: column;
  gap: 1.5rem;
  max-width: 1400px;
  margin: 0 auto;
}

.page-header h1 {
  font-size: 1.75rem;
  margin: 0 0 0.375rem;
  font-weight: 600;
}

.subtitle {
  margin: 0;
  font-size: 0.9375rem;
  color: var(--fuse-text-muted);
  max-width: 60ch;
  transition: color 0.25s ease;
}

.summary-grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(200px, 1fr));
  gap: 0.875rem;
}

.explore-section {
  display: flex;
  flex-direction: column;
  gap: 1rem;
}

.section-header {
  display: flex;
  flex-wrap: wrap;
  justify-content: space-between;
  align-items: center;
  gap: 1rem;
}

.section-header h2 {
  margin: 0;
  font-size: 1.25rem;
  font-weight: 600;
}

.filters {
  display: flex;
  gap: 0.625rem;
  flex-wrap: wrap;
}

.filter-select {
  min-width: 240px;
}

.inventory-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(320px, 1fr));
  gap: 1rem;
}

.empty-results {
  padding: 3rem 1.5rem;
  text-align: center;
  color: var(--fuse-text-subtle);
  display: flex;
  flex-direction: column;
  gap: 0.625rem;
  align-items: center;
  background: var(--fuse-panel-bg);
  border: 1px solid var(--fuse-panel-border);
  border-radius: 12px;
  transition: background-color 0.25s ease, color 0.25s ease, border-color 0.25s ease;
}

.loading-state {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 0.75rem;
  padding: 3rem 0;
  color: var(--fuse-text-subtle);
  transition: color 0.25s ease;
}

@media (max-width: 768px) {
  .inventory-grid {
    grid-template-columns: 1fr;
  }
}

@media (max-width: 640px) {
  .filters {
    width: 100%;
  }

  .filter-select {
    flex: 1 1 100%;
    min-width: unset;
  }
}
</style>
