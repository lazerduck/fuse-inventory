<template>
  <div class="documentation-home">
    <section class="catalogue-hero">
      <div class="hero-copy">
        <div class="eyebrow"><q-icon name="auto_stories" size="20px" />Inventory catalogue</div>
        <h1>Documentation</h1>
        <p>Find a service, understand its dependencies, and follow the people and resources behind it.</p>
        <div class="hero-links">
          <q-btn v-if="canOpenGraph" outline no-caps color="primary" icon="account_tree" label="Explore the graph" :to="{ name: 'graph' }" />
          <span><q-icon name="visibility" size="16px" />Browse the inventory in read-only views</span>
        </div>
      </div>
      <div class="catalogue-summary" aria-label="Catalogue overview">
        <div class="summary-caption">In your catalogue</div>
        <strong>{{ isLoading ? '…' : totalCount.toLocaleString() }}</strong>
        <span>documented records</span>
        <div class="summary-types">{{ availableTypes.length }} categories available</div>
      </div>
    </section>

    <section class="catalogue-search" aria-label="Search documentation">
      <SearchBar v-model="searchQuery" :is-loading="isSearching" placeholder="Search by name, environment, owner, or tag…" @search="beginSearch" @clear="clearText" />
      <q-select v-model="selectedType" :options="categoryOptions" emit-value map-options outlined dense label="Category" class="category-select" />
    </section>

    <q-banner v-if="errors.length" rounded class="catalogue-warning" role="alert">
      <strong>Some documentation could not be loaded.</strong>
      <div v-for="error in errors" :key="error.key">{{ error.label }}: {{ error.message }}</div>
      <template #action><q-btn flat no-caps color="primary" label="Retry loading" :loading="isSearching" @click="retry" /></template>
    </q-banner>
    <p v-if="unavailableTypes.length" class="access-note"><q-icon name="lock_outline" />Categories you do not have permission to read are unavailable.</p>

    <template v-if="!showResults">
      <div class="section-heading">
        <div><h2>Browse by category</h2><p>Start with what you know, then follow the connections.</p></div>
        <q-btn flat no-caps color="primary" icon-right="arrow_forward" label="Browse all records" :disable="!hasAccess" @click="browseAll = true" />
      </div>
      <div class="category-grid">
        <button v-for="category in typeOptions" :key="category.type" type="button" class="category-card"
          :class="{ unavailable: category.status === 'unavailable' }" :disabled="category.status === 'unavailable'"
          :aria-label="`Browse ${category.label}`" @click="selectedType = category.type">
          <div class="category-top"><span class="category-icon" :class="category.type"><q-icon :name="category.icon" size="25px" /></span>
            <q-spinner v-if="category.status === 'loading'" color="primary" size="20px" />
            <q-icon v-else-if="category.status === 'unavailable'" name="lock_outline" size="18px" />
            <q-icon v-else-if="category.status === 'error'" name="error_outline" color="warning" size="20px" />
            <span v-else class="category-count">{{ category.count.toLocaleString() }}</span>
          </div>
          <h3>{{ category.label }}</h3>
          <p>{{ categoryDescriptions[category.type] }}</p>
          <div class="category-bottom"><span>{{ category.status === 'unavailable' ? 'Read access required' : category.status === 'error' ? 'Temporarily unavailable' : 'Explore records' }}</span><q-icon v-if="category.status !== 'unavailable'" name="arrow_forward" size="17px" /></div>
        </button>
      </div>
      <div v-if="!isLoading && hasAccess && totalCount === 0 && !errors.length" class="catalogue-empty">
        <q-icon name="library_books" size="32px" /><div><strong>Your catalogue is ready.</strong><p>Documentation appears here as inventory records are added.</p></div>
      </div>
    </template>

    <section v-else class="catalogue-results" aria-label="Documentation results">
      <div class="section-heading">
        <div><h2>{{ selectedCategoryLabel }}</h2><p role="status">{{ isLoading ? 'Loading documentation…' : `${searchResults.length.toLocaleString()} matching record${searchResults.length === 1 ? '' : 's'}` }}<template v-if="searchQuery.trim()"> for “{{ searchQuery.trim() }}”</template></p></div>
        <q-btn flat no-caps icon="grid_view" color="primary" label="Back to categories" @click="resetCatalogue" />
      </div>
      <q-banner v-if="selectedUnavailable" rounded class="catalogue-warning" role="status">Read access is required to browse {{ selectedCategoryLabel.toLowerCase() }}.</q-banner>
      <SearchResults v-else :results="pageResults" :grouped-results="pageGroups" :search-query="searchQuery" :is-loading="isLoading"
        :has-searched="true" :grouped="selectedType === 'all'" :catalogue-path="cataloguePath" :has-load-errors="selectedType === 'all' ? errors.length > 0 : typeOptions.find(type => type.type === selectedType)?.status === 'error'" />
      <div v-if="searchResults.length > pageSize" class="catalogue-pagination">
        <span>{{ (page - 1) * pageSize + 1 }}–{{ Math.min(page * pageSize, searchResults.length) }} of {{ searchResults.length }}</span>
        <q-pagination v-model="page" :max="pageCount" :max-pages="5" boundary-numbers direction-links color="primary" aria-label="Documentation results pages" />
      </div>
    </section>
  </div>
