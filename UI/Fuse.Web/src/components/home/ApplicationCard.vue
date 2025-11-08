<template>
  <q-card class="application-card">
    <q-card-section class="application-header">
      <div class="header-content">
        <div class="app-info">
          <div class="app-avatar">
            <q-icon name="precision_manufacturing" size="24px" />
          </div>
          <div>
            <h3 class="app-title">{{ application.name }}</h3>
            <div class="app-meta">
              <span v-if="application.version" class="app-version">v{{ application.version }}</span>
              <span v-if="application.owner" class="app-owner">{{ application.owner }}</span>
            </div>
          </div>
        </div>
        <div class="header-actions">
          <q-chip
            v-if="application.framework"
            outline
            dense
            color="primary"
            text-color="primary"
            size="sm"
          >
            {{ application.framework }}
          </q-chip>
          <q-btn
            flat
            round
            dense
            icon="edit"
            color="primary"
            size="sm"
            :to="`/applications/${application.id}`"
          >
            <q-tooltip>Edit application</q-tooltip>
          </q-btn>
          <q-btn
            flat
            round
            dense
            :icon="expanded ? 'expand_less' : 'expand_more'"
            color="grey-7"
            size="sm"
            @click="expanded = !expanded"
          >
            <q-tooltip>{{ expanded ? 'Collapse' : 'Expand' }} details</q-tooltip>
          </q-btn>
        </div>
      </div>
    </q-card-section>

    <q-separator v-if="expanded" />

    <q-slide-transition>
      <q-card-section v-show="expanded" class="application-details">
        <div class="details-grid">
          <div class="detail-item">
            <p class="detail-label">Repository</p>
            <p class="detail-value">
              <a
                v-if="application.repositoryUri"
                :href="application.repositoryUri"
                target="_blank"
                rel="noopener"
                class="detail-link"
              >
                {{ application.repositoryUri }}
              </a>
              <span v-else class="text-grey-6">Not linked</span>
            </p>
          </div>
          <div class="detail-item">
            <p class="detail-label">Tags</p>
            <div v-if="application.tagIds?.length" class="tag-list">
              <q-chip
                v-for="tagId in application.tagIds"
                :key="tagId"
                dense
                outline
                color="grey-8"
                size="sm"
              >
                {{ tagLookup[tagId] ?? tagId }}
              </q-chip>
            </div>
            <span v-else class="text-grey-6">No tags</span>
          </div>
        </div>

        <div v-if="(application.instances ?? []).length" class="instances-section">
          <div class="section-header">
            <h4>Instances</h4>
            <span class="instance-count">{{ (application.instances ?? []).length }}</span>
          </div>
          <div class="instances-list">
            <InstanceCard
              v-for="instance in application.instances"
              :key="instance.id"
              :instance="instance"
              :environment-lookup="environmentLookup"
              :server-lookup="serverLookup"
              :tag-lookup="tagLookup"
              :dependency-formatter="formatDependencyLabel"
            />
          </div>
        </div>
        <div v-else class="no-instances">
          <q-icon name="inventory_2" size="20px" class="text-grey-5" />
          <span>No instances configured</span>
        </div>
      </q-card-section>
    </q-slide-transition>
  </q-card>
</template>

<script setup lang="ts">
import { ref } from 'vue'
import type { Application } from '../../api/client'
import InstanceCard from './InstanceCard.vue'

defineProps<{
  application: Application
  environmentLookup: Record<string, string>
  serverLookup: Record<string, string>
  tagLookup: Record<string, string>
  formatDependencyLabel: (dependency: any) => string
}>()

const expanded = ref(false)
</script>

<style scoped>
.application-card {
  border-radius: 12px;
  box-shadow: var(--fuse-shadow-1);
  transition: box-shadow 0.2s ease;
}

.application-card:hover {
  box-shadow: var(--fuse-shadow-2);
}

.application-header {
  padding: 1rem;
}

.header-content {
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
  gap: 1rem;
}

.app-info {
  display: flex;
  align-items: center;
  gap: 0.75rem;
  flex: 1;
  min-width: 0;
}

.app-avatar {
  width: 40px;
  height: 40px;
  border-radius: 10px;
  display: grid;
  place-items: center;
  background: rgba(64, 120, 255, 0.1);
  flex-shrink: 0;
}

.app-title {
  font-size: 1.125rem;
  font-weight: 600;
  margin: 0 0 0.25rem;
  line-height: 1.2;
}

.app-meta {
  display: flex;
  gap: 0.5rem;
  align-items: center;
  font-size: 0.875rem;
  color: var(--fuse-text-muted);
}

.app-version {
  background: rgba(64, 120, 255, 0.1);
  padding: 0.125rem 0.5rem;
  border-radius: 999px;
  font-size: 0.75rem;
}

.header-actions {
  display: flex;
  gap: 0.25rem;
  align-items: center;
  flex-shrink: 0;
}

.application-details {
  padding: 1rem;
  display: flex;
  flex-direction: column;
  gap: 1rem;
}

.details-grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(200px, 1fr));
  gap: 1rem;
}

.detail-item {
  min-width: 0;
}

.detail-label {
  font-size: 0.75rem;
  text-transform: uppercase;
  letter-spacing: 0.05em;
  color: var(--fuse-text-subtle);
  margin: 0 0 0.375rem;
  font-weight: 500;
}

.detail-value {
  margin: 0;
  word-break: break-word;
}

.detail-link {
  color: var(--q-primary);
  text-decoration: none;
}

.detail-link:hover {
  text-decoration: underline;
}

.tag-list {
  display: flex;
  flex-wrap: wrap;
  gap: 0.375rem;
}

.instances-section {
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
}

.section-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
}

.section-header h4 {
  margin: 0;
  font-size: 1rem;
  font-weight: 600;
}

.instance-count {
  font-size: 0.875rem;
  color: var(--fuse-text-subtle);
  background: var(--fuse-panel-bg);
  padding: 0.25rem 0.625rem;
  border-radius: 999px;
}

.instances-list {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(280px, 1fr));
  gap: 0.75rem;
}

.no-instances {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  padding: 1rem;
  background: var(--fuse-panel-bg);
  border: 1px solid var(--fuse-panel-border);
  border-radius: 8px;
  color: var(--fuse-text-subtle);
  font-size: 0.875rem;
}
</style>
