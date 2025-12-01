<template>
  <ReadOnlyShell
    :title="pageTitle"
    :higher="higherContext"
    :lower="lowerContext"
  >
    <!-- Loading state -->
    <div v-if="isLoading" class="platform-loading">
      <q-spinner color="primary" size="48px" />
      <p>Loading platform...</p>
    </div>

    <!-- Error state: Platform not found -->
    <div v-else-if="!platform" class="platform-error">
      <q-icon name="error_outline" size="48px" color="negative" />
      <h2>Platform Not Found</h2>
      <p>The platform with ID <code>{{ id }}</code> could not be found.</p>
      <q-btn flat label="Back to Search" icon="arrow_back" @click="goBack" />
    </div>

    <!-- Platform details -->
    <div v-else class="platform-details">
      <!-- Header -->
      <section class="detail-section">
        <h2 class="section-title">
          <q-icon name="computer" size="24px" color="primary" />
          {{ platform.displayName ?? platform.dnsName ?? 'Unnamed Platform' }}
        </h2>
      </section>

      <!-- Kind / Type -->
      <section v-if="platform.kind" class="detail-section">
        <h3 class="section-subtitle">
          <q-icon name="category" size="20px" />
          Type
        </h3>
        <q-badge :label="platformKindLabel" color="primary" outline class="kind-badge" />
      </section>

      <!-- DNS Name -->
      <section v-if="platform.dnsName" class="detail-section">
        <h3 class="section-subtitle">
          <q-icon name="dns" size="20px" />
          DNS Name
        </h3>
        <p class="section-value">{{ platform.dnsName }}</p>
      </section>

      <!-- IP Address -->
      <section v-if="platform.ipAddress" class="detail-section">
        <h3 class="section-subtitle">
          <q-icon name="location_on" size="20px" />
          IP Address
        </h3>
        <p class="section-value">{{ platform.ipAddress }}</p>
      </section>

      <!-- Operating System -->
      <section v-if="platform.os" class="detail-section">
        <h3 class="section-subtitle">
          <q-icon name="terminal" size="20px" />
          Operating System
        </h3>
        <p class="section-value">{{ platform.os }}</p>
      </section>

      <!-- Tags -->
      <section v-if="resolvedTags.length > 0" class="detail-section">
        <h3 class="section-subtitle">
          <q-icon name="label" size="20px" />
          Tags
        </h3>
        <div class="tags-container">
          <q-badge
            v-for="tag in resolvedTags"
            :key="tag.id"
            :label="tag.name"
            :color="getTagColor(tag.color)"
            outline
            class="tag-badge"
          />
        </div>
      </section>

      <!-- Notes -->
      <section v-if="platform.notes" class="detail-section">
        <h3 class="section-subtitle">
          <q-icon name="notes" size="20px" />
          Notes
        </h3>
        <p class="section-notes">{{ platform.notes }}</p>
      </section>

      <!-- Empty state for lower context -->
      <section v-if="hostedInstances.length === 0" class="detail-section empty-state">
        <q-icon name="info" size="24px" color="grey-6" />
        <p class="empty-message">No instances are associated with this platform.</p>
      </section>
    </div>
  </ReadOnlyShell>
</template>

<script setup lang="ts">
import { useRoute, useRouter } from 'vue-router'
import { computed } from 'vue'
import ReadOnlyShell from '../../components/readonly/ReadOnlyShell.vue'
import type { HigherItem, LowerItem } from '../../types/readonly'
import { usePlatforms } from '../../composables/usePlatforms'
import { useApplications } from '../../composables/useApplications'
import { useEnvironments } from '../../composables/useEnvironments'
import { useTags } from '../../composables/useTags'
import { PlatformKind, type TagColor } from '../../api/client'

const route = useRoute()
const router = useRouter()
const id = computed(() => route.params.id as string)

// Data queries
const { data: platformsData, isLoading: platformsLoading } = usePlatforms()
const { data: applicationsData, isLoading: appsLoading } = useApplications()
const { lookup: environmentLookup, isLoading: envsLoading } = useEnvironments()
const { tagInfoLookup, isLoading: tagsLoading } = useTags()

