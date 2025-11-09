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
      />
      <StatCard
        :value="serverCount"
        label="Servers"
        icon="dns"
        icon-class="bg-secondary text-white"
        to="/servers"
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
        <h2>Applications</h2>
        <div class="filters">
          <q-input
            v-model="search"
            dense
            outlined
            debounce="100"
            clearable
            placeholder="Search by name, owner, tag, or framework"
            class="filter-input"
            prepend-inner-icon="search"
          />
          <q-select
            v-model="selectedEnvironment"
            dense
            outlined
            emit-value
            map-options
            clearable
            :options="environmentOptions"
            class="filter-select"
            placeholder="Filter by environment"
            popup-content-class="filter-popup"
          >
            <template #option="scope">
              <q-item v-bind="scope.itemProps">
                <q-item-section>
                  <q-item-label>{{ scope.opt.label }}</q-item-label>
                  <q-item-label caption>{{ scope.opt.caption }}</q-item-label>
                </q-item-section>
              </q-item>
            </template>
          </q-select>
        </div>
      </div>

      <div class="applications-list" v-if="!applicationsLoading">
        <ApplicationCard
          v-for="application in filteredApplications"
          :key="application.id"
          :application="application"
          :environment-lookup="environmentLookup"
          :server-lookup="serverLookup"
          :tag-lookup="tagLookup"
          :format-dependency-label="formatDependencyLabel"
        />

        <div v-if="!filteredApplications.length" class="empty-results">
          <q-icon name="search_off" size="32px" class="text-grey-5" />
          <p>No applications match your filters. Try adjusting the search or environment.</p>
        </div>
      </div>

      <div v-else class="loading-state">
        <q-spinner color="primary" size="36px" />
        <p>Loading applications…</p>
      </div>
    </section>
  </div>
</template>

<script setup lang="ts">
import { computed, ref } from 'vue'
import { Notify } from 'quasar'
import { TargetKind } from '../api/client'
import { useApplications } from '../composables/useApplications'
import { usePlatforms } from '../composables/usePlatforms'
import { useEnvironments } from '../composables/useEnvironments'
import { useExternalResources } from '../composables/useExternalResources'
import { useDataStores } from '../composables/useDataStores'
import { useTags } from '../composables/useTags'
import StatCard from '../components/home/StatCard.vue'
import ApplicationCard from '../components/home/ApplicationCard.vue'
import { useOnboardingStore } from '../stores/OnboardingStore'
import { useOnboardingTour } from '../composables/useOnboardingTour'
import { getErrorMessage } from '../utils/error'

const search = ref('')
const selectedEnvironment = ref<string | null>(null)

const onboardingStore = useOnboardingStore()
const { startTour } = useOnboardingTour()

const showOnboardingBanner = computed(
  () => !onboardingStore.hasCompletedTour && !onboardingStore.dismissedBanner
)

const applicationsQuery = useApplications()
const serversQuery = usePlatforms()
const environmentsQuery = useEnvironments()
const externalResourcesQuery = useExternalResources()
const dataStoresQuery = useDataStores()
const tagsQuery = useTags()

const applicationsLoading = computed(() => applicationsQuery.isLoading.value || applicationsQuery.isFetching.value)

const applicationCount = computed(() => applicationsQuery.data.value?.length ?? 0)
const serverCount = computed(() => serversQuery.data.value?.length ?? 0)
const environmentCount = computed(() => environmentsQuery.data.value?.length ?? 0)
const externalResourceCount = computed(() => externalResourcesQuery.data.value?.length ?? 0)

const environmentLookup = computed<Record<string, string>>(() => {
  return (environmentsQuery.data.value ?? []).reduce((map, env) => {
    map[env.id ?? ''] = env.name ?? 'Environment'
    return map
  }, {} as Record<string, string>)
})

const serverLookup = computed<Record<string, string>>(() => {
  return (serversQuery.data.value ?? []).reduce((map, server) => {
    map[server.id ?? ''] = server.name ?? 'Server'
    return map
  }, {} as Record<string, string>)
})

const externalResourceLookup = computed<Record<string, string>>(() => {
  return (externalResourcesQuery.data.value ?? []).reduce((map, resource) => {
    map[resource.id ?? ''] = resource.name ?? 'External resource'
    return map
  }, {} as Record<string, string>)
})

const dataStoreLookup = computed<Record<string, string>>(() => {
  return (dataStoresQuery.data.value ?? []).reduce((map, store) => {
    map[store.id ?? ''] = store.name ?? 'Data store'
    return map
  }, {} as Record<string, string>)
})

const tagLookup = computed<Record<string, string>>(() => {
  return (tagsQuery.data.value ?? []).reduce((map, tag) => {
    map[tag.id ?? ''] = tag.name ?? tag.id ?? 'Tag'
    return map
  }, {} as Record<string, string>)
})

const applicationLookup = computed<Record<string, string>>(() => {
  return (applicationsQuery.data.value ?? []).reduce((map, app) => {
    map[app.id ?? ''] = app.name ?? 'Application'
    return map
  }, {} as Record<string, string>)
})

const environmentOptions = computed(() => {
  return (environmentsQuery.data.value ?? []).map((environment) => ({
    label: environment.name ?? 'Environment',
    value: environment.id ?? '',
    caption: environment.description ?? undefined
  }))
})

const filteredApplications = computed(() => {
  const term = search.value.trim().toLowerCase()
  const environmentFilter = selectedEnvironment.value

  return (applicationsQuery.data.value ?? []).filter((application) => {
    const matchesEnvironment = environmentFilter
      ? (application.instances ?? []).some((instance) => instance.environmentId === environmentFilter)
      : true

    if (!matchesEnvironment) {
      return false
    }

    if (!term) {
      return true
    }

    const haystack = [
      application.name ?? '',
      application.owner ?? '',
      application.framework ?? '',
      application.tagIds?.map((tagId) => tagLookup.value[tagId] ?? tagId).join(' ') ?? '',
      application.repositoryUri ?? ''
    ]

    return haystack.some((value) => value.toLowerCase().includes(term))
  })
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

.filter-input,
.filter-select {
  min-width: 220px;
}

.applications-list {
  display: flex;
  flex-direction: column;
  gap: 0.875rem;
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

@media (max-width: 640px) {
  .filters {
    width: 100%;
  }

  .filter-input,
  .filter-select {
    flex: 1 1 100%;
    min-width: unset;
  }
}
</style>
