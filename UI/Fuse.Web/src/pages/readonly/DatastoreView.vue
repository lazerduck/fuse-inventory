<template>
  <ReadOnlyShell
    :title="pageTitle"
    :higher="higherContext"
    :lower="lowerContext"
  >
    <!-- Loading state -->
    <div v-if="isLoading" class="datastore-loading">
      <q-spinner color="primary" size="48px" />
      <p>Loading datastore...</p>
    </div>

    <!-- Error state: Datastore not found -->
    <div v-else-if="!datastore" class="datastore-error">
      <q-icon name="error_outline" size="48px" color="negative" />
      <h2>Datastore Not Found</h2>
      <p>The datastore with ID <code>{{ id }}</code> could not be found.</p>
      <q-btn flat label="Back to Search" icon="arrow_back" @click="goBack" />
    </div>

    <!-- Datastore details -->
    <div v-else class="datastore-details">
      <!-- Header -->
      <section class="detail-section">
        <h2 class="section-title">
          <q-icon name="storage" size="24px" color="primary" />
          {{ datastore.name ?? 'Unnamed Datastore' }}
        </h2>
        <p v-if="datastore.description" class="section-description">
          {{ datastore.description }}
        </p>
      </section>

      <!-- Datastore Type / Kind -->
      <section v-if="datastore.kind" class="detail-section">
        <h3 class="section-subtitle">
          <q-icon name="category" size="20px" />
          Type
        </h3>
        <q-badge :label="datastore.kind" color="primary" outline class="kind-badge" />
      </section>

      <!-- Environment -->
      <section v-if="environment" class="detail-section">
        <h3 class="section-subtitle">
          <q-icon name="cloud" size="20px" />
          Environment
        </h3>
        <q-badge :label="environment.name ?? 'Unknown'" color="secondary" outline class="env-badge" />
      </section>

      <!-- Connection Info -->
      <section v-if="datastore.connectionUri" class="detail-section">
        <h3 class="section-subtitle">
          <q-icon name="link" size="20px" />
          Connection
        </h3>
        <p class="connection-value">{{ datastore.connectionUri }}</p>
      </section>

      <!-- Platform -->
      <section v-if="platformName" class="detail-section">
        <h3 class="section-subtitle">
          <q-icon name="computer" size="20px" />
          Platform
        </h3>
        <router-link
          v-if="datastore.platformId"
          :to="`/view/platform/${datastore.platformId}`"
          class="platform-link"
        >
          {{ platformName }}
          <q-icon name="chevron_right" size="16px" />
        </router-link>
        <p v-else class="section-value">{{ platformName }}</p>
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
    </div>
  </ReadOnlyShell>
</template>

<script setup lang="ts">
import { useRoute, useRouter } from 'vue-router'
import { computed } from 'vue'
import ReadOnlyShell from '../../components/readonly/ReadOnlyShell.vue'
import type { HigherItem, LowerItem } from '../../types/readonly'
import { useDataStores } from '../../composables/useDataStores'
import { useEnvironments } from '../../composables/useEnvironments'
import { usePlatforms } from '../../composables/usePlatforms'
import { useAccounts } from '../../composables/useAccounts'
import { useApplications } from '../../composables/useApplications'
import { useTags } from '../../composables/useTags'
import { TargetKind, type TagColor } from '../../api/client'

const route = useRoute()
const router = useRouter()
const id = computed(() => route.params.id as string)

// Data queries
const { data: dataStoresData, isLoading: dataStoresLoading } = useDataStores()
const { data: environmentsData, lookup: environmentLookup, isLoading: envsLoading } = useEnvironments()
const { lookup: platformLookup, isLoading: platformsLoading } = usePlatforms()
const { data: accountsData, isLoading: accountsLoading } = useAccounts()
const { data: applicationsData, isLoading: appsLoading } = useApplications()
const { tagInfoLookup, isLoading: tagsLoading } = useTags()

const isLoading = computed(() =>
  dataStoresLoading.value || envsLoading.value || platformsLoading.value ||
  accountsLoading.value || appsLoading.value || tagsLoading.value
)

