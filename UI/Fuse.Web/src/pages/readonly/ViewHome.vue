<template>
  <div class="view-home">
    <section class="page-header">
      <div>
        <h1>Documentation Mode</h1>
        <p class="subtitle">
          Search across your entire inventory to explore applications, instances, accounts, and more.
        </p>
      </div>
    </section>

    <section class="search-section">
      <SearchBar
        v-model="searchQuery"
        :is-loading="isLoading"
        :show-search-button="true"
        placeholder="Search applications, instances, platforms, accounts, data stores..."
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

.search-section {
  display: flex;
  justify-content: center;
}

.results-section {
  margin-top: 1rem;
}
</style>
