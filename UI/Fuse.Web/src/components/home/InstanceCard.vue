<template>
  <q-card class="instance-card" flat bordered>
    <q-card-section class="instance-content">
      <div class="instance-header">
        <q-badge
          outline
          color="primary"
          :label="environmentLookup[instance.environmentId ?? ''] ?? 'No environment'"
        />
        <q-chip
          v-if="instance.version"
          dense
          color="primary"
          text-color="white"
          size="xs"
        >
          v{{ instance.version }}
        </q-chip>
      </div>

      <div class="instance-info">
        <p class="instance-url">
          {{ instance.baseUri ?? 'No base URI' }}
        </p>
        <p class="instance-platform">
          {{ platformLookup[instance.platformId ?? ''] ?? 'No platform' }}
        </p>
      </div>

      <div class="instance-actions">
        <q-btn
          v-if="instance.baseUri"
          flat
          dense
          no-caps
          icon="launch"
          label="Base"
          color="primary"
          size="sm"
          :href="instance.baseUri"
          target="_blank"
          rel="noopener"
        />
        <q-btn
          v-if="instance.healthUri"
          flat
          dense
          no-caps
          icon="favorite"
          label="Health"
          color="positive"
          size="sm"
          :href="instance.healthUri"
          target="_blank"
          rel="noopener"
        />
        <q-btn
          v-if="instance.openApiUri"
          flat
          dense
          no-caps
          icon="api"
          label="API"
          color="secondary"
          size="sm"
          :href="instance.openApiUri"
          target="_blank"
          rel="noopener"
        />
      </div>

      <div v-if="instance.tagIds?.length" class="instance-tags">
        <q-chip
          v-for="tagId in instance.tagIds"
          :key="tagId"
          dense
          outline
          color="grey-7"
          size="xs"
        >
          {{ tagLookup[tagId] ?? tagId }}
        </q-chip>
      </div>

      <div v-if="instance.dependencies?.length" class="instance-dependencies">
        <p class="dependencies-label">Dependencies:</p>
        <div class="dependency-list">
          <q-chip
            v-for="dependency in instance.dependencies"
            :key="dependency.id"
            dense
            outline
            color="secondary"
            size="xs"
          >
            {{ dependencyFormatter(dependency) }}
          </q-chip>
        </div>
      </div>
    </q-card-section>
  </q-card>
</template>

<script setup lang="ts">
import type { ApplicationInstance } from '../../api/client'

defineProps<{
  instance: ApplicationInstance
  environmentLookup: Record<string, string>
  platformLookup: Record<string, string>
  tagLookup: Record<string, string>
  dependencyFormatter: (dependency: any) => string
}>()
</script>

<style scoped>
.instance-card {
  border-radius: 10px;
  border: 1px solid var(--fuse-panel-border);
  background: var(--fuse-panel-bg);
}

.instance-content {
  padding: 0.875rem;
  display: flex;
  flex-direction: column;
  gap: 0.625rem;
}

.instance-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: 0.5rem;
}

.instance-info {
  display: flex;
  flex-direction: column;
  gap: 0.25rem;
}

.instance-url {
  font-weight: 600;
  font-size: 0.875rem;
  margin: 0;
  word-break: break-all;
}

.instance-platform {
  font-size: 0.8125rem;
  color: var(--fuse-text-muted);
  margin: 0;
}

.instance-actions {
  display: flex;
  flex-wrap: wrap;
  gap: 0.375rem;
}

.instance-tags,
.dependency-list {
  display: flex;
  flex-wrap: wrap;
  gap: 0.375rem;
}

.instance-dependencies {
  display: flex;
  flex-direction: column;
  gap: 0.375rem;
}

.dependencies-label {
  font-size: 0.75rem;
  text-transform: uppercase;
  letter-spacing: 0.05em;
  color: var(--fuse-text-subtle);
  margin: 0;
  font-weight: 500;
}
</style>
