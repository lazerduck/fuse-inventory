<template>
  <ReadOnlyShell
    :title="pageTitle"
    :higher="higherContext"
    :lower="lowerContext"
  >
    <!-- Loading state -->
    <div v-if="isLoading" class="identity-loading">
      <q-spinner color="primary" size="48px" />
      <p>Loading identity...</p>
    </div>

    <!-- Error state: Identity not found -->
    <div v-else-if="!identity" class="identity-error">
      <q-icon name="error_outline" size="48px" color="negative" />
      <h2>Identity Not Found</h2>
      <p>The identity with ID <code>{{ id }}</code> could not be found.</p>
      <q-btn flat label="Back to Search" icon="arrow_back" @click="goBack" />
    </div>

    <!-- Identity details -->
    <div v-else class="identity-details">
      <!-- Header -->
      <section class="detail-section">
        <h2 class="section-title">
          <q-icon name="badge" size="24px" color="primary" />
          {{ identityDisplayName }}
        </h2>
      </section>

      <!-- Identity Kind -->
      <section class="detail-section">
        <h3 class="section-subtitle">
          <q-icon name="category" size="20px" />
          Identity Kind
        </h3>
        <q-badge :label="kindLabel" :color="kindColor" outline class="kind-badge" />
      </section>

      <!-- Owner Instance -->
      <section class="detail-section">
        <h3 class="section-subtitle">
          <q-icon name="layers" size="20px" />
          Owner Instance
        </h3>
        <div v-if="ownerInstanceRoute" class="owner-summary">
          <router-link :to="ownerInstanceRoute" class="owner-link">
            {{ ownerInstanceName }}
            <q-icon name="chevron_right" size="16px" />
          </router-link>
        </div>
        <span v-else class="owner-name owner-missing">No assigned owner</span>
      </section>

      <!-- Notes -->
      <section v-if="identity.notes" class="detail-section">
        <h3 class="section-subtitle">
          <q-icon name="notes" size="20px" />
          Notes
        </h3>
        <p class="section-notes">{{ identity.notes }}</p>
      </section>

      <!-- Access Assignments -->
      <section class="detail-section">
        <h3 class="section-subtitle">
          <q-icon name="security" size="20px" />
          Access Assignments
        </h3>
        <div v-if="!hasAssignments" class="empty-value">
          No access assignments configured
        </div>
        <q-list v-else separator class="assignments-list">
          <q-item v-for="assignment in identity.assignments" :key="assignment.id ?? assignment.targetId">
            <q-item-section>
              <q-item-label>
                <span class="assignment-target">Target: {{ resolveTargetName(assignment.targetKind, assignment.targetId) }}</span>
              </q-item-label>
              <q-item-label v-if="assignment.role" caption>
                Role: {{ assignment.role }}
              </q-item-label>
              <q-item-label v-if="assignment.notes" caption class="assignment-notes">
                {{ assignment.notes }}
              </q-item-label>
            </q-item-section>
            <q-item-section side>
              <router-link
                v-if="getAssignmentTargetRoute(assignment)"
                :to="getAssignmentTargetRoute(assignment)!"
                class="assignment-link"
              >
                <q-icon name="chevron_right" size="20px" color="grey-6" />
              </router-link>
              <span v-else class="target-missing-icon">
                <q-icon name="error_outline" size="20px" color="grey-5" />
              </span>
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

      <!-- Timestamps -->
      <section v-if="identity.createdAt || identity.updatedAt" class="detail-section">
        <h3 class="section-subtitle">
          <q-icon name="schedule" size="20px" />
          Timestamps
        </h3>
        <div class="timestamps">
          <div v-if="identity.createdAt" class="timestamp-item">
            <span class="timestamp-label">Created</span>
            <span class="timestamp-value">{{ formatDate(identity.createdAt) }}</span>
          </div>
          <div v-if="identity.updatedAt" class="timestamp-item">
            <span class="timestamp-label">Updated</span>
            <span class="timestamp-value">{{ formatDate(identity.updatedAt) }}</span>
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
import { useIdentities } from '../../composables/useIdentities'
import { useApplications } from '../../composables/useApplications'
import { useDataStores } from '../../composables/useDataStores'
import { useExternalResources } from '../../composables/useExternalResources'
import { useEnvironments } from '../../composables/useEnvironments'
import { useTags } from '../../composables/useTags'
import { DependencyAuthKind, IdentityKind, TargetKind, type IdentityAssignment, type TagColor } from '../../api/client'

