<template>
  <div class="view-home">
    <section class="page-header">
      <div>
        <h1>Documentation Mode</h1>
        <p class="subtitle">
          Search across applications, instances, dependencies, risks, platforms, datastores, brokers, tags, and governance roles.
        </p>
        <div class="entity-chips">
          <q-chip dense outline icon="apps" color="primary">Applications</q-chip>
          <q-chip dense outline icon="warning" color="negative">Risks</q-chip>
          <q-chip dense outline icon="work" color="secondary">Positions</q-chip>
          <q-chip dense outline icon="label" color="teal">Tags</q-chip>
          <q-chip dense outline icon="swap_horiz" color="orange">Message Brokers</q-chip>
        </div>
      </div>
    </section>

    <section class="search-section">
      <SearchBar
        v-model="searchQuery"
        :is-loading="isLoading"
        :show-search-button="true"
        placeholder="Search apps, risks, positions, tags, brokers, datastores, identities..."
        @search="handleSearch"
        @clear="handleClear"
      />
    </section>

    <section class="results-section">
      <SearchResults
        :results="searchResults"
        :grouped-results="groupedResults"
        :search-query="searchQuery"
        :is-loading="isLoading"
        :has-searched="hasSearched"
        :grouped="true"
        @result-selected="handleResultSelected"
      />
    </section>
  </div>
</template>

<script setup lang="ts">
import { ref, watch } from 'vue'
import SearchBar from '../../components/readonly/SearchBar.vue'
import SearchResults from '../../components/readonly/SearchResults.vue'
import { useReadonlySearch, type SearchResult } from '../../composables/useReadonlySearch'

const {
  searchQuery,
  searchResults,
  groupedResults,
  isLoading,
  setSearchQuery,
  clearSearch
} = useReadonlySearch()

const hasSearched = ref(false)

function handleSearch(query: string) {
  setSearchQuery(query)
  if (query.trim()) {
    hasSearched.value = true
  }
}

function handleClear() {
  clearSearch()
  hasSearched.value = false
}

function handleResultSelected(_result: SearchResult) {
  // Navigation is handled by SearchResults component
}

watch(searchQuery, (newQuery) => {
  if (newQuery.trim()) {
    hasSearched.value = true
  }
})
</script>

<style scoped>
.view-home {
  padding: 1.5rem clamp(1.25rem, 3vw, 2.5rem);
  display: flex;
  flex-direction: column;
  gap: 1.5rem;
  max-width: 900px;
  margin: 0 auto;
}

.page-header h1 {
  font-size: 1.75rem;
  margin: 0 0 0.375rem;
  font-weight: 600;
}

.subtitle {
  margin: 0;
  font-size: 0.9375rem;
  color: var(--fuse-text-muted);
  max-width: 60ch;
}

.entity-chips {
  margin-top: 0.875rem;
  display: flex;
  flex-wrap: wrap;
  gap: 0.5rem;
}

.search-section {
  display: flex;
  justify-content: center;
}

.results-section {
  margin-top: 1rem;
}
</style>
