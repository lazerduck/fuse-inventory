<template>
  <ReadOnlyShell
    :title="pageTitle"
    :higher="higherContext"
    :lower="lowerContext"
  >
    <!-- Loading state -->
    <div v-if="isLoading" class="external-loading">
      <q-spinner color="primary" size="48px" />
      <p>Loading external resource...</p>
    </div>

    <!-- Error state: External resource not found -->
    <div v-else-if="!externalResource" class="external-error">
      <q-icon name="error_outline" size="48px" color="negative" />
      <h2>External Resource Not Found</h2>
      <p>The external resource with ID <code>{{ id }}</code> could not be found.</p>
      <q-btn flat label="Back to Search" icon="arrow_back" @click="goBack" />
    </div>

    <!-- External resource details -->
    <div v-else class="external-details">
      <!-- Header -->
      <section class="detail-section">
        <h2 class="section-title">
          <q-icon name="hub" size="24px" color="primary" />
          {{ externalResource.name ?? 'Unnamed External Resource' }}
        </h2>
        <p v-if="externalResource.description" class="section-description">
          {{ externalResource.description }}
        </p>
      </section>

      <!-- Resource URI / Base URL -->
      <section v-if="externalResource.resourceUri" class="detail-section">
        <h3 class="section-subtitle">
          <q-icon name="link" size="20px" />
          Resource URI
        </h3>
        <a :href="externalResource.resourceUri" target="_blank" rel="noopener" class="resource-link">
          {{ externalResource.resourceUri }}
          <q-icon name="open_in_new" size="14px" />
        </a>
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

      <!-- Empty states for lower context -->
      <section v-if="!hasAccounts && !hasDependencies" class="detail-section empty-state">
        <q-icon name="info" size="24px" color="grey-6" />
        <p class="empty-message">No accounts belong to this resource and nothing depends on it.</p>
      </section>
    </div>
  </ReadOnlyShell>
</template>

<script setup lang="ts">
import { useRoute, useRouter } from 'vue-router'
import { computed } from 'vue'
import ReadOnlyShell from '../../components/readonly/ReadOnlyShell.vue'
import type { HigherItem, LowerItem } from '../../types/readonly'
import { useExternalResources } from '../../composables/useExternalResources'
import { useAccounts } from '../../composables/useAccounts'
import { useApplications } from '../../composables/useApplications'
import { useTags } from '../../composables/useTags'
import { TargetKind, type TagColor } from '../../api/client'

const route = useRoute()
const router = useRouter()
const id = computed(() => route.params.id as string)

// Data queries
const { data: externalResourcesData, isLoading: resourcesLoading } = useExternalResources()
const { data: accountsData, isLoading: accountsLoading } = useAccounts()
const { data: applicationsData, isLoading: appsLoading } = useApplications()
const { tagInfoLookup, isLoading: tagsLoading } = useTags()

const isLoading = computed(() =>
  resourcesLoading.value || accountsLoading.value || appsLoading.value || tagsLoading.value
)

// Find the external resource by ID
const externalResource = computed(() => {
  if (!externalResourcesData.value) return null
  return externalResourcesData.value.find((res) => res.id === id.value) ?? null
})

// Page title
const pageTitle = computed(() => {
  const name = externalResource.value?.name ?? id.value
  return `External Resource: ${name}`
})

// Resolved tags with name and color
const resolvedTags = computed(() => {
  const tagIds = externalResource.value?.tagIds ?? []
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

// Higher context: Empty for external resources (not environment-scoped in v1)
const higherContext = computed<HigherItem[]>(() => [])

// Accounts that belong to this external resource
const externalAccounts = computed(() => {
  if (!accountsData.value || !id.value) return []
  return accountsData.value.filter(
    (account) => account.targetKind === TargetKind.External && account.targetId === id.value
  )
})

// Dependencies that target this external resource (with their owning instances/apps)
const externalDependencies = computed(() => {
  if (!applicationsData.value || !id.value) return []

  const deps: Array<{
    depId: string
    depName: string
    instanceName: string
    appName: string
  }> = []

  for (const app of applicationsData.value) {
    for (const instance of app.instances ?? []) {
      for (const dep of instance.dependencies ?? []) {
        if (dep.targetKind === TargetKind.External && dep.targetId === id.value && dep.id) {
          deps.push({
            depId: dep.id,
            depName: `${app.name ?? 'App'} → ${externalResource.value?.name ?? 'Resource'}`,
            instanceName: instance.id ?? 'Instance',
            appName: app.name ?? 'App'
          })
        }
      }
    }
  }

  return deps
})

// Check if there are accounts or dependencies
const hasAccounts = computed(() => externalAccounts.value.length > 0)
const hasDependencies = computed(() => externalDependencies.value.length > 0)

// Lower context: Accounts + Dependencies
const lowerContext = computed<LowerItem[]>(() => {
  const items: LowerItem[] = []

  // Add accounts
  for (const account of externalAccounts.value) {
    if (!account.id) continue
    items.push({
      id: account.id,
      type: 'account',
      name: account.userName ?? account.id,
      route: `/view/account/${account.id}`,
      subtitle: 'Account'
    })
  }

  // Add dependencies
  for (const dep of externalDependencies.value) {
    items.push({
      id: dep.depId,
      type: 'dependency',
      name: dep.depName,
      route: `/view/dependency/${dep.depId}`,
      subtitle: `${dep.appName} → ${dep.instanceName}`
    })
  }

  return items
})

function goBack() {
  router.push('/view')
}
</script>

<style scoped>
.external-loading,
.external-error {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  text-align: center;
  padding: 3rem 1rem;
  gap: 1rem;
}

.external-error h2 {
  margin: 0;
  font-size: 1.5rem;
  font-weight: 600;
}

.external-error p {
  margin: 0;
  color: var(--fuse-text-muted);
}

.external-error code {
  background: var(--fuse-panel-bg);
  padding: 0.125rem 0.5rem;
  border-radius: 4px;
  font-family: monospace;
}

.external-details {
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
  white-space: pre-wrap;
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

.resource-link {
  display: inline-flex;
  align-items: center;
  gap: 0.25rem;
  color: var(--q-primary);
  text-decoration: none;
  word-break: break-all;
}

.resource-link:hover {
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
