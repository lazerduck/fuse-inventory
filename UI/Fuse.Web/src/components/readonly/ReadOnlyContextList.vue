<template>
  <div class="context-list">
    <div class="context-header">
      <q-icon :name="headerIcon" size="20px" />
      <span class="header-title">{{ title }}</span>
      <q-badge v-if="items.length > 0" color="grey-6" :label="items.length" />
    </div>

    <div v-if="items.length === 0" class="empty-state">
      <q-icon name="inbox" size="32px" class="text-grey-5" />
      <p>{{ emptyMessage }}</p>
    </div>

    <q-list v-else class="item-list" separator>
      <q-item
        v-for="item in items"
        :key="item.id"
        clickable
        v-ripple
        @click="handleItemClick(item)"
        class="context-item"
      >
        <q-item-section avatar>
          <q-icon :name="getIconForEntityType(item.type)" color="primary" />
        </q-item-section>
        <q-item-section>
          <q-item-label>{{ item.name }}</q-item-label>
          <q-item-label v-if="item.subtitle" caption>{{ item.subtitle }}</q-item-label>
        </q-item-section>
        <q-item-section side>
          <q-icon name="chevron_right" color="grey-6" />
        </q-item-section>
      </q-item>
    </q-list>
  </div>
</template>

<script setup lang="ts">
import { useRouter } from 'vue-router'
import type { HigherItem, LowerItem } from '../../types/readonly'
import { getIconForEntityType } from '../../types/readonly'

export type ContextItem = HigherItem | LowerItem

withDefaults(
  defineProps<{
    items: ContextItem[]
    title: string
    headerIcon?: string
    emptyMessage?: string
  }>(),
  {
    items: () => [],
    headerIcon: 'list',
    emptyMessage: 'No items'
  }
)

const router = useRouter()

function handleItemClick(item: ContextItem) {
  router.push(item.route)
}
</script>

<style scoped>
.context-list {
  display: flex;
  flex-direction: column;
  height: 100%;
  min-height: 0;
}

.context-header {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  padding: 0.75rem 1rem;
  font-weight: 600;
  color: var(--fuse-text-muted);
  border-bottom: 1px solid var(--fuse-panel-border);
  flex-shrink: 0;
}

.header-title {
  flex: 1;
}

.empty-state {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  padding: 2rem 1rem;
  text-align: center;
  color: var(--fuse-text-muted);
  gap: 0.5rem;
  flex: 1;
}

.empty-state p {
  margin: 0;
  font-size: 0.875rem;
}

.item-list {
  flex: 1;
  overflow-y: auto;
  min-height: 0;
}

.context-item {
  transition: background-color 0.15s ease;
}

.context-item:hover {
  background-color: var(--fuse-hover-bg);
}
</style>