const route = useRoute()
const router = useRouter()
const id = computed(() => route.params.id as string)

// Data queries
const { data: identitiesData, isLoading: identitiesLoading } = useIdentities()
const { data: applicationsData, isLoading: appsLoading } = useApplications()
const { data: dataStoresData, isLoading: dataStoresLoading } = useDataStores()
const { data: externalResourcesData, isLoading: externalLoading } = useExternalResources()
const { lookup: environmentLookup, isLoading: envsLoading } = useEnvironments()
const { tagInfoLookup, isLoading: tagsLoading } = useTags()

const isLoading = computed(() =>
  identitiesLoading.value || appsLoading.value || dataStoresLoading.value ||
  externalLoading.value || envsLoading.value || tagsLoading.value
)

// Find the identity by ID
const identity = computed(() => {
  if (!identitiesData.value) return null
  return identitiesData.value.find((ident) => ident.id === id.value) ?? null
})

// Identity display name
const identityDisplayName = computed(() => {
  if (!identity.value) return 'Identity'
  return identity.value.name ?? identity.value.id ?? 'Unnamed Identity'
})

// Page title
const pageTitle = computed(() => `Identity: ${identityDisplayName.value}`)

// Identity kind display
const kindLabel = computed(() => {
  switch (identity.value?.kind) {
    case IdentityKind.AzureManagedIdentity:
      return 'Azure Managed Identity'
    case IdentityKind.KubernetesServiceAccount:
      return 'Kubernetes Service Account'
    case IdentityKind.AwsIamRole:
      return 'AWS IAM Role'
    case IdentityKind.Custom:
      return 'Custom'
    default:
      return 'Unknown'
  }
})

const kindColor = computed(() => {
  switch (identity.value?.kind) {
    case IdentityKind.AzureManagedIdentity:
      return 'blue'
    case IdentityKind.KubernetesServiceAccount:
      return 'purple'
    case IdentityKind.AwsIamRole:
      return 'orange'
    case IdentityKind.Custom:
      return 'grey'
    default:
      return 'grey'
  }
})

// Owner instance resolution
const ownerInstanceInfo = computed(() => {
  const ownerInstanceId = identity.value?.ownerInstanceId
  if (!ownerInstanceId || !applicationsData.value) return null

  for (const app of applicationsData.value) {
    const inst = app.instances?.find((i) => i.id === ownerInstanceId)
    if (inst) {
      const envName = environmentLookup.value[inst.environmentId ?? ''] ?? '—'
      return {
        instance: inst,
        app,
        displayName: `${app.name ?? 'App'} — ${envName}`,
        envName
      }
    }
  }
  return null
})

const ownerInstanceName = computed(() => {
  return ownerInstanceInfo.value?.displayName ?? 'Unknown Instance'
})

const ownerInstanceRoute = computed(() => {
  const ownerInstanceId = identity.value?.ownerInstanceId
  if (!ownerInstanceId) return null
  return `/view/instance/${ownerInstanceId}`
})

// Assignments
const hasAssignments = computed(() => (identity.value?.assignments?.length ?? 0) > 0)

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

// Get route for an assignment target
function getAssignmentTargetRoute(assignment: IdentityAssignment): string | null {
  const targetId = assignment.targetId
  if (!targetId) return null

  switch (assignment.targetKind) {
    case TargetKind.DataStore:
      return `/view/datastore/${targetId}`
    case TargetKind.External:
      return `/view/external/${targetId}`
    case TargetKind.Application:
      return `/view/instance/${targetId}`
    default:
      return null
  }
}

