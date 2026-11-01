<template>
  <ReadOnlyShell
    :title="pageTitle"
    :higher="higherContext"
    :lower="lowerContext"
  >
    <!-- Loading state -->
    <div v-if="isLoading" class="dependency-loading">
      <q-spinner color="primary" size="48px" />
      <p>Loading dependency...</p>
    </div>

    <div v-else-if="applicationsError" class="dependency-error" role="alert">
      <q-icon name="error_outline" size="48px" color="negative" />
      <h2>Unable to load dependency</h2>
      <p>{{ getErrorMessage(applicationsError, 'The application inventory could not be loaded.') }}</p>
      <q-btn flat label="Retry" icon="refresh" @click="retryApplications()" />
    </div>

    <!-- Error state: Dependency not found -->
    <div v-else-if="!dependency" class="dependency-error">
      <q-icon name="error_outline" size="48px" color="negative" />
      <h2>Dependency Not Found</h2>
      <p>The dependency with ID <code>{{ id }}</code> could not be found.</p>
      <q-btn flat label="Back to Search" icon="arrow_back" @click="goBack" />
    </div>

    <!-- Dependency details -->
    <div v-else class="dependency-details">
      <!-- Header -->
      <section class="detail-section">
        <h2 class="section-title">
          <q-icon name="link" size="24px" color="primary" />
          {{ dependencyDisplayName }}
        </h2>
      </section>

      <!-- Target Type -->
      <section class="detail-section">
        <h3 class="section-subtitle">
          <q-icon name="category" size="20px" />
          Target Type
        </h3>
        <q-badge :label="targetTypeLabel" color="primary" outline class="type-badge" />
      </section>

      <!-- Target Summary -->
      <section class="detail-section">
        <h3 class="section-subtitle">
          <q-icon :name="targetIcon" size="20px" />
          Target
        </h3>
        <div class="target-summary">
          <router-link v-if="targetRoute" :to="targetRoute" class="target-link">
            {{ targetName }}
            <q-icon name="chevron_right" size="16px" />
          </router-link>
          <span v-else class="target-name">{{ targetName }}</span>
        </div>
      </section>

      <!-- Authentication -->
      <section class="detail-section">
        <h3 class="section-subtitle">
          <q-icon name="security" size="20px" />
          Authentication
        </h3>
        <div class="auth-info">
          <q-badge
            :label="authKindLabel"
            :color="authKindColor"
            outline
            class="auth-badge"
          />
          <router-link
            v-if="authEntityRoute"
            :to="authEntityRoute"
            class="auth-entity-link"
          >
            {{ authEntityName }}
            <q-icon name="chevron_right" size="16px" />
          </router-link>
        </div>
        <p v-if="authLookupUnavailable" class="section-description" role="status">{{ authLookupUnavailable }}</p>
      </section>

      <!-- Port (if specified) -->
      <section v-if="dependency.port" class="detail-section">
        <h3 class="section-subtitle">
          <q-icon name="settings_ethernet" size="20px" />
          Port
        </h3>
        <p class="section-value">{{ dependency.port }}</p>
      </section>

      <!-- Severity -->
      <section class="detail-section">
        <h3 class="section-subtitle">
          <q-icon name="warning" size="20px" />
          Severity
        </h3>
        <q-badge
          :label="severityLabel"
          :color="severityColor"
          outline
          class="severity-badge"
        />
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
import { useDataStores } from '../../composables/useDataStores'
import { useExternalResources } from '../../composables/useExternalResources'
import { useMessageBrokers } from '../../composables/useMessageBrokers'
import { useAccounts } from '../../composables/useAccounts'
import { useIdentities } from '../../composables/useIdentities'
import { useFuseStore } from '../../stores/FuseStore'
import { DependencyAuthKind, DependencySeverity } from 'api/client'
import { readonlyDependencyAuthLabel, resolveReadonlyDependencyTarget } from '../../utils/readonlyDependencyTarget'
import { getErrorMessage } from '../../utils/error'

const route = useRoute()
const router = useRouter()
const store = useFuseStore()
const id = computed(() => route.params.id as string)

// Data queries
const { data: applicationsData, isLoading, error: applicationsError, refetch: retryApplications } = useApplications()
const { lookup: environmentLookup } = useEnvironments()
const { data: dataStoresData } = useDataStores()
const { data: externalResourcesData } = useExternalResources()
const { data: messageBrokersData } = useMessageBrokers()
const { data: accountsData, error: accountsError } = useAccounts()
const { data: identitiesData, error: identitiesError } = useIdentities()

// Find the dependency by ID and its owning context
const dependencyContext = computed(() => {
  if (!applicationsData.value) return null

  for (const app of applicationsData.value) {
    for (const instance of app.instances ?? []) {
      const dep = instance.dependencies?.find((d) => d.id === id.value)
      if (dep) {
        return { application: app, instance, dependency: dep }
      }
    }
  }
  return null
})

const application = computed(() => dependencyContext.value?.application ?? null)
const instance = computed(() => dependencyContext.value?.instance ?? null)
const dependency = computed(() => dependencyContext.value?.dependency ?? null)

// Environment name for instance
const environmentName = computed(() => {
  const envId = instance.value?.environmentId
  if (!envId) return 'Unknown Environment'
  return environmentLookup.value[envId] ?? 'Unknown Environment'
})

// Dependency display name - generate "uses X" name if unnamed
const dependencyDisplayName = computed(() => {
  if (!dependency.value) return 'Dependency'
  return `Uses ${targetName.value}`
})

// Page title
const pageTitle = computed(() => `Dependency: ${dependencyDisplayName.value}`)

