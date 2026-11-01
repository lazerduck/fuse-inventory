<template>
  <div class="search-results" :aria-busy="isLoading">
    <div v-if="isLoading && !results.length" class="results-state" role="status"><q-spinner color="primary" size="28px" /><span>Loading documentation…</span></div>
    <div v-else-if="!hasSearched" class="results-state"><q-icon name="auto_stories" size="40px" /><p>Choose a category or search to explore the catalogue.</p></div>
    <div v-else-if="!results.length" class="results-state" role="status">
      <q-icon :name="hasLoadErrors ? 'cloud_off' : 'search_off'" size="40px" />
      <h3>{{ hasLoadErrors ? 'Documentation is partly unavailable' : searchQuery.trim() ? 'No matching records' : 'No records in this category yet' }}</h3>
      <p>{{ hasLoadErrors ? 'Retry loading above to check the unavailable categories.' : searchQuery.trim() ? 'Try a different name, environment, owner, or tag, or choose another category.' : 'Records appear here when they are added to the inventory.' }}</p>
    </div>
    <template v-else>
      <section v-for="group in displayGroups" :key="group.type" class="result-group">
        <div v-if="grouped" class="group-heading"><q-icon :name="group.icon" size="20px" /><h3>{{ group.label }}</h3></div>
        <q-list class="result-grid">
          <q-item v-for="result in group.results" :key="`${result.type}-${result.id}`" :to="resultRoute(result)" clickable v-ripple class="result-item" @click="emit('resultSelected', result)">
            <q-item-section avatar><div class="result-icon"><q-icon :name="typeConfig[result.type].icon" size="22px" /></div></q-item-section>
            <q-item-section class="result-content">
              <q-item-label class="result-title">{{ result.name }}</q-item-label>
              <q-item-label v-if="result.subtitle" caption class="result-subtitle">{{ result.subtitle }}</q-item-label>
              <div class="result-meta">
                <span>{{ typeConfig[result.type].label }}</span>
                <span v-if="result.environmentName" class="result-environment"><q-icon name="cloud_queue" size="13px" />{{ result.environmentName }}</span>
                <span v-for="tag in (result.tags ?? []).slice(0, 2)" :key="tag" class="result-tag"><q-icon name="label_outline" size="12px" />{{ tag }}</span>
                <span v-if="(result.tags?.length ?? 0) > 2">+{{ result.tags!.length - 2 }}</span>
              </div>
            </q-item-section>
            <q-item-section side><q-icon name="arrow_forward" size="18px" color="primary" /></q-item-section>
          </q-item>
        </q-list>
      </section>
    </template>
  </div>
</template>

<script setup lang="ts">
import { computed } from 'vue'
import type { SearchResult, SearchResultGroup, SearchResultType } from '../../composables/useReadonlySearch'
const props = withDefaults(defineProps<{
  results: SearchResult[]
  groupedResults?: SearchResultGroup[]
  searchQuery?: string
  isLoading?: boolean
  hasSearched?: boolean
  grouped?: boolean
  cataloguePath?: string
  hasLoadErrors?: boolean
}>(), { results: () => [], groupedResults: () => [], searchQuery: '', isLoading: false, hasSearched: false, grouped: true, cataloguePath: '/view', hasLoadErrors: false })
const emit = defineEmits<{ (event: 'resultSelected', result: SearchResult): void }>()
const typeConfig: Record<SearchResultType, { label: string; icon: string }> = {
  app: { label: 'Application', icon: 'apps' }, instance: { label: 'Instance', icon: 'layers' },
  dependency: { label: 'Dependency', icon: 'link' }, account: { label: 'Account', icon: 'vpn_key' },
  identity: { label: 'Identity', icon: 'badge' }, datastore: { label: 'Data store', icon: 'storage' },
  external: { label: 'External resource', icon: 'language' }, platform: { label: 'Platform', icon: 'computer' },
  risk: { label: 'Risk', icon: 'warning' }, messagebroker: { label: 'Message broker', icon: 'swap_horiz' },
  position: { label: 'Position', icon: 'work' }, tag: { label: 'Tag', icon: 'label' }
}
const displayGroups = computed(() => props.grouped && props.groupedResults.length ? props.groupedResults : [{ type: 'all', label: 'Records', icon: 'auto_stories', results: props.results }])
function resultRoute(result: SearchResult) { return { path: result.route, query: { from: props.cataloguePath } } }
</script>

<style scoped>
.search-results { width: 100%; }.results-state { display: flex; align-items: center; justify-content: center; flex-direction: column; gap: 12px; padding: 48px 24px; text-align: center; border: 1px dashed var(--fuse-panel-border); border-radius: 12px; color: var(--fuse-text-muted); }.results-state h3 { font-size: 17px; line-height: 1.4; margin: 4px 0 0; }.results-state p { font-size: 14px; line-height: 1.6; margin: 0; max-width: 55ch; }
.result-group + .result-group { margin-top: 24px; }.group-heading { display: flex; align-items: center; gap: 8px; color: var(--fuse-text-muted); margin-bottom: 12px; }.group-heading h3 { font-size: 14px; font-weight: 600; margin: 0; line-height: 1.4; }.result-grid { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 12px; }.result-item { align-items: start; border: 1px solid var(--fuse-panel-border); background: var(--fuse-panel-bg); border-radius: 10px; padding: 18px 16px; transition: border-color .15s; }.result-item:hover { border-color: var(--q-primary); }.result-item:focus-visible { outline: 2px solid var(--q-primary); outline-offset: 2px; }.result-item :deep(.q-item__section--avatar) { min-width: 50px; padding-right: 12px; }.result-icon { width: 38px; height: 38px; display: grid; place-items: center; border-radius: 10px; color: var(--q-primary); background: color-mix(in srgb, var(--q-primary) 8%, transparent); }.result-title { font-size: 14px; font-weight: 600; overflow-wrap: anywhere; line-height: 1.45 !important; }.result-subtitle { margin-top: 6px; line-height: 1.5 !important; overflow-wrap: anywhere; display: -webkit-box; -webkit-line-clamp: 2; -webkit-box-orient: vertical; overflow: hidden; }.result-meta { display: flex; flex-wrap: wrap; align-items: center; gap: 5px 9px; margin-top: 12px; color: var(--fuse-text-muted); font-size: 11px; }.result-meta > span { display: inline-flex; align-items: center; gap: 4px; max-width: 100%; overflow-wrap: anywhere; }.result-tag { background: var(--fuse-hover-bg); border-radius: 4px; padding: 2px 4px; }
@media (max-width: 1100px) { .result-grid { grid-template-columns: 1fr; } }
</style>
