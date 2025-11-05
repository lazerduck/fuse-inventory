<template>
  <div class="dashboard-page">
    <section class="hero">
      <div>
        <p class="eyebrow">Fuse Dashboard</p>
        <h1>Your interactive atlas for the platform</h1>
        <p class="lead">
          Explore every application, environment, and integration from a single, visually rich hub.
        </p>
      </div>
      <q-card class="hero-card glossy">
        <q-card-section>
          <div class="hero-highlight">
            <div class="hero-metric">{{ applicationCount }}</div>
            <div class="hero-label">Applications catalogued</div>
          </div>
          <q-separator dark spaced class="q-my-md" />
          <div class="hero-insight">
            <q-icon name="travel_explore" size="28px" class="text-primary" />
            <div>
              <p class="hero-insight-title">Jump in anywhere</p>
              <p class="hero-insight-copy">
                Filter by environment or search across owners, tags, and frameworks to uncover the right surface area.
              </p>
            </div>
          </div>
        </q-card-section>
      </q-card>
    </section>

    <section class="summary-grid">
      <q-card class="summary-card">
        <div class="summary-icon bg-primary text-white">
          <q-icon name="apps" size="28px" />
        </div>
        <div>
          <p class="summary-value">{{ applicationCount }}</p>
          <p class="summary-label">Applications</p>
        </div>
      </q-card>
      <q-card class="summary-card">
        <div class="summary-icon bg-secondary text-white">
          <q-icon name="dns" size="28px" />
        </div>
        <div>
          <p class="summary-value">{{ serverCount }}</p>
          <p class="summary-label">Servers</p>
        </div>
      </q-card>
      <q-card class="summary-card">
        <div class="summary-icon bg-positive text-white">
          <q-icon name="layers" size="28px" />
        </div>
        <div>
          <p class="summary-value">{{ environmentCount }}</p>
          <p class="summary-label">Environments</p>
        </div>
      </q-card>
      <q-card class="summary-card">
        <div class="summary-icon bg-dark text-white">
          <q-icon name="hub" size="28px" />
        </div>
        <div>
          <p class="summary-value">{{ externalResourceCount }}</p>
          <p class="summary-label">External resources</p>
        </div>
      </q-card>
    </section>

    <section class="explore-panel q-mt-lg">
      <div class="panel-header">
        <div>
          <h2>Explore your estate</h2>
          <p class="panel-subtitle">
            Start broad, then drill into application footprints, deployment targets, and connected services.
          </p>
        </div>
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

      <div class="applications-grid" v-if="!applicationsLoading">
        <q-card
          v-for="application in filteredApplications"
          :key="application.id"
          class="application-card glossy"
        >
          <q-expansion-item expand-separator default-opened>
            <template #header>
              <q-item class="application-header">
                <q-item-section avatar>
                  <div class="app-avatar">
                    <q-icon name="precision_manufacturing" size="30px" />
                  </div>
                </q-item-section>
                <q-item-section>
                  <q-item-label class="app-title">{{ application.name }}</q-item-label>
                  <q-item-label caption class="app-subtitle">
                    <span v-if="application.version" class="app-badge">v{{ application.version }}</span>
                    <span v-if="application.owner">Owned by {{ application.owner }}</span>
                  </q-item-label>
                </q-item-section>
                <q-item-section side v-if="application.framework">
                  <q-chip outline color="primary" text-color="primary">{{ application.framework }}</q-chip>
                </q-item-section>
              </q-item>
            </template>

            <q-card-section class="application-body">
              <div class="application-meta">
                <div>
                  <p class="meta-label">Repository</p>
                  <p class="meta-value">
                    <a
                      v-if="application.repositoryUri"
                      :href="application.repositoryUri"
                      target="_blank"
                      rel="noopener"
                    >
                      {{ application.repositoryUri }}
                    </a>
                    <span v-else class="text-grey">Not linked</span>
                  </p>
                </div>
                <div>
                  <p class="meta-label">Tags</p>
                  <div class="meta-tags" v-if="application.tagIds?.length">
                    <q-chip
                      v-for="tagId in application.tagIds"
                      :key="tagId"
                      dense
                      outline
                      color="grey-8"
                      text-color="grey-8"
                    >
                      {{ tagLookup[tagId] ?? tagId }}
                    </q-chip>
                  </div>
                  <span v-else class="text-grey">No tags yet</span>
                </div>
              </div>

              <div class="instances-section">
                <div class="section-title">
                  <h3>Instances</h3>
                  <span>{{ (application.instances ?? []).length }} total</span>
                </div>
                <div class="instances-grid">
                  <q-card
                    v-for="instance in application.instances ?? []"
                    :key="instance.id"
                    class="instance-card"
                  >
                    <q-card-section>
                      <div class="instance-header">
                        <q-badge outline color="primary" :label="environmentLookup[instance.environmentId ?? ''] ?? 'Environment TBD'" />
                        <q-chip v-if="instance.version" size="sm" color="primary" text-color="white" dense>
                          v{{ instance.version }}
                        </q-chip>
                      </div>
                      <div class="instance-body">
                        <p class="instance-title">
                          {{ instance.baseUri ?? 'Instance target pending' }}
                        </p>
                        <p class="instance-subtitle">
                          {{ serverLookup[instance.serverId ?? ''] ?? 'Unassigned host' }}
                        </p>
                        <div class="instance-links">
                          <q-btn
                            v-if="instance.baseUri"
                            flat
                            dense
                            icon="launch"
                            label="Open base"
                            color="primary"
                            :href="instance.baseUri"
                            target="_blank"
                            rel="noopener"
                          />
                          <q-btn
                            v-if="instance.healthUri"
                            flat
                            dense
                            icon="favorite"
                            label="Health"
                            color="positive"
                            :href="instance.healthUri"
                            target="_blank"
                            rel="noopener"
                          />
                          <q-btn
                            v-if="instance.openApiUri"
                            flat
                            dense
                            icon="api"
                            label="API docs"
                            color="secondary"
                            :href="instance.openApiUri"
                            target="_blank"
                            rel="noopener"
                          />
                        </div>
                        <div v-if="instance.tagIds?.length" class="instance-tags">
                          <q-chip
                            v-for="tagId in instance.tagIds"
                            :key="tagId"
                            size="sm"
                            outline
                            color="grey-7"
                            text-color="grey-8"
                          >
                            {{ tagLookup[tagId] ?? tagId }}
                          </q-chip>
                        </div>
                        <div v-if="instance.dependencies?.length" class="dependency-chips">
                          <q-chip
                            v-for="dependency in instance.dependencies"
                            :key="dependency.id"
                            size="sm"
                            outline
                            color="secondary"
                            text-color="secondary"
                          >
                            {{ formatDependencyLabel(dependency) }}
                          </q-chip>
                        </div>
                        <p v-else class="text-grey dependency-placeholder">No dependencies declared</p>
                      </div>
                    </q-card-section>
                  </q-card>
                  <div v-if="!(application.instances ?? []).length" class="empty-state">
                    <q-icon name="inventory_2" size="28px" class="text-grey-6" />
                    <p>This application has no instances yet.</p>
                    <q-btn flat color="primary" label="Go to applications" to="/applications" dense />
                  </div>
                </div>
              </div>
            </q-card-section>
          </q-expansion-item>
        </q-card>

        <div v-if="!filteredApplications.length" class="empty-results">
          <q-icon name="travel_explore" size="36px" class="text-grey-6" />
          <p>No applications match your filters. Try adjusting the search or environment.</p>
        </div>
      </div>

      <div v-else class="loading-state">
        <q-spinner color="primary" size="40px" />
        <p>Loading your catalog…</p>
      </div>
    </section>
  </div>
