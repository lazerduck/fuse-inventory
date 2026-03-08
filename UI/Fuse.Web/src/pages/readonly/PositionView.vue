<template>
  <ReadOnlyShell
    :title="pageTitle"
    :higher="higherContext"
    :lower="lowerContext"
  >
    <!-- Loading state -->
    <div v-if="isLoading" class="position-loading">
      <q-spinner color="primary" size="48px" />
      <p>Loading position...</p>
    </div>

    <!-- Error state: Position not found -->
    <div v-else-if="!position" class="position-error">
      <q-icon name="error_outline" size="48px" color="negative" />
      <h2>Position Not Found</h2>
      <p>The position with ID <code>{{ id }}</code> could not be found.</p>
      <q-btn flat label="Back to Search" icon="arrow_back" @click="goBack" />
    </div>

    <!-- Position details -->
    <div v-else class="position-details">
      <!-- Header -->
      <section class="detail-section">
        <h2 class="section-title">
          <q-icon name="work" size="24px" color="primary" />
          {{ position.name ?? 'Unnamed Position' }}
        </h2>
        <p v-if="position.description" class="section-description">
          {{ position.description }}
        </p>
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

      <!-- Owned Risks Summary -->
      <section v-if="ownedRisks.length > 0" class="detail-section">
        <h3 class="section-subtitle">
          <q-icon name="warning" size="20px" />
          Owned Risks
        </h3>
        <div class="risks-note">
          This position owns {{ ownedRisks.length }} risk{{ ownedRisks.length === 1 ? '' : 's' }}.
        </div>
      </section>

      <!-- Risks for Approval -->
      <section v-if="risksForApproval.length > 0" class="detail-section">
        <h3 class="section-subtitle">
          <q-icon name="verified_user" size="20px" />
          Risks for Approval
        </h3>
        <div class="risks-note">
          This position is approver for {{ risksForApproval.length }} risk{{ risksForApproval.length === 1 ? '' : 's' }}.
        </div>
      </section>

      <!-- Responsibility Assignments -->
      <section v-if="positionAssignments.length > 0" class="detail-section">
        <h3 class="section-subtitle">
          <q-icon name="assignment_ind" size="20px" />
          Responsibility Assignments
        </h3>
        <div class="assignment-list">
          <div v-for="assignment in positionAssignments" :key="assignment.id" class="assignment-item">
            <div class="assignment-main">
              <router-link :to="`/view/app/${assignment.applicationId}`" class="assignment-link">
                {{ assignment.applicationName }}
              </router-link>
              <span class="assignment-type">{{ assignment.responsibilityTypeName }}</span>
            </div>
            <div class="assignment-meta">
              <q-badge
                :label="assignment.scopeLabel"
                :color="assignment.scope === ResponsibilityScope.All ? 'primary' : 'secondary'"
                outline
              />
              <q-badge v-if="assignment.environmentName" :label="assignment.environmentName" color="grey-7" outline />
              <q-badge v-if="assignment.primary" label="Primary" color="positive" />
            </div>
          </div>
        </div>
      </section>

      <!-- Empty state -->
      <section v-if="ownedRisks.length === 0 && risksForApproval.length === 0 && positionAssignments.length === 0" class="detail-section empty-state">
        <q-icon name="info" size="24px" color="grey-6" />
        <p class="empty-message">No risks or responsibilities are currently assigned to this position.</p>
      </section>
    </div>
  </ReadOnlyShell>
</template>

<script setup lang="ts">
import { useRoute, useRouter } from 'vue-router'
import { computed } from 'vue'
import { useQuery } from '@tanstack/vue-query'
import ReadOnlyShell from '../../components/readonly/ReadOnlyShell.vue'
import type { HigherItem, LowerItem } from '../../types/readonly'
import { usePositions } from '../../composables/usePositions'
import { useRisks } from '../../composables/useRisks'
import { useTags } from '../../composables/useTags'
import { useApplications } from '../../composables/useApplications'
import { useEnvironments } from '../../composables/useEnvironments'
import { useFuseClient } from '../../composables/useFuseClient'
import { ResponsibilityScope, type ResponsibilityAssignment, type Risk, type TagColor } from '../../api/client'

const route = useRoute()
const router = useRouter()
const id = computed(() => route.params.id as string)
const client = useFuseClient()

// Data queries
const { data: positionsData, isLoading: positionsLoading } = usePositions()
const { risks, risksLoading } = useRisks()
const { tagInfoLookup, isLoading: tagsLoading } = useTags()
const { data: applicationsData, isLoading: appsLoading } = useApplications()
const { lookup: environmentLookup, isLoading: envsLoading } = useEnvironments()

const responsibilityTypesQuery = useQuery({
  queryKey: ['responsibilityTypes'],
  queryFn: () => client.responsibilityTypeAll()
})