</template>

<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import SearchBar from '../../components/readonly/SearchBar.vue'
import SearchResults from '../../components/readonly/SearchResults.vue'
import { useReadonlySearch, type SearchResultType } from '../../composables/useReadonlySearch'
import { useFuseStore } from '../../stores/FuseStore'

const { searchQuery, searchResults, groupedResults, isLoading, isSearching, selectedTypes, typeOptions, totalCount, unavailableTypes, errors, retry, hasAccess } = useReadonlySearch()
const store = useFuseStore()
const route = useRoute()
const router = useRouter()
const browseAll = ref(false)
const page = ref(1)
const pageSize = 24
const canOpenGraph = computed(() => store.hasPermission('application:read'))
const availableTypes = computed(() => typeOptions.value.filter(type => type.status !== 'unavailable'))
const categoryOptions = computed(() => [{ label: 'All categories', value: 'all' }, ...typeOptions.value.map(type => ({ label: type.label, value: type.type, disable: type.status === 'unavailable' }))])
const selectedType = computed({
  get: () => selectedTypes.value[0] ?? 'all',
  set: (type: string) => { selectedTypes.value = type === 'all' ? [] : [type as SearchResultType] }
})
const selectedUnavailable = computed(() => typeOptions.value.find(type => type.type === selectedType.value)?.status === 'unavailable')
const selectedCategoryLabel = computed(() => typeOptions.value.find(type => type.type === selectedType.value)?.label ?? 'All records')
const showResults = computed(() => browseAll.value || !!searchQuery.value.trim() || selectedType.value !== 'all')
const pageCount = computed(() => Math.max(1, Math.ceil(searchResults.value.length / pageSize)))
const pageResults = computed(() => searchResults.value.slice((page.value - 1) * pageSize, page.value * pageSize))
const pageGroups = computed(() => groupedResults.value.map(group => ({ ...group, results: group.results.filter(result => pageResults.value.includes(result)) })).filter(group => group.results.length))
const categoryDescriptions: Record<SearchResultType, string> = {
  app: 'Services, ownership, versions, and delivery pipelines.',
  instance: 'Where applications run and what each deployment uses.',
  dependency: 'Connections, authentication, and failure impact.',
  datastore: 'Databases and storage across your environments.',
  messagebroker: 'Messaging infrastructure and the services using it.',
  external: 'Third-party services and other external systems.',
  platform: 'The infrastructure hosting your applications and data.',
  account: 'Account ownership, authentication, and linked resources.',
  identity: 'Shared and instance identities and their associations.',
  risk: 'Documented risks, their impact, and affected resources.',
  position: 'Organisational roles and assigned responsibilities.',
  tag: 'Find related records through shared classifications.'
}
function beginSearch() { browseAll.value = true }
function clearText() { searchQuery.value = ''; page.value = 1 }
function resetCatalogue() { searchQuery.value = ''; selectedTypes.value = []; browseAll.value = false; page.value = 1 }
function readQuery() {
  searchQuery.value = typeof route.query.q === 'string' ? route.query.q : ''
  selectedTypes.value = typeof route.query.type === 'string' && typeOptions.value.some(type => type.type === route.query.type) ? [route.query.type as SearchResultType] : []
  browseAll.value = route.query.browse === 'all'
  const requestedPage = Number(route.query.page)
  page.value = Number.isSafeInteger(requestedPage) && requestedPage > 0 ? requestedPage : 1
}
function viewQuery() {
  const query: Record<string, string> = {}
  if (searchQuery.value.trim()) query.q = searchQuery.value
  if (selectedType.value !== 'all') query.type = selectedType.value
  if (browseAll.value) query.browse = 'all'
  if (page.value > 1) query.page = String(page.value)
  return query
}
const cataloguePath = computed(() => router.resolve({ name: 'viewHome', query: viewQuery() }).fullPath)
readQuery()
let readingQuery = false
watch(() => route.query, () => { readingQuery = true; readQuery(); readingQuery = false })
watch([searchQuery, selectedType], () => { if (!readingQuery) page.value = 1 }, { flush: 'sync' })
watch([searchQuery, selectedType, browseAll, page], () => {
  if (cataloguePath.value !== route.fullPath) void router.replace({ name: 'viewHome', query: viewQuery() })
})
watch([pageCount, isLoading, hasAccess], () => { if (hasAccess.value && !isLoading.value && page.value > pageCount.value) page.value = pageCount.value }, { immediate: true })
</script>

