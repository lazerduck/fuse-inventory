<template>
  <ReadOnlyShell
    :title="pageTitle"
    :higher="higherContext"
    :lower="lowerContext"
  >
    <!-- Loading state -->
    <div v-if="isLoading" class="risk-loading">
      <q-spinner color="primary" size="48px" />
      <p>Loading risk...</p>
    </div>

    <!-- Error state: Risk not found -->
    <div v-else-if="!risk" class="risk-error">
      <q-icon name="error_outline" size="48px" color="negative" />
      <h2>Risk Not Found</h2>
      <p>The risk with ID <code>{{ id }}</code> could not be found.</p>
      <q-btn flat label="Back to Search" icon="arrow_back" @click="goBack" />
    </div>

    <!-- Risk details -->
    <div v-else class="risk-details">
      <!-- Header -->
      <section class="detail-section">
        <h2 class="section-title">
          <q-icon name="warning" size="24px" :color="impactColor" />
          {{ risk.title ?? 'Unnamed Risk' }}
        </h2>
        <p v-if="risk.description" class="section-description">
          {{ risk.description }}
        </p>
      </section>

      <!-- Status Badge -->
      <section class="detail-section">
        <h3 class="section-subtitle">
          <q-icon name="flag" size="20px" />
          Status
        </h3>
        <q-badge :label="risk.status" :color="statusColor" class="status-badge" />
      </section>

      <!-- Impact & Likelihood -->
      <section class="detail-section">
        <h3 class="section-subtitle">
          <q-icon name="assessment" size="20px" />
          Assessment
        </h3>
        <div class="assessment-container">
          <div class="assessment-item">
            <span class="assessment-label">Impact:</span>
            <q-badge :label="risk.impact" :color="impactColor" class="assessment-badge" />
          </div>
          <div class="assessment-item">
            <span class="assessment-label">Likelihood:</span>
            <q-badge :label="risk.likelihood" color="info" outline class="assessment-badge" />
          </div>
        </div>
      </section>

      <!-- Target Entity -->
      <section class="detail-section" v-if="targetEntity">
        <h3 class="section-subtitle">
          <q-icon name="my_location" size="20px" />
          Affects
        </h3>
        <router-link :to="targetRoute" class="target-link">
          <q-icon :name="targetIcon" size="18px" />
          {{ targetEntityName }}
          <q-icon name="chevron_right" size="16px" />
        </router-link>
      </section>

      <!-- Owner Position -->
      <section v-if="ownerPosition" class="detail-section">
        <h3 class="section-subtitle">
          <q-icon name="person" size="20px" />
          Owner
        </h3>
        <router-link
          v-if="risk.ownerPositionId"
          :to="`/view/position/${risk.ownerPositionId}`"
          class="position-link"
        >
          {{ ownerPosition.name }}
          <q-icon name="chevron_right" size="16px" />
        </router-link>
        <p v-else class="section-value">{{ ownerPosition.name }}</p>
      </section>

      <!-- Approver Position -->
      <section v-if="approverPosition" class="detail-section">
        <h3 class="section-subtitle">
          <q-icon name="verified_user" size="20px" />
          Approver
        </h3>
        <router-link
          v-if="risk.approverPositionId"
          :to="`/view/position/${risk.approverPositionId}`"
          class="position-link"
        >
          {{ approverPosition.name }}
          <q-icon name="chevron_right" size="16px" />
        </router-link>
        <p v-else class="section-value">{{ approverPosition.name }}</p>
      </section>

      <!-- Mitigation -->
      <section v-if="risk.mitigation" class="detail-section">
        <h3 class="section-subtitle">
          <q-icon name="health_and_safety" size="20px" />
          Mitigation
        </h3>
        <p class="section-notes">{{ risk.mitigation }}</p>
      </section>

      <!-- Review Date -->
      <section v-if="risk.reviewDate" class="detail-section">
        <h3 class="section-subtitle">
          <q-icon name="event" size="20px" />
          Review Date
        </h3>
        <p class="section-value">{{ formatDate(risk.reviewDate) }}</p>
      </section>

      <!-- Approval Date -->
      <section v-if="risk.approvalDate" class="detail-section">
        <h3 class="section-subtitle">
          <q-icon name="check_circle" size="20px" />
          Approval Date
        </h3>
        <p class="section-value">{{ formatDate(risk.approvalDate) }}</p>
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
      <section v-if="risk.notes" class="detail-section">
        <h3 class="section-subtitle">
          <q-icon name="notes" size="20px" />
          Notes
        </h3>
        <p class="section-notes">{{ risk.notes }}</p>
      </section>
    </div>
  </ReadOnlyShell>
