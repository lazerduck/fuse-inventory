<template>
  <ReadOnlyShell
    :title="pageTitle"
    :higher="higherContext"
    :lower="lowerContext"
  >
    <!-- Loading state -->
    <div v-if="isLoading" class="account-loading">
      <q-spinner color="primary" size="48px" />
      <p>Loading account...</p>
    </div>

    <!-- Error state: Account not found -->
    <div v-else-if="!account" class="account-error">
      <q-icon name="error_outline" size="48px" color="negative" />
      <h2>Account Not Found</h2>
      <p>The account with ID <code>{{ id }}</code> could not be found.</p>
      <q-btn flat label="Back to Search" icon="arrow_back" @click="goBack" />
    </div>

    <!-- Account details -->
    <div v-else class="account-details">
      <!-- Header -->
      <section class="detail-section">
        <h2 class="section-title">
          <q-icon name="vpn_key" size="24px" color="primary" />
          {{ accountDisplayName }}
        </h2>
      </section>

      <!-- Account Type -->
      <section class="detail-section">
        <h3 class="section-subtitle">
          <q-icon name="category" size="20px" />
          Authentication Type
        </h3>
        <q-badge :label="authKindLabel" :color="authKindColor" outline class="type-badge" />
      </section>

      <!-- Target -->
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
          <span v-else class="target-name target-missing">Target not found</span>
        </div>
        <p v-if="account.targetKind" class="target-type-label">{{ targetTypeLabel }}</p>
      </section>

      <!-- Secret Reference (masked) -->
      <section v-if="secretReference" class="detail-section">
        <h3 class="section-subtitle">
          <q-icon name="key" size="20px" />
          Secret Reference
        </h3>
        <div class="secret-reference">
          <q-badge :label="secretBindingLabel" color="grey" outline class="secret-badge" />
          <span class="secret-value">{{ secretReference }}</span>
        </div>
      </section>

      <!-- SQL Grants -->
      <section v-if="hasGrants" class="detail-section">
        <h3 class="section-subtitle">
          <q-icon name="security" size="20px" />
          SQL Grants
        </h3>
        <q-list separator class="grants-list">
          <q-item v-for="grant in account.grants" :key="grant.id ?? grant.database">
            <q-item-section>
              <q-item-label>
                <span class="grant-database">{{ grant.database ?? 'All databases' }}</span>
                <span v-if="grant.schema" class="grant-schema"> / {{ grant.schema }}</span>
              </q-item-label>
              <q-item-label caption>
                <div v-if="grant.privileges?.length" class="privileges-container">
                  <q-badge
                    v-for="privilege in grant.privileges"
                    :key="privilege"
                    :label="privilege"
                    color="secondary"
                    outline
                    class="privilege-badge"
                  />
                </div>
                <span v-else class="no-privileges">No specific privileges</span>
              </q-item-label>
            </q-item-section>
          </q-item>
        </q-list>
      </section>

      <!-- Parameters -->
      <section v-if="hasParameters" class="detail-section">
        <h3 class="section-subtitle">
          <q-icon name="settings" size="20px" />
          Parameters
        </h3>
        <div class="parameters-list">
          <div v-for="(value, key) in account.parameters" :key="key" class="parameter-item">
            <span class="parameter-key">{{ key }}</span>
            <span class="parameter-value">{{ value }}</span>
          </div>
        </div>
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

      <!-- Timestamps -->
      <section v-if="account.createdAt || account.updatedAt" class="detail-section">
        <h3 class="section-subtitle">
          <q-icon name="schedule" size="20px" />
          Timestamps
        </h3>
        <div class="timestamps">
          <div v-if="account.createdAt" class="timestamp-item">
            <span class="timestamp-label">Created</span>
            <span class="timestamp-value">{{ formatDate(account.createdAt) }}</span>
          </div>
          <div v-if="account.updatedAt" class="timestamp-item">
            <span class="timestamp-label">Updated</span>
            <span class="timestamp-value">{{ formatDate(account.updatedAt) }}</span>
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
import { useAccounts } from '../../composables/useAccounts'
import { useApplications } from '../../composables/useApplications'
import { useDataStores } from '../../composables/useDataStores'
import { useExternalResources } from '../../composables/useExternalResources'
import { useEnvironments } from '../../composables/useEnvironments'
import { useTags } from '../../composables/useTags'
import { AuthKind, DependencyAuthKind, SecretBindingKind, TargetKind, type TagColor } from '../../api/client'

const route = useRoute()
const router = useRouter()
const id = computed(() => route.params.id as string)

// Data queries
const { data: accountsData, isLoading: accountsLoading } = useAccounts()
const { data: applicationsData, isLoading: appsLoading } = useApplications()
const { data: dataStoresData, isLoading: dataStoresLoading } = useDataStores()
const { data: externalResourcesData, isLoading: externalLoading } = useExternalResources()
const { lookup: environmentLookup, isLoading: envsLoading } = useEnvironments()
const { tagInfoLookup, isLoading: tagsLoading } = useTags()

