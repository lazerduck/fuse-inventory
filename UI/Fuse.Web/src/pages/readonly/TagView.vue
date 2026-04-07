<template>
  <ReadOnlyShell
    :title="pageTitle"
    :higher="higherContext"
    :lower="lowerContext"
  >
    <!-- Loading state -->
    <div v-if="isLoading" class="tag-loading">
      <q-spinner color="primary" size="48px" />
      <p>Loading tag...</p>
    </div>

    <!-- Error state: Tag not found -->
    <div v-else-if="!tag" class="tag-error">
      <q-icon name="error_outline" size="48px" color="negative" />
      <h2>Tag Not Found</h2>
      <p>The tag with ID <code>{{ id }}</code> could not be found.</p>
      <q-btn flat label="Back to Search" icon="arrow_back" @click="goBack" />
    </div>

    <!-- Tag details -->
    <div v-else class="tag-details">
      <!-- Header -->
      <section class="detail-section">
        <h2 class="section-title">
          <q-icon name="label" size="24px" :color="getTagColorName(tag.color)" />
          {{ tag.name ?? 'Unnamed Tag' }}
        </h2>
        <p v-if="tag.description" class="section-description">
          {{ tag.description }}
        </p>
      </section>

      <!-- Color -->
      <section v-if="tag.color" class="detail-section">
        <h3 class="section-subtitle">
          <q-icon name="palette" size="20px" />
          Color
        </h3>
        <q-badge :label="tag.color" :color="getTagColorName(tag.color)" outline class="color-badge" />
      </section>

      <!-- Usage Statistics -->
      <section class="detail-section">
        <h3 class="section-subtitle">
          <q-icon name="analytics" size="20px" />
          Usage
        </h3>
        <div class="usage-stats">
          <div v-if="taggedApplications.length > 0" class="usage-item">
            <span class="usage-count">{{ taggedApplications.length }}</span>
            <span class="usage-label">Application{{ taggedApplications.length === 1 ? '' : 's' }}</span>
          </div>
          <div v-if="taggedDataStores.length > 0" class="usage-item">
            <span class="usage-count">{{ taggedDataStores.length }}</span>
            <span class="usage-label">Data Store{{ taggedDataStores.length === 1 ? '' : 's' }}</span>
          </div>
          <div v-if="taggedPlatforms.length > 0" class="usage-item">
            <span class="usage-count">{{ taggedPlatforms.length }}</span>
            <span class="usage-label">Platform{{ taggedPlatforms.length === 1 ? '' : 's' }}</span>
          </div>
          <div v-if="taggedExternalResources.length > 0" class="usage-item">
            <span class="usage-count">{{ taggedExternalResources.length }}</span>
            <span class="usage-label">External Resource{{ taggedExternalResources.length === 1 ? '' : 's' }}</span>
          </div>
          <div v-if="taggedRisks.length > 0" class="usage-item">
            <span class="usage-count">{{ taggedRisks.length }}</span>
            <span class="usage-label">Risk{{ taggedRisks.length === 1 ? '' : 's' }}</span>
          </div>
          <div v-if="taggedPositions.length > 0" class="usage-item">
            <span class="usage-count">{{ taggedPositions.length }}</span>
            <span class="usage-label">Position{{ taggedPositions.length === 1 ? '' : 's' }}</span>
          </div>
        </div>
      </section>

      <!-- Empty state -->
      <section v-if="allTaggedEntities.length === 0" class="detail-section empty-state">
        <q-icon name="info" size="24px" color="grey-6" />
        <p class="empty-message">This tag is not currently applied to any entities.</p>
      </section>
    </div>
  </ReadOnlyShell>
</template>

