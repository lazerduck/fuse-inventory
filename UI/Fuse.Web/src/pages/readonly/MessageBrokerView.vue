<template>
  <ReadOnlyShell
    :title="pageTitle"
    :higher="higherContext"
    :lower="lowerContext"
  >
    <!-- Loading state -->
    <div v-if="isLoading" class="broker-loading">
      <q-spinner color="primary" size="48px" />
      <p>Loading message broker...</p>
    </div>

    <!-- Error state: Message broker not found -->
    <div v-else-if="!messageBroker" class="broker-error">
      <q-icon name="error_outline" size="48px" color="negative" />
      <h2>Message Broker Not Found</h2>
      <p>The message broker with ID <code>{{ id }}</code> could not be found.</p>
      <q-btn flat label="Back to Search" icon="arrow_back" @click="goBack" />
    </div>

    <!-- Message broker details -->
    <div v-else class="broker-details">
      <!-- Header -->
      <section class="detail-section">
        <h2 class="section-title">
          <q-icon name="swap_horiz" size="24px" color="primary" />
          {{ messageBroker.name ?? 'Unnamed Message Broker' }}
        </h2>
        <p v-if="messageBroker.description" class="section-description">
          {{ messageBroker.description }}
        </p>
      </section>

      <!-- Kind -->
      <section class="detail-section">
        <h3 class="section-subtitle">
          <q-icon name="category" size="20px" />
          Kind
        </h3>
        <q-badge :label="messageBroker.kind ?? 'Unknown'" color="primary" outline class="kind-badge" />
      </section>

      <!-- Connection URI -->
      <section v-if="messageBroker.connectionUri" class="detail-section">
        <h3 class="section-subtitle">
          <q-icon name="link" size="20px" />
          Connection URI
        </h3>
        <code class="connection-uri">{{ messageBroker.connectionUri }}</code>
      </section>

      <!-- Queues -->
      <section v-if="(messageBroker.queues ?? []).length > 0" class="detail-section">
        <h3 class="section-subtitle">
          <q-icon name="inbox" size="20px" />
          Queues
        </h3>
        <div class="channel-list">
          <div v-for="queue in messageBroker.queues" :key="queue.id" class="channel-item">
            <div class="channel-item-name">{{ queue.name }}</div>
            <div v-if="queue.description" class="channel-item-desc">{{ queue.description }}</div>
          </div>
        </div>
      </section>

      <!-- Topics -->
      <section v-if="(messageBroker.topics ?? []).length > 0" class="detail-section">
        <h3 class="section-subtitle">
          <q-icon name="alt_route" size="20px" />
          Topics
        </h3>
        <div class="channel-list">
          <div v-for="topic in messageBroker.topics" :key="topic.id" class="channel-item">
            <div class="channel-item-name">{{ topic.name }}</div>
            <div v-if="topic.description" class="channel-item-desc">{{ topic.description }}</div>
            <div v-if="(topic.subscribers ?? []).length > 0" class="channel-subscribers">
              <span class="subscribers-label">Subscribers:</span>
              <q-badge
                v-for="subscriber in topic.subscribers"
                :key="subscriber"
                :label="subscriber"
                color="teal"
                outline
                class="subscriber-badge"
              />
            </div>
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

      <!-- Empty state -->
      <section v-if="!hasAccounts && !hasDependencies" class="detail-section empty-state">
        <q-icon name="info" size="24px" color="grey-6" />
        <p class="empty-message">No accounts belong to this broker and nothing depends on it.</p>
      </section>
    </div>
  </ReadOnlyShell>
</template>

<script setup lang="ts">
import { useRoute, useRouter } from 'vue-router'
import { computed } from 'vue'
import ReadOnlyShell from '../../components/readonly/ReadOnlyShell.vue'
import type { HigherItem, LowerItem } from '../../types/readonly'
import { useMessageBrokers } from '../../composables/useMessageBrokers'
import { useAccounts } from '../../composables/useAccounts'
import { useApplications } from '../../composables/useApplications'
import { useEnvironments } from '../../composables/useEnvironments'
import { useTags } from '../../composables/useTags'
import { TargetKind, type TagColor } from '../../api/client'