const assignmentsQuery = useQuery({
  queryKey: ['responsibilityAssignmentsAllApps'],
  enabled: computed(() => (applicationsData.value?.length ?? 0) > 0),
  queryFn: async () => {
    const appIds = (applicationsData.value ?? []).map((app) => app.id).filter((appId): appId is string => !!appId)
    const assignmentSets = await Promise.all(appIds.map((appId) => client.responsibilityAssignmentAll(appId)))
    return assignmentSets.flat()
  }
})

const isLoading = computed(() =>
  positionsLoading.value ||
  risksLoading.value ||
  tagsLoading.value ||
  appsLoading.value ||
  envsLoading.value ||
  responsibilityTypesQuery.isLoading.value ||
  assignmentsQuery.isLoading.value
)

// Find the position by ID
const position = computed(() => {
  if (!positionsData.value) return null
  return positionsData.value.find((p) => p.id === id.value) ?? null
})

// Page title
const pageTitle = computed(() => {
  const name = position.value?.name ?? id.value
  return `Position: ${name}`
})

// Resolved tags
const resolvedTags = computed(() => {
  const tagIds = position.value?.tagIds ?? []
  return tagIds
    .map((tagId) => {
      const info = tagInfoLookup.value[tagId]
      return info ? { id: tagId, name: info.name, color: info.color } : null
    })
    .filter((tag): tag is { id: string; name: string; color: TagColor | undefined } => tag !== null)
})

// Risks owned by this position
const ownedRisks = computed(() => {
  const allRisks = (risks.value ?? []) as Risk[]
  if (!id.value) return []
  return allRisks.filter((risk) => risk.ownerPositionId === id.value)
})

// Risks this position approves
const risksForApproval = computed(() => {
  const allRisks = (risks.value ?? []) as Risk[]
  if (!id.value) return []
  return allRisks.filter((risk) => risk.approverPositionId === id.value)
})

const positionAssignments = computed(() => {
  const assignmentList = (assignmentsQuery.data.value ?? []) as ResponsibilityAssignment[]
  const apps = applicationsData.value ?? []
  const responsibilityTypes = responsibilityTypesQuery.data.value ?? []
  const typeLookup = new Map(responsibilityTypes.filter((t) => t.id).map((t) => [t.id!, t.name ?? 'Responsibility']))
  const appLookup = new Map(apps.filter((a) => a.id).map((a) => [a.id!, a.name ?? 'Application']))

  return assignmentList
    .filter((assignment) => assignment.positionId === id.value)
    .map((assignment) => {
      const scope = assignment.scope ?? ResponsibilityScope.All
      const environmentName = assignment.environmentId ? (environmentLookup.value[assignment.environmentId] ?? 'Unknown Environment') : undefined
      return {
        id: assignment.id ?? `${assignment.applicationId}-${assignment.responsibilityTypeId}`,
        applicationId: assignment.applicationId ?? '',
        applicationName: appLookup.get(assignment.applicationId ?? '') ?? 'Application',
        responsibilityTypeName: typeLookup.get(assignment.responsibilityTypeId ?? '') ?? 'Responsibility',
        scope,
        scopeLabel: scope === ResponsibilityScope.Environment ? 'Environment' : 'All Environments',
        environmentName,
        primary: assignment.primary ?? false
      }
    })
})

// Higher context: Empty for positions (no parent entities)
const higherContext = computed<HigherItem[]>(() => [])

// Lower context: Empty for now (could add responsibility assignments in future)
const lowerContext = computed<LowerItem[]>(() => [])

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

function goBack() {
  router.push('/view')
}
</script>

<style scoped>
.position-loading,
.position-error {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  text-align: center;
  padding: 3rem 1rem;
  gap: 1rem;
}

.position-error h2 {
  margin: 0;
  font-size: 1.5rem;
  font-weight: 600;
}

.position-error p {
  margin: 0;
  color: var(--fuse-text-muted);
}

.position-error code {
  background: var(--fuse-panel-bg);
  padding: 0.125rem 0.5rem;
  border-radius: 4px;
  font-family: monospace;
}

.position-details {
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

.tags-container {
  display: flex;
  flex-wrap: wrap;
  gap: 0.5rem;
}

.tag-badge {
  font-size: 0.8rem;
}

.risks-note {
  color: var(--fuse-text-secondary);
  font-size: 0.95rem;
}

.assignment-list {
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
}

.assignment-item {
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
  padding: 0.75rem;
  background: var(--fuse-panel-bg);
  border-radius: 6px;
}

.assignment-main {
  display: flex;
  align-items: center;
  gap: 0.75rem;
  flex-wrap: wrap;
}

.assignment-link {
  color: var(--q-primary);
  text-decoration: none;
  font-weight: 500;
}

.assignment-link:hover {
  text-decoration: underline;
}

.assignment-type {
  color: var(--fuse-text-muted);
}

.assignment-meta {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  flex-wrap: wrap;
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
