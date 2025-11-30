<template>
  <ReadOnlyShell
    :title="pageTitle"
    :higher="higherContext"
    :lower="lowerContext"
  >
    <!-- Loading state -->
    <div v-if="isLoading" class="app-loading">
      <q-spinner color="primary" size="48px" />
      <p>Loading application...</p>
    </div>

    <!-- Error state: App not found -->
    <div v-else-if="!application" class="app-error">
      <q-icon name="error_outline" size="48px" color="negative" />
      <h2>Application Not Found</h2>
      <p>The application with ID <code>{{ id }}</code> could not be found.</p>
      <q-btn flat label="Back to Search" icon="arrow_back" @click="goBack" />
    </div>

    <!-- Application details -->
    <div v-else class="app-details">
      <!-- Header -->
      <section class="detail-section">
        <h2 class="section-title">
          <q-icon name="apps" size="24px" color="primary" />
          {{ application.name ?? 'Unnamed Application' }}
        </h2>
        <p v-if="application.description" class="section-description">
          {{ application.description }}
        </p>
      </section>

      <!-- Repository URL -->
      <section v-if="application.repositoryUri" class="detail-section">
        <h3 class="section-subtitle">
          <q-icon name="code" size="20px" />
          Repository
        </h3>
        <a :href="application.repositoryUri" target="_blank" rel="noopener" class="repo-link">
          {{ application.repositoryUri }}
          <q-icon name="open_in_new" size="14px" />
        </a>
      </section>

      <!-- Owner -->
      <section v-if="application.owner" class="detail-section">
        <h3 class="section-subtitle">
          <q-icon name="person" size="20px" />
          Owner
        </h3>
        <p class="section-value">{{ application.owner }}</p>
      </section>

      <!-- Framework -->
      <section v-if="application.framework" class="detail-section">
        <h3 class="section-subtitle">
          <q-icon name="build" size="20px" />
          Framework
        </h3>
        <p class="section-value">{{ application.framework }}</p>
      </section>

      <!-- Version -->
      <section v-if="application.version" class="detail-section">
        <h3 class="section-subtitle">
          <q-icon name="new_releases" size="20px" />
          Version
        </h3>
        <p class="section-value">{{ application.version }}</p>
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
      <section v-if="application.notes" class="detail-section">
        <h3 class="section-subtitle">
          <q-icon name="notes" size="20px" />
          Notes
        </h3>
        <p class="section-notes">{{ application.notes }}</p>
      </section>

      <!-- Pipelines -->
      <section v-if="application.pipelines && application.pipelines.length > 0" class="detail-section">
        <h3 class="section-subtitle">
          <q-icon name="account_tree" size="20px" />
          Pipelines
        </h3>
        <div class="pipeline-list">
          <div v-for="pipeline in application.pipelines" :key="pipeline.id" class="pipeline-item">
            <span class="pipeline-name">{{ pipeline.name ?? 'Unnamed Pipeline' }}</span>
            <a
              v-if="pipeline.pipelineUri"
              :href="pipeline.pipelineUri"
              target="_blank"
              rel="noopener"
              class="pipeline-link"
            >
              {{ pipeline.pipelineUri }}
              <q-icon name="open_in_new" size="14px" />
            </a>
          </div>
        </div>
      </section>
    </div>
  </ReadOnlyShell>
</template>

<script setup lang="ts">
import { useRoute, useRouter } from 'vue-router'
import { computed } from 'vue'
import ReadOnlyShell from '../../components/readonly/ReadOnlyShell.vue'
import type { HigherItem, LowerItem } from '../../types/readonly'
import { useApplications } from '../../composables/useApplications'
import { useEnvironments } from '../../composables/useEnvironments'
import { useTags } from '../../composables/useTags'
import type { TagColor } from '../../api/client'

const route = useRoute()
const router = useRouter()
const id = computed(() => route.params.id as string)

// Data queries
const { data: applicationsData, isLoading: appsLoading } = useApplications()
const { data: environmentsData, lookup: environmentLookup, isLoading: envsLoading } = useEnvironments()
const { tagInfoLookup, isLoading: tagsLoading } = useTags()

const isLoading = computed(() => appsLoading.value || envsLoading.value || tagsLoading.value)

// Find the application by ID
const application = computed(() => {
  if (!applicationsData.value) return null
  return applicationsData.value.find((app) => app.id === id.value) ?? null
})

// Page title
const pageTitle = computed(() => {
  const name = application.value?.name ?? id.value
  return `Application: ${name}`
})

// Resolved tags with name and color
const resolvedTags = computed(() => {
  const tagIds = application.value?.tagIds ?? []
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

// Higher context: Empty for applications (no parent entities)
const higherContext = computed<HigherItem[]>(() => [])

// Lower context: Instances sorted by environment
const lowerContext = computed<LowerItem[]>(() => {
  const instances = application.value?.instances ?? []
  
  if (instances.length === 0) return []
  
  // Get environment order from environmentsData
  const envOrder = new Map<string, number>()
  if (environmentsData.value) {
    environmentsData.value.forEach((env, index) => {
      if (env.id) {
        envOrder.set(env.id, index)
      }
    })
  }
  
  // Sort instances by environment order
  const sortedInstances = [...instances].sort((a, b) => {
    const orderA = envOrder.get(a.environmentId ?? '') ?? 999
    const orderB = envOrder.get(b.environmentId ?? '') ?? 999
    return orderA - orderB
  })
  
  const appName = application.value?.name ?? 'App'
  return sortedInstances.map((instance) => {
    const envName = environmentLookup.value[instance.environmentId ?? ''] ?? 'Unknown'
    return {
      id: instance.id ?? '',
      type: 'instance',
      name: `${appName} — ${envName}`,
      route: `/view/instance/${instance.id}`,
      subtitle: envName
    } as LowerItem
  })
})

function goBack() {
  router.push('/view')
}
</script>

<style scoped>
.app-loading,
.app-error {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  text-align: center;
  padding: 3rem 1rem;
  gap: 1rem;
}

.app-error h2 {
  margin: 0;
  font-size: 1.5rem;
  font-weight: 600;
}

.app-error p {
  margin: 0;
  color: var(--fuse-text-muted);
}

.app-error code {
  background: var(--fuse-panel-bg);
  padding: 0.125rem 0.5rem;
  border-radius: 4px;
  font-family: monospace;
}

.app-details {
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

.section-value {
  margin: 0;
  font-size: 1rem;
}

.section-notes {
  margin: 0;
  white-space: pre-wrap;
  color: var(--fuse-text-secondary);
}

.repo-link {
  display: inline-flex;
  align-items: center;
  gap: 0.25rem;
  color: var(--q-primary);
  text-decoration: none;
  word-break: break-all;
}

.repo-link:hover {
  text-decoration: underline;
}

.tags-container {
  display: flex;
  flex-wrap: wrap;
  gap: 0.5rem;
}

.tag-badge {
  font-size: 0.8rem;
}

.pipeline-list {
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
}

.pipeline-item {
  display: flex;
  flex-direction: column;
  gap: 0.25rem;
}

.pipeline-name {
  font-weight: 500;
}

.pipeline-link {
  display: inline-flex;
  align-items: center;
  gap: 0.25rem;
  color: var(--q-primary);
  text-decoration: none;
  font-size: 0.9rem;
  word-break: break-all;
}

.pipeline-link:hover {
  text-decoration: underline;
}
</style>