const route = useRoute()
const router = useRouter()
const id = computed(() => route.params.id as string)

// Data queries
const { data: messageBrokersData, isLoading: brokersLoading } = useMessageBrokers()
const { data: accountsData, isLoading: accountsLoading } = useAccounts()
const { data: applicationsData, isLoading: appsLoading } = useApplications()
const { lookup: environmentLookup, isLoading: envsLoading } = useEnvironments()
const { tagInfoLookup, isLoading: tagsLoading } = useTags()

const isLoading = computed(() =>
  brokersLoading.value || accountsLoading.value || appsLoading.value || envsLoading.value || tagsLoading.value
)

// Find the message broker by ID
const messageBroker = computed(() => {
  if (!messageBrokersData.value) return null
  return messageBrokersData.value.find((b) => b.id === id.value) ?? null
})

// Page title
const pageTitle = computed(() => {
  const name = messageBroker.value?.name ?? id.value
  return `Message Broker: ${name}`
})

// Resolved tags with name and color
const resolvedTags = computed(() => {
  const tagIds = messageBroker.value?.tagIds ?? []
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
  if (!messageBroker.value?.environmentId) return []
  const envName = environmentLookup.value[messageBroker.value.environmentId] ?? messageBroker.value.environmentId
  return [{ id: messageBroker.value.environmentId, type: 'environment', name: envName }]
})

// Accounts that belong to this message broker
const brokerAccounts = computed(() => {
  if (!accountsData.value || !id.value) return []
  return accountsData.value.filter(
    (account) => account.targetKind === TargetKind.MessageBroker && account.targetId === id.value
  )
})

// Dependencies that target this message broker
const brokerDependencies = computed(() => {
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
        if (dep.targetKind === TargetKind.MessageBroker && dep.targetId === id.value && dep.id) {
          deps.push({
            depId: dep.id,
            depName: `${app.name ?? 'App'} → ${messageBroker.value?.name ?? 'Broker'}`,
            instanceName: instance.id ?? 'Instance',
            appName: app.name ?? 'App'
          })
        }
      }
    }
  }

  return deps
})

const hasAccounts = computed(() => brokerAccounts.value.length > 0)
const hasDependencies = computed(() => brokerDependencies.value.length > 0)

// Lower context: Accounts + Dependencies
const lowerContext = computed<LowerItem[]>(() => {
  const items: LowerItem[] = []

  for (const account of brokerAccounts.value) {
    if (!account.id) continue
    items.push({
      id: account.id,
      type: 'account',
      name: account.userName ?? account.id,
      route: `/view/account/${account.id}`,
      subtitle: 'Account'
    })
  }

  for (const dep of brokerDependencies.value) {
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
.broker-loading,
.broker-error {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  text-align: center;
  padding: 3rem 1rem;
  gap: 1rem;
}

.broker-error h2 {
  margin: 0;
  font-size: 1.5rem;
  font-weight: 600;
}

.broker-error p {
  margin: 0;
  color: var(--fuse-text-muted);
}

.broker-error code {
  background: var(--fuse-panel-bg);
  padding: 0.125rem 0.5rem;
  border-radius: 4px;
  font-family: monospace;
}

.broker-details {
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

.kind-badge {
  font-size: 0.875rem;
}

.connection-uri {
  font-family: monospace;
  font-size: 0.875rem;
  background: var(--fuse-panel-bg);
  padding: 0.25rem 0.5rem;
  border-radius: 4px;
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

.channel-list {
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
}

.channel-item {
  padding: 0.5rem 0.75rem;
  background: var(--fuse-panel-bg, #f8f8f8);
  border: 1px solid var(--fuse-panel-border, #e0e0e0);
  border-radius: 6px;
}

.channel-item-name {
  font-weight: 600;
  font-size: 0.9rem;
}

.channel-item-desc {
  color: var(--fuse-text-muted);
  font-size: 0.85rem;
  margin-top: 0.1rem;
}

.channel-subscribers {
  margin-top: 0.4rem;
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 0.4rem;
}

.subscribers-label {
  font-size: 0.8rem;
  color: var(--fuse-text-muted);
  font-weight: 500;
}

.subscriber-badge {
  font-size: 0.75rem;
}
</style>