<script setup lang="ts">
import { useRoute, useRouter } from 'vue-router'
import { computed } from 'vue'
import ReadOnlyShell from '../../components/readonly/ReadOnlyShell.vue'
import type { HigherItem, LowerItem } from '../../types/readonly'
import { useTags } from '../../composables/useTags'
import { useApplications } from '../../composables/useApplications'
import { useDataStores } from '../../composables/useDataStores'
import { usePlatforms } from '../../composables/usePlatforms'
import { useExternalResources } from '../../composables/useExternalResources'
import { useRisks } from '../../composables/useRisks'
import { usePositions } from '../../composables/usePositions'
import { useEnvironments } from '../../composables/useEnvironments'
import type { TagColor } from 'api/client'

const route = useRoute()
const router = useRouter()
const id = computed(() => route.params.id as string)

// Data queries
const { data: tagsData, isLoading: tagsLoading } = useTags()
const { data: applicationsData, isLoading: appsLoading } = useApplications()
const { data: dataStoresData, isLoading: dataStoresLoading } = useDataStores()
const { data: platformsData, isLoading: platformsLoading } = usePlatforms()
const { data: externalResourcesData, isLoading: externalLoading } = useExternalResources()
const { risks, risksLoading } = useRisks()
const { data: positionsData, isLoading: positionsLoading } = usePositions()
const { lookup: environmentLookup } = useEnvironments()

const isLoading = computed(() =>
  tagsLoading.value ||
  appsLoading.value ||
  dataStoresLoading.value ||
  platformsLoading.value ||
  externalLoading.value ||
  risksLoading.value ||
  positionsLoading.value
)

// Find the tag by ID
const tag = computed(() => {
  if (!tagsData.value) return null
  return tagsData.value.find((t) => t.id === id.value) ?? null
})

// Page title
const pageTitle = computed(() => {
  const name = tag.value?.name ?? id.value
  return `Tag: ${name}`
})

// Find all entities tagged with this tag
const taggedApplications = computed(() => {
  if (!applicationsData.value || !id.value) return []
  return applicationsData.value.filter((app) =>
    app.tagIds?.includes(id.value)
  )
})

const taggedDataStores = computed(() => {
  if (!dataStoresData.value || !id.value) return []
  return dataStoresData.value.filter((ds) =>
    ds.tagIds?.includes(id.value)
  )
})

const taggedPlatforms = computed(() => {
  if (!platformsData.value || !id.value) return []
  return platformsData.value.filter((p) =>
    p.tagIds?.includes(id.value)
  )
})

const taggedExternalResources = computed(() => {
  if (!externalResourcesData.value || !id.value) return []
  return externalResourcesData.value.filter((er) =>
    er.tagIds?.includes(id.value)
  )
})

const taggedRisks = computed(() => {
  if (!risks.value || !Array.isArray(risks.value) || !id.value) return []
  return risks.value.filter((risk: any) =>
    risk.tagIds?.includes(id.value)
  )
})

const taggedPositions = computed(() => {
  if (!positionsData.value || !id.value) return []
  return positionsData.value.filter((pos) =>
    pos.tagIds?.includes(id.value)
  )
})

const allTaggedEntities = computed(() => [
  ...taggedApplications.value,
  ...taggedDataStores.value,
  ...taggedPlatforms.value,
  ...taggedExternalResources.value,
  ...taggedRisks.value,
  ...taggedPositions.value
])

// Higher context: Empty for tags (no parent entities)
const higherContext = computed<HigherItem[]>(() => [])