</template>

<script setup lang="ts">
import { useRoute, useRouter } from 'vue-router'
import { computed } from 'vue'
import ReadOnlyShell from '../../components/readonly/ReadOnlyShell.vue'
import type { HigherItem, LowerItem } from '../../types/readonly'
import { useRisks } from '../../composables/useRisks'
import { useApplications } from '../../composables/useApplications'
import { useDataStores } from '../../composables/useDataStores'
import { usePlatforms } from '../../composables/usePlatforms'
import { useExternalResources } from '../../composables/useExternalResources'
import { useMessageBrokers } from '../../composables/useMessageBrokers'
import { usePositions } from '../../composables/usePositions'
import { useTags } from '../../composables/useTags'
import type {
  Application,
  DataStore,
  ExternalResource,
  MessageBroker,
  Platform,
  Risk,
  TagColor
} from 'api/client'

type TargetEntity = Application | DataStore | ExternalResource | MessageBroker | Platform
type RiskTargetType = 'Application' | 'DataStore' | 'Platform' | 'ExternalResource' | 'MessageBroker'

const route = useRoute()
const router = useRouter()
const id = computed(() => route.params.id as string)

// Data queries
const { risks, risksLoading } = useRisks()
const { data: applicationsData, isLoading: appsLoading } = useApplications()
const { data: dataStoresData, isLoading: datastoresLoading } = useDataStores()
const { data: platformsData, isLoading: platformsLoading } = usePlatforms()
const { data: externalResourcesData, isLoading: externalLoading } = useExternalResources()
const { data: messageBrokersData, isLoading: brokersLoading } = useMessageBrokers()
const { data: positionsData, isLoading: positionsDataLoading } = usePositions()
const { tagInfoLookup, isLoading: tagsLoading } = useTags()

const isLoading = computed(() =>
  risksLoading.value ||
  appsLoading.value ||
  datastoresLoading.value ||
  platformsLoading.value ||
  externalLoading.value ||
  brokersLoading.value ||
    positionsDataLoading.value ||
  tagsLoading.value
)

// Find the risk by ID
const risk = computed(() => {
  const allRisks = (risks.value ?? []) as Risk[]
  return allRisks.find((r) => r.id === id.value) ?? null
})

// Page title
const pageTitle = computed(() => {
  const title = risk.value?.title ?? id.value
  return `Risk: ${title}`
})

// Resolved tags
const resolvedTags = computed(() => {
  const tagIds = risk.value?.tagIds ?? []
  return tagIds
    .map((tagId: string) => {
      const info = tagInfoLookup.value[tagId]
      return info ? { id: tagId, name: info.name, color: info.color } : null
    })
    .filter((tag): tag is { id: string; name: string; color: TagColor | undefined } => tag !== null)
})

// Impact color mapping
const impactColor = computed(() => {
  const impact = risk.value?.impact
  switch (impact) {
    case 'Critical':
      return 'negative'
    case 'High':
      return 'orange'
    case 'Medium':
      return 'warning'
    case 'Low':
      return 'positive'
    default:
      return 'grey'
  }
})

// Status color mapping
const statusColor = computed(() => {
  const status = risk.value?.status
  switch (status) {
    case 'Identified':
      return 'info'
    case 'Mitigated':
      return 'positive'
    case 'Accepted':
      return 'warning'
    case 'Closed':
      return 'grey'
    default:
      return 'grey'
  }
})

// Find target entity and build route
const targetEntity = computed<TargetEntity | null>(() => {
  if (!risk.value) return null
  const targetType = risk.value.targetType as RiskTargetType | undefined
  const targetId = risk.value.targetId

  switch (targetType) {
    case 'Application':
      return applicationsData.value?.find((app) => app.id === targetId) ?? null
    case 'DataStore':
      return dataStoresData.value?.find((ds) => ds.id === targetId) ?? null
    case 'Platform':
      return platformsData.value?.find((p) => p.id === targetId) ?? null
    case 'ExternalResource':
      return externalResourcesData.value?.find((er) => er.id === targetId) ?? null
    case 'MessageBroker':
      return messageBrokersData.value?.find((mb) => mb.id === targetId) ?? null
    default:
      return null
  }
})

