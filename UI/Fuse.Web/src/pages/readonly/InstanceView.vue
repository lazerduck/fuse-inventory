<template>
  <ReadOnlyShell
    :title="pageTitle"
    :higher="higherContext"
    :lower="lowerContext"
  >
    <!-- Loading state -->
    <div v-if="isLoading" class="instance-loading">
      <q-spinner color="primary" size="48px" />
      <p>Loading instance...</p>
    </div>

    <!-- Error state: Instance not found -->
    <div v-else-if="!instance" class="instance-error">
      <q-icon name="error_outline" size="48px" color="negative" />
      <h2>Instance Not Found</h2>
      <p>The instance with ID <code>{{ id }}</code> could not be found.</p>
      <q-btn flat label="Back to Search" icon="arrow_back" @click="goBack" />
    </div>

    <!-- Instance details -->
    <div v-else class="instance-details">
      <!-- Header -->
      <section class="detail-section">
        <h2 class="section-title">
          <q-icon name="layers" size="24px" color="primary" />
          {{ instanceDisplayName }}
        </h2>
        <p v-if="application?.description" class="section-description">
          {{ application.description }}
        </p>
      </section>

      <!-- URLs -->
      <section v-if="hasUrls" class="detail-section">
        <h3 class="section-subtitle">
          <q-icon name="link" size="20px" />
          URLs
        </h3>
        <div class="url-list">
          <div v-if="instance.baseUri" class="url-item">
            <span class="url-label">Base URL</span>
            <a :href="instance.baseUri" target="_blank" rel="noopener" class="url-value">
              {{ instance.baseUri }}
              <q-icon name="open_in_new" size="14px" />
            </a>
          </div>
          <div v-if="instance.healthUri" class="url-item">
            <span class="url-label">Health URL</span>
            <a :href="instance.healthUri" target="_blank" rel="noopener" class="url-value">
              {{ instance.healthUri }}
              <q-icon name="open_in_new" size="14px" />
            </a>
          </div>
          <div v-if="instance.openApiUri" class="url-item">
            <span class="url-label">OpenAPI URL</span>
            <a :href="instance.openApiUri" target="_blank" rel="noopener" class="url-value">
              {{ instance.openApiUri }}
              <q-icon name="open_in_new" size="14px" />
            </a>
          </div>
        </div>
      </section>

      <!-- Platform -->
      <section v-if="platformName" class="detail-section">
        <h3 class="section-subtitle">
          <q-icon name="computer" size="20px" />
          Platform
        </h3>
        <p class="section-value">{{ platformName }}</p>
      </section>

      <!-- Version -->
      <section v-if="instance.version" class="detail-section">
        <h3 class="section-subtitle">
          <q-icon name="new_releases" size="20px" />
          Version
        </h3>
        <p class="section-value">{{ instance.version }}</p>
      </section>

      <!-- Identities -->
      <section class="detail-section">
        <h3 class="section-subtitle">
          <q-icon name="badge" size="20px" />
          Identities
        </h3>
        <div v-if="ownedIdentities.length === 0" class="empty-value">
          No identities configured
        </div>
        <q-list v-else separator class="identity-list">
          <q-item
            v-for="identity in ownedIdentities"
            :key="identity.id"
            clickable
            @click="identity.id && navigateToIdentity(identity.id)"
          >
            <q-item-section avatar>
              <q-icon name="badge" color="primary" />
            </q-item-section>
            <q-item-section>
              <q-item-label>{{ identity.name ?? identity.id }}</q-item-label>
              <q-item-label v-if="identity.kind" caption>{{ identity.kind }}</q-item-label>
            </q-item-section>
            <q-item-section side>
              <q-icon name="chevron_right" color="grey-6" />
            </q-item-section>
          </q-item>
        </q-list>
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
      <section v-if="application?.notes" class="detail-section">
        <h3 class="section-subtitle">
          <q-icon name="notes" size="20px" />
          Notes
        </h3>
        <p class="section-notes">{{ application.notes }}</p>
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
import { usePlatforms } from '../../composables/usePlatforms'
import { useIdentities } from '../../composables/useIdentities'
import { useAccounts } from '../../composables/useAccounts'
import { useDataStores } from '../../composables/useDataStores'
import { useExternalResources } from '../../composables/useExternalResources'
import { useTags } from '../../composables/useTags'
import { DependencyAuthKind, TargetKind, type TagColor } from '../../api/client'

const route = useRoute()
const router = useRouter()
const id = computed(() => route.params.id as string)

// Data queries
const { data: applicationsData, isLoading: appsLoading } = useApplications()
const { data: environmentsData, lookup: environmentLookup, isLoading: envsLoading } = useEnvironments()
const { lookup: platformLookup, isLoading: platformsLoading } = usePlatforms()
const { data: identitiesData, isLoading: identitiesLoading } = useIdentities()
const { lookup: accountLookup, isLoading: accountsLoading } = useAccounts()
const { data: dataStoresData, isLoading: dataStoresLoading } = useDataStores()
const { data: externalResourcesData, isLoading: externalLoading } = useExternalResources()
const { tagInfoLookup, isLoading: tagsLoading } = useTags()

const isLoading = computed(() => 
  appsLoading.value || envsLoading.value || platformsLoading.value || 
  identitiesLoading.value || accountsLoading.value || dataStoresLoading.value || 
  externalLoading.value || tagsLoading.value
)

// Find the application and instance by instance ID
const applicationAndInstance = computed(() => {
  if (!applicationsData.value) return null
  
  for (const app of applicationsData.value) {
    const inst = app.instances?.find((i) => i.id === id.value)
    if (inst) {
      return { application: app, instance: inst }
    }
  }
  return null
})

