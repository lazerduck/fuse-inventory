<template>
  <div class="search-results">
    <div v-if="isLoading" class="loading-state">
      <q-spinner color="primary" size="24px" />
      <span>Searching...</span>
    </div>

    <div v-else-if="!hasSearched" class="empty-state">
      <q-icon name="search" size="48px" class="text-grey-5" />
      <p>Enter a search term to find applications, instances, accounts, and more.</p>
    </div>

    <div v-else-if="results.length === 0" class="no-results">
      <q-icon name="search_off" size="48px" class="text-grey-5" />
      <p>No results found for "{{ searchQuery }}"</p>
      <p class="hint">Try a different search term or check your spelling.</p>
    </div>

    <template v-else>
      <template v-if="grouped && groupedResults.length > 0">
        <div v-for="group in groupedResults" :key="group.type" class="result-group">
          <div class="group-header">
            <q-icon :name="group.icon" size="20px" />
            <span>{{ group.label }}</span>
            <q-badge color="grey-6" :label="group.results.length" />
          </div>
          <q-list class="result-list" separator>
            <q-item
              v-for="result in group.results"
              :key="result.id"
              clickable
              v-ripple
              @click="handleResultClick(result)"
              class="result-item"
            >
              <q-item-section avatar>
                <q-icon :name="getIconForType(result.type)" color="primary" />
              </q-item-section>
              <q-item-section>
                <q-item-label>{{ result.name }}</q-item-label>
                <q-item-label v-if="result.subtitle" caption>{{ result.subtitle }}</q-item-label>
              </q-item-section>
              <q-item-section side>
                <q-icon name="chevron_right" color="grey-6" />
              </q-item-section>
            </q-item>
          </q-list>
        </div>
      </template>

      <template v-else>
        <q-list class="result-list" separator>
          <q-item
            v-for="result in results"
            :key="result.id"
            clickable
            v-ripple
            @click="handleResultClick(result)"
            class="result-item"
          >
            <q-item-section avatar>
              <q-icon :name="getIconForType(result.type)" color="primary" />
            </q-item-section>
            <q-item-section>
              <q-item-label>{{ result.name }}</q-item-label>
              <q-item-label v-if="result.subtitle" caption>{{ result.subtitle }}</q-item-label>
            </q-item-section>
            <q-item-section side>
              <q-badge :label="getLabelForType(result.type)" color="grey-6" />
            </q-item-section>
            <q-item-section side>
              <q-icon name="chevron_right" color="grey-6" />
            </q-item-section>
          </q-item>
        </q-list>
      </template>

      <div class="results-footer">
        <span class="results-count">{{ results.length }} result{{ results.length === 1 ? '' : 's' }} found</span>
      </div>
    </template>
  </div>
</template>

<script setup lang="ts">
import { useRouter } from 'vue-router'
import type { SearchResult, SearchResultGroup, SearchResultType } from '../../composables/useReadonlySearch'

withDefaults(
  defineProps<{
    results: SearchResult[]
    groupedResults?: SearchResultGroup[]
    searchQuery?: string
    isLoading?: boolean
    hasSearched?: boolean
    grouped?: boolean
  }>(),
  {
    results: () => [],
    groupedResults: () => [],
    searchQuery: '',
    isLoading: false,
    hasSearched: false,
    grouped: true
  }
)

const emit = defineEmits<{
  (e: 'resultSelected', result: SearchResult): void
}>()

const router = useRouter()

const typeConfig: Record<SearchResultType, { label: string; icon: string }> = {
  app: { label: 'Application', icon: 'apps' },
  instance: { label: 'Instance', icon: 'layers' },
  dependency: { label: 'Dependency', icon: 'link' },
  account: { label: 'Account', icon: 'vpn_key' },
  identity: { label: 'Identity', icon: 'badge' },
  datastore: { label: 'Data Store', icon: 'storage' },
  external: { label: 'External', icon: 'hub' }
}

function getIconForType(type: SearchResultType): string {
  return typeConfig[type]?.icon ?? 'help'
}

function getLabelForType(type: SearchResultType): string {
  return typeConfig[type]?.label ?? 'Unknown'
}

function handleResultClick(result: SearchResult) {
  emit('resultSelected', result)
  router.push(result.route)
}
</script>

<style scoped>
.search-results {
  width: 100%;
}

.loading-state,
.empty-state,
.no-results {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  padding: 2rem;
  text-align: center;
  color: var(--fuse-text-muted);
  gap: 0.75rem;
}

.loading-state {
  flex-direction: row;
  gap: 0.5rem;
}

.no-results .hint {
  font-size: 0.875rem;
  color: var(--fuse-text-subtle);
  margin: 0;
}

.result-group {
  margin-bottom: 1rem;
}

.group-header {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  padding: 0.75rem 1rem;
  font-weight: 600;
  color: var(--fuse-text-muted);
  border-bottom: 1px solid var(--fuse-panel-border);
}

.result-list {
  background: var(--fuse-panel-bg);
  border-radius: 8px;
  border: 1px solid var(--fuse-panel-border);
}

.result-item {
  transition: background-color 0.15s ease;
}

.result-item:hover {
  background-color: var(--fuse-hover-bg);
}

.results-footer {
  padding: 0.75rem 1rem;
  text-align: center;
}

.results-count {
  font-size: 0.875rem;
  color: var(--fuse-text-subtle);
}
</style>