const targetEntityName = computed(() => {
  if (!targetEntity.value) return 'Unknown'
  const entity = targetEntity.value as {
    name?: string
    displayName?: string
    dnsName?: string
  }
  return entity.displayName ?? entity.name ?? entity.dnsName ?? 'Unknown'
})

const targetRoute = computed(() => {
  if (!risk.value) return '/'
  const targetType = risk.value.targetType as RiskTargetType | undefined
  const targetId = risk.value.targetId

  switch (targetType) {
    case 'Application':
      return `/view/app/${targetId}`
    case 'DataStore':
      return `/view/datastore/${targetId}`
    case 'Platform':
      return `/view/platform/${targetId}`
    case 'ExternalResource':
      return `/view/external/${targetId}`
    case 'MessageBroker':
      return `/view/message-broker/${targetId}`
    default:
      return '/'
  }
})

const targetIcon = computed(() => {
  const targetType = risk.value?.targetType as RiskTargetType | undefined
  switch (targetType) {
    case 'Application':
      return 'apps'
    case 'DataStore':
      return 'storage'
    case 'Platform':
      return 'computer'
    case 'ExternalResource':
      return 'hub'
    case 'MessageBroker':
      return 'swap_horiz'
    default:
      return 'help'
  }
})

function mapTargetTypeToEntityType(targetType: RiskTargetType | undefined): HigherItem['type'] {
  switch (targetType) {
    case 'Application':
      return 'app'
    case 'DataStore':
      return 'datastore'
    case 'Platform':
      return 'platform'
    case 'ExternalResource':
      return 'external'
    case 'MessageBroker':
      return 'messagebroker'
    default:
      return 'external'
  }
}

// Position lookups
const ownerPosition = computed(() => {
  if (!risk.value?.ownerPositionId) return null
  return positionsData.value?.find((p) => p.id === risk.value?.ownerPositionId) ?? null
})

const approverPosition = computed(() => {
  if (!risk.value?.approverPositionId) return null
  return positionsData.value?.find((p) => p.id === risk.value?.approverPositionId) ?? null
})

// Higher context: The target entity this risk affects
const higherContext = computed<HigherItem[]>(() => {
  if (!targetEntity.value || !risk.value) return []
  
  return [
    {
      id: risk.value.targetId!,
      type: mapTargetTypeToEntityType(risk.value.targetType as RiskTargetType | undefined),
      name: targetEntityName.value,
      route: targetRoute.value
    }
  ]
})

// Lower context: Empty for risks (no children)
const lowerContext = computed<LowerItem[]>(() => [])

function formatDate(date: Date | string | undefined): string {
  if (!date) return 'N/A'
  const d = typeof date === 'string' ? new Date(date) : date
  return d.toLocaleDateString('en-US', {
    year: 'numeric',
    month: 'long',
    day: 'numeric'
  })
}

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
.risk-loading,
.risk-error {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  text-align: center;
  padding: 3rem 1rem;
  gap: 1rem;
}

.risk-error h2 {
  margin: 0;
  font-size: 1.5rem;
  font-weight: 600;
}

.risk-error p {
  margin: 0;
  color: var(--fuse-text-muted);
}

.risk-error code {
  background: var(--fuse-panel-bg);
  padding: 0.125rem 0.5rem;
  border-radius: 4px;
  font-family: monospace;
}

.risk-details {
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

.status-badge {
  font-size: 0.9rem;
  padding: 0.25rem 0.75rem;
}

.assessment-container {
  display: flex;
  gap: 1.5rem;
  flex-wrap: wrap;
}

.assessment-item {
  display: flex;
  align-items: center;
  gap: 0.5rem;
}

.assessment-label {
  font-weight: 500;
  color: var(--fuse-text-muted);
}

.assessment-badge {
  font-size: 0.85rem;
  padding: 0.25rem 0.75rem;
}

.target-link,
.position-link {
  display: inline-flex;
  align-items: center;
  gap: 0.25rem;
  color: var(--q-primary);
  text-decoration: none;
  font-size: 1rem;
}

.target-link:hover,
.position-link:hover {
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
</style>