// Resolved tags with name and color
const resolvedTags = computed(() => {
  const tagIds = identity.value?.tagIds ?? []
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

// Higher context: Owning instance and app
const higherContext = computed<HigherItem[]>(() => {
  const items: HigherItem[] = []
  const info = ownerInstanceInfo.value

  if (info) {
    // Add the owning application
    if (info.app.id) {
      items.push({
        id: info.app.id,
        type: 'app',
        name: info.app.name ?? 'Application',
        route: `/view/app/${info.app.id}`
      })
    }

    // Add the owning instance
    if (info.instance.id) {
      items.push({
        id: info.instance.id,
        type: 'instance',
        name: info.displayName,
        route: `/view/instance/${info.instance.id}`,
        subtitle: info.envName
      })
    }
  }

  return items
})

// Lower context: Dependencies that use this identity + Assignment targets
const lowerContext = computed<LowerItem[]>(() => {
  const items: LowerItem[] = []

  // (A) Dependencies that use this identity
  if (applicationsData.value && id.value) {
    for (const app of applicationsData.value) {
      for (const instance of app.instances ?? []) {
        for (const dep of instance.dependencies ?? []) {
          if (dep.authKind === DependencyAuthKind.Identity && dep.identityId === id.value && dep.id) {
            const envName = environmentLookup.value[instance.environmentId ?? ''] ?? 'Unknown'
            const targetDisplayName = resolveTargetName(dep.targetKind, dep.targetId)
            items.push({
              id: dep.id,
              type: 'dependency',
              name: targetDisplayName,
              route: `/view/dependency/${dep.id}`,
              subtitle: `${app.name ?? 'App'} → ${app.name ?? 'App'} — ${envName}`
            })
          }
        }
      }
    }
  }

  // (B) Targets from identity assignments
  const assignments = identity.value?.assignments ?? []
  for (const assignment of assignments) {
    const targetId = assignment.targetId
    if (!targetId) continue

    const targetName = resolveTargetName(assignment.targetKind, targetId)
    const route = getAssignmentTargetRoute(assignment)

    let targetType: 'datastore' | 'external' | 'instance'
    switch (assignment.targetKind) {
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
      name: targetName,
      route: route ?? undefined,
      subtitle: assignment.role ?? 'Assigned Access'
    })
  }

  return items
})

function goBack() {
  router.push('/view')
}
</script>

<style scoped>
.identity-loading,
.identity-error {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  text-align: center;
  padding: 3rem 1rem;
  gap: 1rem;
}

.identity-error h2 {
  margin: 0;
  font-size: 1.5rem;
  font-weight: 600;
}

.identity-error p {
  margin: 0;
  color: var(--fuse-text-muted);
}

.identity-error code {
  background: var(--fuse-panel-bg);
  padding: 0.125rem 0.5rem;
  border-radius: 4px;
  font-family: monospace;
}

.identity-details {
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

.section-notes {
  margin: 0;
  white-space: pre-wrap;
  color: var(--fuse-text-secondary);
}

.kind-badge {
  font-size: 0.85rem;
}

.owner-summary {
  display: flex;
  align-items: center;
}

.owner-link {
  display: inline-flex;
  align-items: center;
  gap: 0.25rem;
  color: var(--q-primary);
  text-decoration: none;
  font-weight: 500;
}

.owner-link:hover {
  text-decoration: underline;
}

.owner-name {
  font-weight: 500;
}

.owner-missing {
  color: var(--fuse-text-muted);
  font-style: italic;
}

.empty-value {
  color: var(--fuse-text-muted);
  font-style: italic;
}

.assignments-list {
  border: 1px solid var(--fuse-panel-border);
  border-radius: 8px;
  overflow: hidden;
}

.assignment-target {
  font-weight: 500;
}

.assignment-notes {
  white-space: pre-wrap;
}

.assignment-link {
  display: flex;
  align-items: center;
  color: var(--q-primary);
  text-decoration: none;
}

.assignment-link:hover {
  color: var(--q-primary-dark, var(--q-primary));
}

.target-missing-icon {
  display: flex;
  align-items: center;
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