const isLoading = computed(() =>
  accountsLoading.value || appsLoading.value || dataStoresLoading.value ||
  externalLoading.value || envsLoading.value || tagsLoading.value
)

// Find the account by ID
const account = computed(() => {
  if (!accountsData.value) return null
  return accountsData.value.find((acc) => acc.id === id.value) ?? null
})

// Account display name
const accountDisplayName = computed(() => {
  if (!account.value) return 'Account'
  return account.value.userName ?? account.value.id ?? 'Unnamed Account'
})

// Page title
const pageTitle = computed(() => `Account: ${accountDisplayName.value}`)

// Auth kind display
const authKindLabel = computed(() => {
  switch (account.value?.authKind) {
    case AuthKind.None:
      return 'None'
    case AuthKind.UserPassword:
      return 'Username/Password'
    case AuthKind.ApiKey:
      return 'API Key'
    case AuthKind.BearerToken:
      return 'Bearer Token'
    case AuthKind.OAuthClient:
      return 'OAuth Client'
    case AuthKind.ManagedIdentity:
      return 'Managed Identity'
    case AuthKind.Certificate:
      return 'Certificate'
    case AuthKind.Other:
      return 'Other'
    default:
      return 'Unknown'
  }
})

const authKindColor = computed(() => {
  switch (account.value?.authKind) {
    case AuthKind.None:
      return 'grey'
    case AuthKind.UserPassword:
      return 'orange'
    case AuthKind.ApiKey:
      return 'blue'
    case AuthKind.BearerToken:
      return 'purple'
    case AuthKind.OAuthClient:
      return 'teal'
    case AuthKind.ManagedIdentity:
      return 'green'
    case AuthKind.Certificate:
      return 'deep-orange'
    case AuthKind.Other:
      return 'grey'
    default:
      return 'grey'
  }
})

// Target resolution
const targetTypeLabel = computed(() => {
  switch (account.value?.targetKind) {
    case TargetKind.DataStore:
      return 'Data Store'
    case TargetKind.External:
      return 'External Resource'
    case TargetKind.Application:
      return 'Instance'
    default:
      return 'Unknown'
  }
})

const targetIcon = computed(() => {
  switch (account.value?.targetKind) {
    case TargetKind.DataStore:
      return 'storage'
    case TargetKind.External:
      return 'hub'
    case TargetKind.Application:
      return 'layers'
    default:
      return 'help'
  }
})

// Resolve target name from targetKind and targetId
function resolveTargetName(targetKind: TargetKind | undefined, targetId: string | undefined): string {
  if (!targetId) return 'Unknown'

  switch (targetKind) {
    case TargetKind.DataStore: {
      const store = dataStoresData.value?.find((ds) => ds.id === targetId)
      return store?.name ?? targetId
    }
    case TargetKind.External: {
      const ext = externalResourcesData.value?.find((er) => er.id === targetId)
      return ext?.name ?? targetId
    }
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
    default:
      return targetId
  }
}

const targetName = computed(() => {
  return resolveTargetName(account.value?.targetKind, account.value?.targetId)
})

const targetRoute = computed(() => {
  const targetId = account.value?.targetId
  if (!targetId) return null

  switch (account.value?.targetKind) {
    case TargetKind.DataStore:
      return `/view/datastore/${targetId}`
    case TargetKind.External:
      return `/view/external/${targetId}`
    case TargetKind.Application:
      return `/view/instance/${targetId}`
    default:
      return null
  }
})

// Secret reference (masked)
const secretReference = computed(() => {
  const binding = account.value?.secretBinding
  if (!binding) return account.value?.secretRef ?? null

  switch (binding.kind) {
    case SecretBindingKind.AzureKeyVault:
      return binding.azureKeyVault?.secretName ?? null
    case SecretBindingKind.PlainReference:
      return binding.plainReference ?? null
    default:
      return account.value?.secretRef ?? null
  }
})

const secretBindingLabel = computed(() => {
  const binding = account.value?.secretBinding
  if (!binding) return 'Reference'

  switch (binding.kind) {
    case SecretBindingKind.AzureKeyVault:
      return 'Azure Key Vault'
    case SecretBindingKind.PlainReference:
      return 'Plain Reference'
    default:
      return 'Reference'
  }
})

// Grants
const hasGrants = computed(() => (account.value?.grants?.length ?? 0) > 0)

// Parameters
const hasParameters = computed(() => {
  const params = account.value?.parameters
  return params && Object.keys(params).length > 0
})