const application = computed(() => applicationAndInstance.value?.application)
const instance = computed(() => applicationAndInstance.value?.instance)

// Environment info
const environment = computed(() => {
  const envId = instance.value?.environmentId
  if (!envId || !environmentsData.value) return null
  return environmentsData.value.find((env) => env.id === envId) ?? null
})

// Platform name
const platformName = computed(() => {
  const platformId = instance.value?.platformId
  if (!platformId) return null
  return platformLookup.value[platformId] ?? null
})

// Instance display name (app name + environment name)
const instanceDisplayName = computed(() => {
  const appName = application.value?.name ?? 'Instance'
  const envName = environment.value?.name ?? 'Unknown Environment'
  return `${appName} — ${envName}`
})

// Page title
const pageTitle = computed(() => `Instance: ${instanceDisplayName.value}`)

// Check if any URLs are present
const hasUrls = computed(() => 
  !!(instance.value?.baseUri || instance.value?.healthUri || instance.value?.openApiUri)
)

// Identities owned by this instance
const ownedIdentities = computed(() => {
  if (!identitiesData.value || !id.value) return []
  return identitiesData.value.filter((identity) => 
    identity.id && identity.ownerInstanceId === id.value
  )
})

// Resolved tags with name and color
const resolvedTags = computed(() => {
  const tagIds = instance.value?.tagIds ?? []
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

// Higher context: App + Environment
const higherContext = computed<HigherItem[]>(() => {
  const items: HigherItem[] = []
  
  // App
  if (application.value?.id) {
    items.push({
      id: application.value.id,
      type: 'app',
      name: application.value.name ?? 'Application',
      route: `/view/app/${application.value.id}`,
      subtitle: application.value.repositoryUri ?? application.value.description ?? undefined
    })
  }
  
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

// Lower context: Dependencies
const lowerContext = computed<LowerItem[]>(() => {
  const deps = instance.value?.dependencies ?? []
  
  return deps.map((dep) => {
    const targetName = resolveTargetName(dep.targetKind, dep.targetId)
    const authInfo = resolveAuthInfo(dep.authKind, dep.accountId, dep.identityId)
    
    return {
      id: dep.id ?? '',
      type: 'dependency',
      name: targetName,
      route: `/view/dependency/${dep.id}`,
      subtitle: authInfo
    } as LowerItem
  })
})

// Resolve target name from targetKind and targetId
function resolveTargetName(targetKind: TargetKind | undefined, targetId: string | undefined): string {
  if (!targetId) return 'Unknown'
  
  switch (targetKind) {
    case TargetKind.Application: {
      // Find the instance from all applications
      if (!applicationsData.value) return targetId
      for (const app of applicationsData.value) {
        const inst = app.instances?.find((i) => i.id === targetId)
        if (inst) {
          const appName = app.name ?? 'App'
          const envName = environmentLookup.value[inst.environmentId ?? ''] ?? '—'
          return `${appName} — ${envName}`
        }
      }
      return targetId
    }
    case TargetKind.DataStore: {
      const store = dataStoresData.value?.find((ds) => ds.id === targetId)
      return store?.name ?? targetId
    }
    case TargetKind.External: {
      const ext = externalResourcesData.value?.find((er) => er.id === targetId)
      return ext?.name ?? targetId
    }
    default:
      return targetId
  }
}

// Resolve auth info string
function resolveAuthInfo(
  authKind: DependencyAuthKind | undefined, 
  accountId: string | undefined, 
  identityId: string | undefined
): string {
  switch (authKind) {
    case DependencyAuthKind.None:
      return 'auth: none'
    case DependencyAuthKind.Account:
      if (accountId) {
        const accountName = accountLookup.value[accountId] ?? accountId
        return `account: ${accountName}`
      }
      return 'auth: account'
    case DependencyAuthKind.Identity:
      if (identityId) {
        const identity = identitiesData.value?.find((i) => i.id === identityId)
        const identityName = identity?.name ?? identityId
        return `identity: ${identityName}`
      }
      return 'auth: identity'
    default:
      return ''
  }
}

function goBack() {
  router.push('/view')
}

function navigateToIdentity(identityId: string) {
  router.push(`/view/identity/${identityId}`)
}
</script>

<style scoped>
.instance-loading,
.instance-error {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  text-align: center;
  padding: 3rem 1rem;
  gap: 1rem;
}

.instance-error h2 {
  margin: 0;
  font-size: 1.5rem;
  font-weight: 600;
}

.instance-error p {
  margin: 0;
  color: var(--fuse-text-muted);
}

.instance-error code {
  background: var(--fuse-panel-bg);
  padding: 0.125rem 0.5rem;
  border-radius: 4px;
  font-family: monospace;
}

.instance-details {
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

.url-list {
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
}

.url-item {
  display: flex;
  align-items: baseline;
  gap: 0.75rem;
}

.url-label {
  font-weight: 500;
  color: var(--fuse-text-muted);
  min-width: 100px;
  flex-shrink: 0;
}

.url-value {
  display: inline-flex;
  align-items: center;
  gap: 0.25rem;
  color: var(--q-primary);
  text-decoration: none;
  word-break: break-all;
}

.url-value:hover {
  text-decoration: underline;
}

.empty-value {
  color: var(--fuse-text-muted);
  font-style: italic;
}

.identity-list {
  border: 1px solid var(--fuse-panel-border);
  border-radius: 8px;
  overflow: hidden;
}

.tags-container {
  display: flex;
  flex-wrap: wrap;
  gap: 0.5rem;
}

.tag-badge {
  font-size: 0.8rem;
}
</style>