// Target resolution
const target = computed(() => resolveReadonlyDependencyTarget(dependency.value?.targetKind, dependency.value?.targetId, {
  applications: applicationsData.value ?? [], dataStores: dataStoresData.value ?? [],
  externalResources: externalResourcesData.value ?? [], messageBrokers: messageBrokersData.value ?? [],
  environmentNames: environmentLookup.value, sourceEnvironmentId: instance.value?.environmentId
}))
const targetTypeLabel = computed(() => target.value.typeLabel)
const targetIcon = computed(() => target.value.icon)
const targetName = computed(() => target.value.name)
const targetRoute = computed(() => target.value.route)

// Auth resolution
const authKindLabel = computed(() => readonlyDependencyAuthLabel(dependency.value?.authKind))
const authLookupUnavailable = computed(() => {
  if (dependency.value?.authKind === DependencyAuthKind.Account && (!store.hasPermission('accounts:read') || accountsError.value)) return 'Account details are unavailable.'
  if (dependency.value?.authKind === DependencyAuthKind.Identity && (!store.hasPermission('identities:read') || identitiesError.value)) return 'Identity details are unavailable.'
  return ''
})

const authKindColor = computed(() => {
  switch (dependency.value?.authKind) {
    case DependencyAuthKind.None:
      return 'grey'
    case DependencyAuthKind.Account:
      return 'orange'
    case DependencyAuthKind.Identity:
      return 'purple'
    case DependencyAuthKind.ApiKey:
      return 'blue'
    default:
      return 'grey'
  }
})

const authEntityName = computed(() => {
  if (authLookupUnavailable.value) return null
  if (dependency.value?.authKind === DependencyAuthKind.Account && dependency.value.accountId) {
    const account = accountsData.value?.find((acc) => acc.id === dependency.value?.accountId)
    return account?.userName ?? account?.id ?? dependency.value.accountId
  }
  if (dependency.value?.authKind === DependencyAuthKind.Identity && dependency.value.identityId) {
    const identity = identitiesData.value?.find((i) => i.id === dependency.value?.identityId)
    return identity?.name ?? identity?.id ?? dependency.value.identityId
  }
  return null
})

const authEntityRoute = computed(() => {
  if (authLookupUnavailable.value) return null
  if (dependency.value?.authKind === DependencyAuthKind.Account && dependency.value.accountId) {
    return `/view/account/${dependency.value.accountId}`
  }
  if (dependency.value?.authKind === DependencyAuthKind.Identity && dependency.value.identityId) {
    return `/view/identity/${dependency.value.identityId}`
  }
  return null
})

const severityLabel = computed(() => {
  switch (dependency.value?.severity) {
    case DependencySeverity.Partial:
      return 'Partial / Degraded'
    case DependencySeverity.Full:
      return 'Full (will not work without)'
    default:
      return 'Full (will not work without)'
  }
})

const severityColor = computed(() => {
  switch (dependency.value?.severity) {
    case DependencySeverity.Partial:
      return 'warning'
    case DependencySeverity.Full:
      return 'negative'
    default:
      return 'negative'
  }
})

// Higher context: App + Instance
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

  // Instance
  if (instance.value?.id && application.value) {
    items.push({
      id: instance.value.id,
      type: 'instance',
      name: `${application.value.name ?? 'App'} — ${environmentName.value}`,
      route: `/view/instance/${instance.value.id}`,
      subtitle: environmentName.value
    })
  }

  return items
})

// Lower context: Target + Auth entity
const lowerContext = computed<LowerItem[]>(() => {
  const items: LowerItem[] = []

  // Target
  const targetId = target.value.id
  const route = targetRoute.value
  if (targetId && route) {
    items.push({
      id: targetId,
      type: target.value.type,
      name: targetName.value,
      route,
      subtitle: targetTypeLabel.value
    })
  }

  // Auth entity
  if (!authLookupUnavailable.value && dependency.value?.authKind === DependencyAuthKind.Account && dependency.value.accountId) {
    items.push({
      id: dependency.value.accountId,
      type: 'account',
      name: authEntityName.value ?? dependency.value.accountId,
      route: `/view/account/${dependency.value.accountId}`,
      subtitle: 'Account'
    })
  }

  if (!authLookupUnavailable.value && dependency.value?.authKind === DependencyAuthKind.Identity && dependency.value.identityId) {
    items.push({
      id: dependency.value.identityId,
      type: 'identity',
      name: authEntityName.value ?? dependency.value.identityId,
      route: `/view/identity/${dependency.value.identityId}`,
      subtitle: 'Identity'
    })
  }

  return items
})

function goBack() {
  router.push('/view')
}
</script>

<style scoped>
.dependency-loading,
.dependency-error {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  text-align: center;
  padding: 3rem 1rem;
  gap: 1rem;
}

.dependency-error h2 {
  margin: 0;
  font-size: 1.5rem;
  font-weight: 600;
}

.dependency-error p {
  margin: 0;
  color: var(--fuse-text-muted);
}

.dependency-error code {
  background: var(--fuse-panel-bg);
  padding: 0.125rem 0.5rem;
  border-radius: 4px;
  font-family: monospace;
}

.dependency-details {
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

.type-badge,
.auth-badge,
.severity-badge {
  font-size: 0.85rem;
}

.target-summary {
  display: flex;
  align-items: center;
}

.target-link {
  display: inline-flex;
  align-items: center;
  gap: 0.25rem;
  color: var(--q-primary);
  text-decoration: none;
  font-weight: 500;
}

.target-link:hover {
  text-decoration: underline;
}

.target-name {
  font-weight: 500;
}

.auth-info {
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
}

.auth-entity-link {
  display: inline-flex;
  align-items: center;
  gap: 0.25rem;
  color: var(--q-primary);
  text-decoration: none;
}

.auth-entity-link:hover {
  text-decoration: underline;
}
</style>