</template>

<script setup lang="ts">
import { computed, ref } from 'vue'
import { TargetKind } from '../api/client'
import { useApplications } from '../composables/useApplications'
import { useServers } from '../composables/useServers'
import { useEnvironments } from '../composables/useEnvironments'
import { useExternalResources } from '../composables/useExternalResources'
import { useDataStores } from '../composables/useDataStores'
import { useTags } from '../composables/useTags'

const search = ref('')
const selectedEnvironment = ref<string | null>(null)

const applicationsQuery = useApplications()
const serversQuery = useServers()
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
</script>

<style scoped>
.dashboard-page {
  padding: 2.5rem clamp(1.5rem, 3vw, 3rem);
  display: flex;
  flex-direction: column;
  gap: 2.5rem;
}

.hero {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(260px, 1fr));
  gap: 1.5rem;
  align-items: stretch;
}

.eyebrow {
  text-transform: uppercase;
  letter-spacing: 0.3em;
  font-size: 0.75rem;
  font-weight: 600;
  color: var(--q-primary);
  margin-bottom: 0.75rem;
}

.hero h1 {
  font-size: clamp(2rem, 4vw, 2.8rem);
  margin: 0 0 0.75rem;
}

.lead {
  font-size: 1rem;
  color: var(--q-dark-page, rgba(0, 0, 0, 0.72));
  max-width: 38ch;
}

.hero-card {
  border-radius: 18px;
  box-shadow: 0 20px 45px rgba(0, 0, 0, 0.12);
}

.glossy {
  background: linear-gradient(145deg, rgba(255, 255, 255, 0.85), rgba(236, 244, 255, 0.9));
  backdrop-filter: blur(12px);
}

.hero-highlight {
  display: flex;
  flex-direction: column;
  gap: 0.3rem;
}

.hero-metric {
  font-size: clamp(2.2rem, 4vw, 3.5rem);
  font-weight: 700;
}

.hero-label {
  font-size: 0.9rem;
  color: var(--q-dark-page, rgba(0, 0, 0, 0.6));
}