// Find the datastore by ID
const datastore = computed(() => {
  if (!dataStoresData.value) return null
  return dataStoresData.value.find((ds) => ds.id === id.value) ?? null
})

// Environment info
const environment = computed(() => {
  const envId = datastore.value?.environmentId
  if (!envId || !environmentsData.value) return null
  return environmentsData.value.find((env) => env.id === envId) ?? null
})

// Platform name
const platformName = computed(() => {
  const platformId = datastore.value?.platformId
  if (!platformId) return null
  return platformLookup.value[platformId] ?? null
})

// Page title
const pageTitle = computed(() => {
  const name = datastore.value?.name ?? id.value
  return `Data Store: ${name}`
})

// Resolved tags with name and color
const resolvedTags = computed(() => {
  const tagIds = datastore.value?.tagIds ?? []
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

// Higher context: Environment
const higherContext = computed<HigherItem[]>(() => {
  const items: HigherItem[] = []

  // Environment (non-clickable for now)
  if (environment.value?.id) {
    items.push({
      id: environment.value.id,
      type: 'environment',
      name: environment.value.name ?? 'Environment',
      // No route - environments are not clickable yet
      subtitle: environment.value.description ?? undefined
    })
  }

  return items
})

// Accounts that belong to this datastore
const datastoreAccounts = computed(() => {
  if (!accountsData.value || !id.value) return []
  return accountsData.value.filter(
    (account) => account.targetKind === TargetKind.DataStore && account.targetId === id.value
  )
})

// Dependencies that target this datastore (with their owning instances/apps)
const datastoreDependencies = computed(() => {
  if (!applicationsData.value || !id.value) return []

  const deps: Array<{
    depId: string
    depName: string
    instanceName: string
    appName: string
    envName: string
  }> = []

  for (const app of applicationsData.value) {
    for (const instance of app.instances ?? []) {
      for (const dep of instance.dependencies ?? []) {
        if (dep.targetKind === TargetKind.DataStore && dep.targetId === id.value && dep.id) {
          const envName = environmentLookup.value[instance.environmentId ?? ''] ?? 'Unknown'
          deps.push({
            depId: dep.id,
            depName: `${app.name ?? 'App'} → ${datastore.value?.name ?? 'Datastore'}`,
            instanceName: `${app.name ?? 'App'} — ${envName}`,
            appName: app.name ?? 'App',
            envName
          })
        }
      }
    }
  }

  return deps
})

// Lower context: Accounts + Dependencies
const lowerContext = computed<LowerItem[]>(() => {
  const items: LowerItem[] = []

  // Add accounts
  for (const account of datastoreAccounts.value) {
    if (!account.id) continue
    items.push({
      id: account.id,
      type: 'account',
      name: account.userName ?? account.id,
      route: `/view/account/${account.id}`,
      subtitle: account.authKind ?? 'Account'
    })
  }

  // Add dependencies
  for (const dep of datastoreDependencies.value) {
    items.push({
      id: dep.depId,
      type: 'dependency',
      name: dep.depName,
      route: `/view/dependency/${dep.depId}`,
      subtitle: `Used by ${dep.instanceName}`
    })
  }

  return items
})

function goBack() {
  router.push('/view')
}
</script>

<style scoped>
.datastore-loading,
.datastore-error {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  text-align: center;
  padding: 3rem 1rem;
  gap: 1rem;
}

.datastore-error h2 {
  margin: 0;
  font-size: 1.5rem;
  font-weight: 600;
}

.datastore-error p {
  margin: 0;
  color: var(--fuse-text-muted);
}

.datastore-error code {
  background: var(--fuse-panel-bg);
  padding: 0.125rem 0.5rem;
  border-radius: 4px;
  font-family: monospace;
}

.datastore-details {
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

.connection-value {
  margin: 0;
  font-family: monospace;
  font-size: 0.9rem;
  word-break: break-all;
  color: var(--fuse-text-secondary);
}

.kind-badge,
.env-badge {
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

.platform-link {
  display: inline-flex;
  align-items: center;
  gap: 0.25rem;
  color: var(--q-primary);
  text-decoration: none;
  font-size: 1rem;
}

.platform-link:hover {
  text-decoration: underline;
}
</style>
