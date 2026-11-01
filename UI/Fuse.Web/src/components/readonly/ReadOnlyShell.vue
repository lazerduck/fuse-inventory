<template>
  <div class="readonly-shell">
    <header class="shell-header">
      <div class="header-content">
        <div class="header-heading">
          <div class="shell-eyebrow">Documentation</div>
          <h1 class="shell-title">{{ title }}</h1>
        </div>
        <nav class="header-actions" aria-label="Documentation navigation">
          <q-btn flat no-caps icon="arrow_back" label="Back to catalogue" :to="cataloguePath" />
          <q-btn v-if="graphNodeId" outline no-caps color="primary" icon="account_tree" label="View in graph" :to="{ name: 'graph', query: { node: graphNodeId } }" />
        </nav>
      </div>
    </header>

    <div class="shell-columns">
      <!-- Details come first for keyboard users and on narrow screens. -->
      <main class="column-center" aria-label="Resource details">
        <div class="center-content"><slot /></div>
      </main>
      <aside class="context-column column-left" aria-label="Resource context">
        <ReadOnlyContextList :items="higher" title="Context" header-icon="layers" empty-message="No additional context recorded." />
      </aside>
      <aside class="context-column column-right" aria-label="Related resources">
        <ReadOnlyContextList :items="lower" title="Related resources" header-icon="hub" empty-message="No related resources recorded." />
      </aside>
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed } from 'vue'
import { useRoute } from 'vue-router'
import ReadOnlyContextList from './ReadOnlyContextList.vue'
import type { HigherItem, LowerItem } from '../../types/readonly'
import { validateDocumentationOrigin } from '../../utils/documentationNavigation'
import { useFuseStore } from '../../stores/FuseStore'

withDefaults(defineProps<{ higher?: HigherItem[]; lower?: LowerItem[]; title: string }>(), {
  higher: () => [], lower: () => []
})
const route = useRoute()
const store = useFuseStore()
const cataloguePath = computed(() => validateDocumentationOrigin(route.query.from) ?? '/view')
const graphNodeId = computed(() => {
  if (!store.hasPermission('application:read')) return null
  const prefixes: Record<string, string> = { viewInstance: 'appi', viewDatastore: 'ds', viewExternal: 'ext', viewMessageBroker: 'mb' }
  const prefix = prefixes[String(route.name)]
  const id = route.params.id
  return prefix && typeof id === 'string' && id ? `${prefix}-${id}` : null
})
</script>

<style scoped>
.readonly-shell { min-height: calc(100dvh - 64px); color: var(--fuse-text); }
.shell-header { background: var(--fuse-panel-bg); border-bottom: 1px solid var(--fuse-panel-border); }
.header-content { display: flex; align-items: center; justify-content: space-between; flex-wrap: wrap; gap: 1rem; max-width: 1600px; margin: 0 auto; padding: 1.5rem clamp(1rem, 2vw, 2rem); }
.header-heading { min-width: 0; flex: 1 1 260px; }
.shell-eyebrow { color: var(--fuse-text-muted); font-size: 0.75rem; font-weight: 600; letter-spacing: 0.08em; text-transform: uppercase; margin-bottom: 0.35rem; }
.shell-title { margin: 0; font-size: clamp(1.3rem, 2vw, 1.8rem); line-height: 1.3; font-weight: 650; overflow-wrap: anywhere; }
.header-actions { display: flex; align-items: center; flex-wrap: wrap; gap: 0.5rem; }
.shell-columns { display: grid; grid-template-columns: minmax(180px, 230px) minmax(0, 1fr) minmax(200px, 270px); grid-template-areas: 'context details related'; align-items: start; gap: 1.25rem; width: 100%; max-width: 1600px; margin: 0 auto; padding: 1.5rem clamp(1rem, 2vw, 2rem); }
.column-center { grid-area: details; min-width: 0; background: var(--fuse-card-bg); border: 1px solid var(--fuse-panel-border); border-radius: 6px; }
.center-content { padding: clamp(1rem, 2vw, 1.75rem); overflow-wrap: anywhere; }
.context-column { min-width: 0; position: sticky; top: 5.25rem; max-height: calc(100dvh - 8rem); overflow-y: auto; border: 1px solid var(--fuse-panel-border); border-radius: 6px; background: var(--fuse-card-bg); }
.column-left { grid-area: context; }
.column-right { grid-area: related; }
@media (max-width: 1100px) {
  .shell-columns { grid-template-columns: repeat(2, minmax(0, 1fr)); grid-template-areas: 'details details' 'context related'; }
  .context-column { position: static; max-height: 440px; }
}
@media (max-width: 760px) {
  .header-content { padding: 1.15rem 1rem; }
  .header-actions { width: 100%; }
  .shell-columns { grid-template-columns: minmax(0, 1fr); grid-template-areas: 'details' 'context' 'related'; gap: 1rem; padding: 1rem; }
  .context-column { position: static; max-height: 380px; }
}
</style>
