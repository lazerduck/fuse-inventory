<template>
  <section class="context-list" :aria-label="title">
    <div class="context-header">
      <q-icon :name="headerIcon" size="19px" color="primary" />
      <h2 class="header-title">{{ title }}</h2>
      <q-badge color="grey-3" text-color="grey-9" :label="items.length" :aria-label="`${items.length} records`" />
    </div>
    <div v-if="items.length > 8" class="context-filter">
      <q-input v-model="filter" dense outlined clearable :aria-label="`Filter ${title.toLowerCase()}`" placeholder="Filter records">
        <template #prepend><q-icon name="search" size="18px" /></template>
      </q-input>
    </div>
    <div v-if="!items.length" class="empty-state">{{ emptyMessage }}</div>
    <div v-else-if="!filteredItems.length" class="empty-state" role="status">No matching records.</div>
    <q-list v-else class="item-list" separator>
      <q-item v-for="item in filteredItems" :key="`${item.type}:${item.id}`" :to="destination(item)" :clickable="!!item.route" v-ripple="!!item.route" class="context-item" :class="{ 'non-clickable': !item.route }">
        <q-item-section avatar class="item-icon"><q-icon :name="getIconForEntityType(item.type)" color="primary" size="20px" /></q-item-section>
        <q-item-section>
          <q-item-label class="item-name">{{ item.name }}</q-item-label>
          <q-item-label caption class="item-type">{{ getLabelForEntityType(item.type) }}</q-item-label>
          <q-item-label v-if="item.subtitle && item.subtitle !== item.name" caption class="item-subtitle">{{ item.subtitle }}</q-item-label>
        </q-item-section>
        <q-item-section v-if="item.route" side><q-icon name="chevron_right" size="18px" /></q-item-section>
      </q-item>
    </q-list>
  </section>
</template>

<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { useRoute, useRouter, type RouteLocationRaw } from 'vue-router'
import type { HigherItem, LowerItem } from '../../types/readonly'
import { getIconForEntityType, getLabelForEntityType } from '../../types/readonly'
import { validateDocumentationOrigin } from '../../utils/documentationNavigation'

export type ContextItem = HigherItem | LowerItem
const props = withDefaults(defineProps<{ items: ContextItem[]; title: string; headerIcon?: string; emptyMessage?: string }>(), {
  items: () => [], headerIcon: 'list', emptyMessage: 'No records'
})
const route = useRoute()
const router = useRouter()
const filter = ref<string | null>('')
const filteredItems = computed(() => {
  const query = (filter.value ?? '').trim().toLocaleLowerCase()
  return query ? props.items.filter(item => [item.name, item.subtitle, getLabelForEntityType(item.type)].some(value => value?.toLocaleLowerCase().includes(query))) : props.items
})
watch(() => route.fullPath, () => { filter.value = '' })
function destination(item: ContextItem): RouteLocationRaw | undefined {
  if (!item.route) return undefined
  const target = router.resolve(item.route)
  if (!target.path.startsWith('/view/')) return item.route
  const from = validateDocumentationOrigin(route.query.from) ?? '/view'
  return { path: target.path, query: { ...target.query, from }, hash: target.hash }
}
</script>

<style scoped>
.context-list { min-width: 0; }
.context-header { display: flex; align-items: center; gap: 0.5rem; padding: 0.9rem 1rem; background: var(--fuse-panel-bg); border-bottom: 1px solid var(--fuse-panel-border); }
.header-title { flex: 1; margin: 0; font-size: 0.875rem; font-weight: 650; line-height: 1.4; color: var(--fuse-text); }
.context-filter { padding: 0.75rem; border-bottom: 1px solid var(--fuse-panel-border); }
.empty-state { padding: 1.2rem 1rem; color: var(--fuse-text-muted); font-size: 0.85rem; line-height: 1.6; }
.context-item { min-height: 68px; padding: 0.85rem 0.9rem; transition: background-color 0.15s ease; }
.context-item:hover:not(.non-clickable) { background: var(--fuse-hover-bg); }
.context-item.non-clickable { cursor: default; }
.item-icon { min-width: 30px; padding-right: 0.6rem; }
.item-name { font-weight: 550; line-height: 1.4; overflow-wrap: anywhere; }
.item-type { color: var(--fuse-text-muted); margin-top: 0.25rem; }
.item-subtitle { color: var(--fuse-text-muted); overflow-wrap: anywhere; }
</style>