// Lower context: All entities using this tag
const lowerContext = computed<LowerItem[]>(() => {
  const items: LowerItem[] = []

  // Applications
  for (const app of taggedApplications.value) {
    if (!app.id) continue
    items.push({
      id: app.id,
      type: 'app',
      name: app.name ?? 'Unnamed Application',
      route: `/view/app/${app.id}`,
      subtitle: app.description ?? undefined
    })
  }

  // Data Stores
  for (const ds of taggedDataStores.value) {
    if (!ds.id) continue
    const envName = environmentLookup.value[ds.environmentId ?? ''] ?? undefined
    items.push({
      id: ds.id,
      type: 'datastore',
      name: ds.name ?? 'Unnamed Data Store',
      route: `/view/datastore/${ds.id}`,
      subtitle: envName ? `${ds.kind ?? 'Data Store'} in ${envName}` : ds.kind ?? undefined
    })
  }

  // Platforms
  for (const platform of taggedPlatforms.value) {
    if (!platform.id) continue
    items.push({
      id: platform.id,
      type: 'platform',
      name: platform.displayName ?? platform.dnsName ?? 'Unnamed Platform',
      route: `/view/platform/${platform.id}`,
      subtitle: platform.kind ?? undefined
    })
  }

  // External Resources
  for (const resource of taggedExternalResources.value) {
    if (!resource.id) continue
    items.push({
      id: resource.id,
      type: 'external',
      name: resource.name ?? 'Unnamed External Resource',
      route: `/view/external/${resource.id}`,
      subtitle: resource.description ?? undefined
    })
  }

  // Risks
  for (const risk of taggedRisks.value) {
    if (!risk.id) continue
    items.push({
      id: risk.id,
      type: 'risk',
      name: risk.title ?? 'Unnamed Risk',
      route: `/view/risk/${risk.id}`,
      subtitle: `${risk.impact} Impact`
    })
  }

  // Positions
  for (const position of taggedPositions.value) {
    if (!position.id) continue
    items.push({
      id: position.id,
      type: 'position',
      name: position.name ?? 'Unnamed Position',
      route: `/view/position/${position.id}`,
      subtitle: position.description ?? undefined
    })
  }

  return items
})

function getTagColorName(color: TagColor | undefined): string {
  if (!color) return 'grey'
  const colorMap: Record<string, string> = {
    Red: 'red',
    Green: 'green',
    Blue: 'blue',
    Yellow: 'yellow-8',
    Purple: 'purple',
    Orange: 'orange',
    Teal: 'teal',
    Gray: 'grey'
  }
  return colorMap[color] ?? 'grey'
}

function goBack() {
  router.push('/view')
}
</script>

<style scoped>
.tag-loading,
.tag-error {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  text-align: center;
  padding: 3rem 1rem;
  gap: 1rem;
}

.tag-error h2 {
  margin: 0;
  font-size: 1.5rem;
  font-weight: 600;
}

.tag-error p {
  margin: 0;
  color: var(--fuse-text-muted);
}

.tag-error code {
  background: var(--fuse-panel-bg);
  padding: 0.125rem 0.5rem;
  border-radius: 4px;
  font-family: monospace;
}

.tag-details {
  display: flex;
  flex-direction: column;
  gap: 1.5rem;
}

.detail-section {
  padding-bottom: 1rem;
  border-bottom: 1px solid var(--fuse-panel-border);
}

.detail-section:last-child {
  border-bottom: none;
}

.section-title {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  margin: 0 0 0.5rem 0;
  font-size: 1.5rem;
  font-weight: 600;
}

.section-description {
  margin: 0;
  color: var(--fuse-text-muted);
  font-size: 0.95rem;
}

.section-subtitle {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  margin: 0 0 0.75rem 0;
  font-size: 1rem;
  font-weight: 600;
  color: var(--fuse-text-muted);
}

.color-badge {
  font-size: 0.9rem;
}

.usage-stats {
  display: flex;
  gap: 1.5rem;
  flex-wrap: wrap;
}

.usage-item {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 0.25rem;
  padding: 0.75rem 1rem;
  background: var(--fuse-panel-bg);
  border-radius: 6px;
}

.usage-count {
  font-size: 1.5rem;
  font-weight: 600;
  color: var(--q-primary);
}

.usage-label {
  font-size: 0.85rem;
  color: var(--fuse-text-muted);
  text-transform: uppercase;
  letter-spacing: 0.5px;
}

.empty-state {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 0.75rem;
  padding: 2rem 1rem;
  text-align: center;
}

.empty-message {
  margin: 0;
  color: var(--fuse-text-muted);
}
</style>