.hero-insight {
  display: flex;
  gap: 0.75rem;
  align-items: flex-start;
  margin-top: 1rem;
}

.hero-insight-title {
  font-weight: 600;
  margin-bottom: 0.25rem;
}

.hero-insight-copy {
  margin: 0;
  color: var(--q-dark-page, rgba(0, 0, 0, 0.6));
  line-height: 1.4;
}

.summary-grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(180px, 1fr));
  gap: 1rem;
}

.summary-card {
  display: flex;
  align-items: center;
  gap: 1rem;
  padding: 1.25rem;
  border-radius: 16px;
  box-shadow: 0 14px 35px rgba(0, 0, 0, 0.08);
}

.summary-icon {
  width: 48px;
  height: 48px;
  border-radius: 14px;
  display: grid;
  place-items: center;
}

.summary-value {
  font-size: 1.6rem;
  font-weight: 600;
  margin-bottom: 0.1rem;
}

.summary-label {
  margin: 0;
  color: var(--q-dark-page, rgba(0, 0, 0, 0.6));
}

.explore-panel {
  display: flex;
  flex-direction: column;
  gap: 1.5rem;
}

.panel-header {
  display: flex;
  flex-wrap: wrap;
  justify-content: space-between;
  align-items: flex-end;
  gap: 1.5rem;
}

.panel-header h2 {
  margin: 0;
}

.panel-subtitle {
  margin: 0.25rem 0 0;
  color: var(--q-dark-page, rgba(0, 0, 0, 0.65));
  max-width: 48ch;
}

.filters {
  display: flex;
  gap: 0.75rem;
  flex-wrap: wrap;
}

.filter-input,
.filter-select {
  min-width: 220px;
}

.applications-grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(320px, 1fr));
  gap: 1.25rem;
}

.application-card {
  border-radius: 18px;
  box-shadow: 0 16px 45px rgba(15, 23, 42, 0.08);
  transition: transform 0.25s ease;
}

.application-card:hover {
  transform: translateY(-6px);
}

.application-header {
  padding: 1.2rem 1rem 1rem;
}

.app-avatar {
  width: 54px;
  height: 54px;
  border-radius: 16px;
  display: grid;
  place-items: center;
  background: rgba(64, 120, 255, 0.12);
}

.app-title {
  font-size: 1.25rem;
  font-weight: 600;
}

.app-subtitle {
  display: flex;
  gap: 0.5rem;
  align-items: center;
}

.app-badge {
  background: rgba(64, 120, 255, 0.12);
  padding: 0.25rem 0.5rem;
  border-radius: 999px;
}

.application-body {
  display: flex;
  flex-direction: column;
  gap: 1.75rem;
}

.application-meta {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(200px, 1fr));
  gap: 1.25rem;
}

.meta-label {
  font-size: 0.75rem;
  letter-spacing: 0.08em;
  text-transform: uppercase;
  color: var(--q-dark-page, rgba(0, 0, 0, 0.45));
  margin-bottom: 0.3rem;
}

.meta-value {
  margin: 0;
  word-break: break-all;
}

.meta-tags {
  display: flex;
  flex-wrap: wrap;
  gap: 0.4rem;
}

.instances-section {
  display: flex;
  flex-direction: column;
  gap: 1rem;
}

.section-title {
  display: flex;
  justify-content: space-between;
  align-items: baseline;
  font-weight: 600;
}

.instances-grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(240px, 1fr));
  gap: 1rem;
}

.instance-card {
  border-radius: 14px;
  box-shadow: none;
  border: 1px solid rgba(0, 0, 0, 0.06);
  background: rgba(255, 255, 255, 0.9);
}

.instance-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 0.5rem;
}

.instance-title {
  font-weight: 600;
  margin-bottom: 0.25rem;
}

.instance-subtitle {
  margin: 0 0 0.75rem;
  color: var(--q-dark-page, rgba(0, 0, 0, 0.6));
}

.instance-links {
  display: flex;
  flex-wrap: wrap;
  gap: 0.4rem;
  margin-bottom: 0.75rem;
}

.instance-tags {
  display: flex;
  flex-wrap: wrap;
  gap: 0.35rem;
  margin-bottom: 0.75rem;
}

.dependency-chips {
  display: flex;
  flex-wrap: wrap;
  gap: 0.35rem;
}

.dependency-placeholder {
  margin: 0;
}

.empty-state {
  grid-column: 1 / -1;
  text-align: center;
  padding: 2.5rem;
  border: 1px dashed rgba(0, 0, 0, 0.15);
  border-radius: 14px;
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
  align-items: center;
}

.empty-results {
  grid-column: 1 / -1;
  padding: 3rem;
  text-align: center;
  color: var(--q-dark-page, rgba(0, 0, 0, 0.6));
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
  align-items: center;
}

.loading-state {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 0.75rem;
  padding: 3rem 0;
  color: var(--q-dark-page, rgba(0, 0, 0, 0.6));
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