// Resolved tags with name and color
const resolvedTags = computed(() => {
  const tagIds = account.value?.tagIds ?? []
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

// Format date for display
function formatDate(date: Date | undefined): string {
  if (!date) return '—'
  return new Date(date).toLocaleString()
}

// Higher context: Dependencies that use this account
const higherContext = computed<HigherItem[]>(() => {
  if (!applicationsData.value || !id.value) return []

  const usages: Array<{
    depId: string
    depName: string
    appName: string
    envName: string
    instanceName: string
    instanceId: string
    appId: string
  }> = []

  // Find all dependencies that reference this account
  for (const app of applicationsData.value) {
    for (const instance of app.instances ?? []) {
      for (const dep of instance.dependencies ?? []) {
        if (dep.authKind === DependencyAuthKind.Account && dep.accountId === id.value && dep.id) {
          const envName = environmentLookup.value[instance.environmentId ?? ''] ?? 'Unknown'
          const targetDisplayName = resolveTargetName(dep.targetKind, dep.targetId)
          usages.push({
            depId: dep.id,
            depName: targetDisplayName,
            appName: app.name ?? 'App',
            envName,
            instanceName: `${app.name ?? 'App'} — ${envName}`,
            instanceId: instance.id ?? '',
            appId: app.id ?? ''
          })
        }
      }
    }
  }

  // Sort by app name, environment, instance name
  usages.sort((a, b) => {
    const appCompare = a.appName.localeCompare(b.appName)
    if (appCompare !== 0) return appCompare
    const envCompare = a.envName.localeCompare(b.envName)
    if (envCompare !== 0) return envCompare
    return a.instanceName.localeCompare(b.instanceName)
  })

  return usages.map((usage) => ({
    id: usage.depId,
    type: 'dependency' as const,
    name: usage.depName,
    route: `/view/dependency/${usage.depId}`,
    subtitle: `${usage.appName} → ${usage.instanceName}`
  }))
})

// Lower context: Target (single item)
const lowerContext = computed<LowerItem[]>(() => {
  const items: LowerItem[] = []

  const targetId = account.value?.targetId
  const route = targetRoute.value

  if (targetId && route) {
    let targetType: 'datastore' | 'external' | 'instance'
    switch (account.value?.targetKind) {
      case TargetKind.DataStore:
        targetType = 'datastore'
        break
      case TargetKind.External:
        targetType = 'external'
        break
      case TargetKind.Application:
        targetType = 'instance'
        break
      default:
        targetType = 'datastore'
    }

    items.push({
      id: targetId,
      type: targetType,
      name: targetName.value,
      route,
      subtitle: 'Target'
    })
  }

  return items
})

function goBack() {
  router.push('/view')
}
</script>

<style scoped>
.account-loading,
.account-error {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  text-align: center;
  padding: 3rem 1rem;
  gap: 1rem;
}

.account-error h2 {
  margin: 0;
  font-size: 1.5rem;
  font-weight: 600;
}

.account-error p {
  margin: 0;
  color: var(--fuse-text-muted);
}

.account-error code {
  background: var(--fuse-panel-bg);
  padding: 0.125rem 0.5rem;
  border-radius: 4px;
  font-family: monospace;
}

.account-details {
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

.type-badge {
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

.target-missing {
  color: var(--fuse-text-muted);
  font-style: italic;
}

.target-type-label {
  margin: 0.25rem 0 0 0;
  font-size: 0.85rem;
  color: var(--fuse-text-muted);
}

.secret-reference {
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
}

.secret-badge {
  width: fit-content;
  font-size: 0.75rem;
}

.secret-value {
  font-family: monospace;
  font-size: 0.9rem;
  color: var(--fuse-text-secondary);
  word-break: break-all;
}

.grants-list {
  border: 1px solid var(--fuse-panel-border);
  border-radius: 8px;
  overflow: hidden;
}

.grant-database {
  font-weight: 500;
}

.grant-schema {
  color: var(--fuse-text-muted);
}

.privileges-container {
  display: flex;
  flex-wrap: wrap;
  gap: 0.25rem;
  margin-top: 0.25rem;
}

.privilege-badge {
  font-size: 0.7rem;
}

.no-privileges {
  color: var(--fuse-text-muted);
  font-style: italic;
}

.parameters-list {
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
}

.parameter-item {
  display: flex;
  gap: 0.75rem;
  padding: 0.5rem;
  background: var(--fuse-panel-bg);
  border-radius: 4px;
}

.parameter-key {
  font-weight: 500;
  color: var(--fuse-text-muted);
  min-width: 100px;
}

.parameter-value {
  font-family: monospace;
  font-size: 0.9rem;
  word-break: break-all;
}

.tags-container {
  display: flex;
  flex-wrap: wrap;
  gap: 0.5rem;
}

.tag-badge {
  font-size: 0.8rem;
}

.timestamps {
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
}

.timestamp-item {
  display: flex;
  gap: 0.75rem;
}

.timestamp-label {
  font-weight: 500;
  color: var(--fuse-text-muted);
  min-width: 80px;
}

.timestamp-value {
  color: var(--fuse-text-secondary);
}
</style>