const isLoading = computed(() =>
  platformsLoading.value || appsLoading.value || envsLoading.value || tagsLoading.value
)

// Find the platform by ID
const platform = computed(() => {
  if (!platformsData.value) return null
  return platformsData.value.find((p) => p.id === id.value) ?? null
})

// Page title
const pageTitle = computed(() => {
  const name = platform.value?.displayName ?? platform.value?.dnsName ?? id.value
  return `Platform: ${name}`
})

// Get user-friendly label for platform kind
const platformKindLabel = computed(() => {
  const kind = platform.value?.kind
  if (!kind) return 'Unknown'
  const labels: Record<PlatformKind, string> = {
    [PlatformKind.Server]: 'Server',
    [PlatformKind.Cluster]: 'Cluster',
    [PlatformKind.Serverless]: 'Serverless',
    [PlatformKind.ContainerHost]: 'Container Host'
  }
  return labels[kind] ?? kind
})

// Resolved tags with name and color
const resolvedTags = computed(() => {
  const tagIds = platform.value?.tagIds ?? []
  return tagIds
    .map((tagId) => {
      const info = tagInfoLookup.value[tagId]
      return info ? { id: tagId, name: info.name, color: info.color } : null
    })
    .filter((tag): tag is { id: string; name: string; color: TagColor | undefined } => tag !== null)
})

// Map TagColor enum to Quasar color
function getTagColor(color: TagColor | undefined): string {
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

// Instances hosted on this platform
const hostedInstances = computed(() => {
  if (!applicationsData.value || !id.value) return []

  const instances: Array<{
    instanceId: string
    instanceName: string
    appId: string
    appName: string
    envName: string
  }> = []

  for (const app of applicationsData.value) {
    for (const instance of app.instances ?? []) {
      if (instance.platformId === id.value && instance.id) {
        const envName = environmentLookup.value[instance.environmentId ?? ''] ?? 'Unknown'
        instances.push({
          instanceId: instance.id,
          instanceName: `${app.name ?? 'App'} — ${envName}`,
          appId: app.id ?? '',
          appName: app.name ?? 'App',
          envName
        })
      }
    }
  }

  return instances
})

// Higher context: Empty for platforms (no parent entities)
const higherContext = computed<HigherItem[]>(() => [])

// Lower context: Hosted instances
const lowerContext = computed<LowerItem[]>(() => {
  return hostedInstances.value.map((inst) => ({
    id: inst.instanceId,
    type: 'instance',
    name: inst.instanceName,
    route: `/view/instance/${inst.instanceId}`,
    subtitle: `${inst.appName} (${inst.envName})`
  } as LowerItem))
})

function goBack() {
  router.push('/view')
}
</script>

<style scoped>
.platform-loading,
.platform-error {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  text-align: center;
  padding: 3rem 1rem;
  gap: 1rem;
}

.platform-error h2 {
  margin: 0;
  font-size: 1.5rem;
  font-weight: 600;
}

.platform-error p {
  margin: 0;
  color: var(--fuse-text-muted);
}

.platform-error code {
  background: var(--fuse-panel-bg);
  padding: 0.125rem 0.5rem;
  border-radius: 4px;
  font-family: monospace;
}

.platform-details {
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

.section-subtitle {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  margin: 0 0 0.75rem 0;
  font-size: 1rem;
  font-weight: 600;
  color: var(--fuse-text-muted);
}

.section-value {
  margin: 0;
  font-size: 1rem;
}

.section-notes {
  margin: 0;
  white-space: pre-wrap;
  color: var(--fuse-text-secondary);
}

.kind-badge {
  font-size: 0.85rem;
}

.tags-container {
  display: flex;
  flex-wrap: wrap;
  gap: 0.5rem;
}

.tag-badge {
  font-size: 0.8rem;
}

.empty-state {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 0.5rem;
  padding: 2rem 1rem;
  text-align: center;
}

.empty-message {
  margin: 0;
  color: var(--fuse-text-muted);
  font-size: 0.9rem;
}
</style>