<style scoped>
.documentation-home { max-width: 1320px; margin: 0 auto; padding: 30px clamp(20px, 3vw, 40px) 48px; display: flex; flex-direction: column; gap: 24px; }
.catalogue-hero { display: grid; grid-template-columns: minmax(0, 1fr) 220px; align-items: center; gap: 32px; padding: 32px; border: 1px solid var(--fuse-panel-border); border-radius: 16px; background: linear-gradient(125deg, color-mix(in srgb, var(--q-primary) 7%, var(--fuse-panel-bg)), var(--fuse-panel-bg) 70%); }
.eyebrow { display: flex; align-items: center; gap: 8px; color: var(--q-primary); font-size: 12px; letter-spacing: .09em; text-transform: uppercase; font-weight: 600; }
h1 { margin: 10px 0 12px; font-size: clamp(28px, 3vw, 38px); line-height: 1.2; font-weight: 650; letter-spacing: -.02em; }
.hero-copy > p { max-width: 60ch; color: var(--fuse-text-muted); font-size: 15px; line-height: 1.6; margin-bottom: 22px; }
.hero-links { display: flex; align-items: center; flex-wrap: wrap; gap: 16px; }
.hero-links > span { display: inline-flex; align-items: center; gap: 6px; color: var(--fuse-text-muted); font-size: 12px; }
.catalogue-summary { border-left: 1px solid var(--fuse-panel-border); padding-left: 28px; display: flex; flex-direction: column; gap: 5px; }
.summary-caption { font-size: 12px; color: var(--fuse-text-muted); }.catalogue-summary strong { font-size: 44px; line-height: 1.2; font-weight: 600; letter-spacing: -.03em; }.catalogue-summary > span { font-size: 13px; }.summary-types { font-size: 12px; color: var(--fuse-text-muted); margin-top: 12px; }
.catalogue-search { display: flex; align-items: start; gap: 16px; }.catalogue-search :deep(.search-bar) { flex: 1; max-width: none; }.category-select { width: 230px; }
.section-heading { display: flex; gap: 16px; align-items: center; justify-content: space-between; }.section-heading h2 { font-size: 19px; font-weight: 600; margin: 0 0 5px; line-height: 1.4; }.section-heading p { margin: 0; font-size: 13px; color: var(--fuse-text-muted); }
.category-grid { display: grid; grid-template-columns: repeat(3, minmax(0, 1fr)); gap: 16px; }.category-card { appearance: none; color: inherit; text-align: left; font: inherit; background: var(--fuse-panel-bg); border: 1px solid var(--fuse-panel-border); border-radius: 12px; padding: 22px; display: flex; flex-direction: column; cursor: pointer; transition: border-color .15s, box-shadow .15s; }.category-card:hover:not(:disabled) { border-color: var(--q-primary); box-shadow: 0 3px 14px #00000008; }.category-card:focus-visible { outline: 3px solid var(--q-primary); outline-offset: 3px; }.category-card.unavailable { opacity: .6; cursor: default; }
.category-top { display: flex; justify-content: space-between; align-items: center; }.category-icon { width: 46px; height: 46px; display: grid; place-items: center; border-radius: 12px; color: var(--q-primary); background: color-mix(in srgb, var(--q-primary) 10%, transparent); }.category-icon.risk { color: #d97706; background: #d9770617; }.category-icon.identity, .category-icon.account { color: #8b5cf6; background: #8b5cf617; }.category-icon.position, .category-icon.tag { color: #0d9488; background: #0d948817; }.category-count { font-size: 22px; font-weight: 550; font-variant-numeric: tabular-nums; }
.category-card h3 { font-size: 16px; font-weight: 600; line-height: 1.4; margin: 18px 0 7px; }.category-card p { color: var(--fuse-text-muted); font-size: 13px; line-height: 1.55; margin: 0 0 20px; flex: 1; }.category-bottom { display: flex; justify-content: space-between; align-items: center; font-size: 12px; color: var(--q-primary); }
.catalogue-results { display: flex; flex-direction: column; gap: 22px; }.catalogue-pagination { display: flex; justify-content: space-between; align-items: center; gap: 16px; color: var(--fuse-text-muted); font-size: 12px; }.catalogue-warning { background: var(--fuse-panel-bg); border: 1px solid #d97706; }.access-note { color: var(--fuse-text-muted); font-size: 12px; margin: -8px 0 0; }.catalogue-empty { display: flex; align-items: center; justify-content: center; gap: 16px; padding: 24px; color: var(--fuse-text-muted); }.catalogue-empty p { margin: 5px 0 0; }
@media (max-width: 1150px) { .category-grid { grid-template-columns: repeat(2, minmax(0, 1fr)); }.catalogue-hero { padding: 24px; grid-template-columns: minmax(0, 1fr) 170px; } }
@media (max-width: 700px) { .documentation-home { padding: 20px 16px 32px; gap: 20px; }.catalogue-hero { grid-template-columns: 1fr; gap: 20px; }.catalogue-summary { border-left: 0; border-top: 1px solid var(--fuse-panel-border); padding: 16px 0 0; }.catalogue-summary strong { font-size: 30px; }.summary-types { margin-top: 2px; }.catalogue-search { flex-direction: column; }.catalogue-search :deep(.search-bar), .category-select { width: 100%; }.section-heading, .catalogue-pagination { align-items: start; flex-direction: column; }.category-grid { grid-template-columns: 1fr; }.category-card { padding: 20px; } }
</style>
