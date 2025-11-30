<template>
  <div class="readonly-shell">
    <!-- Header -->
    <header class="shell-header">
      <div class="header-content">
        <q-btn flat dense icon="arrow_back" @click="goBack" class="back-btn">
          <q-tooltip>Back to Search</q-tooltip>
        </q-btn>
        <h1 class="shell-title">{{ title }}</h1>
      </div>
    </header>

    <!-- Three-column layout -->
    <div class="shell-columns">
      <!-- Left column: Higher context (upstream items) -->
      <aside class="column column-left">
        <ReadOnlyContextList
          :items="higher"
          title="Upstream"
          header-icon="arrow_upward"
          empty-message="No upstream items"
        />
      </aside>

      <!-- Center column: Entity detail (slot) -->
      <main class="column column-center">
        <div class="center-content">
          <slot />
        </div>
      </main>

      <!-- Right column: Lower context (downstream items) -->
      <aside class="column column-right">
        <ReadOnlyContextList
          :items="lower"
          title="Downstream"
          header-icon="arrow_downward"
          empty-message="No downstream items"
        />
      </aside>
    </div>
  </div>
</template>

<script setup lang="ts">
import { useRouter } from 'vue-router'
import ReadOnlyContextList from './ReadOnlyContextList.vue'
import type { HigherItem, LowerItem } from '../../types/readonly'

withDefaults(
  defineProps<{
    higher?: HigherItem[]
    lower?: LowerItem[]
    title: string
  }>(),
  {
    higher: () => [],
    lower: () => []
  }
)

const router = useRouter()

function goBack() {
  router.push('/view')
}
</script>

<style scoped>
.readonly-shell {
  display: flex;
  flex-direction: column;
  height: calc(100vh - 50px); /* Account for app header */
  min-height: 0;
}

.shell-header {
  flex-shrink: 0;
  background: var(--fuse-panel-bg);
  border-bottom: 1px solid var(--fuse-panel-border);
  padding: 0.75rem 1rem;
}

.header-content {
  display: flex;
  align-items: center;
  gap: 0.75rem;
  max-width: 1600px;
  margin: 0 auto;
}

.back-btn {
  flex-shrink: 0;
}

.shell-title {
  margin: 0;
  font-size: 1.25rem;
  font-weight: 600;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.shell-columns {
  display: flex;
  flex: 1;
  min-height: 0;
  overflow: hidden;
}

.column {
  display: flex;
  flex-direction: column;
  min-height: 0;
  overflow: hidden;
}

.column-left,
.column-right {
  width: 20%;
  min-width: 200px;
  max-width: 320px;
  background: var(--fuse-panel-bg);
  border-color: var(--fuse-panel-border);
}

.column-left {
  border-right: 1px solid var(--fuse-panel-border);
}

.column-right {
  border-left: 1px solid var(--fuse-panel-border);
}

.column-center {
  flex: 1;
  min-width: 0;
  overflow-y: auto;
}

.center-content {
  padding: 1.5rem;
  max-width: 900px;
  margin: 0 auto;
}

/* Responsive: Stack columns on narrow screens */
@media (max-width: 900px) {
  .shell-columns {
    flex-direction: column;
    overflow-y: auto;
  }

  .column-left,
  .column-right {
    width: 100%;
    max-width: none;
    min-width: 0;
    border: none;
    border-bottom: 1px solid var(--fuse-panel-border);
  }

  .column-left {
    order: 1;
    border-right: none;
  }

  .column-center {
    order: 2;
    overflow-y: visible;
  }

  .column-right {
    order: 3;
    border-left: none;
    border-bottom: none;
  }

  .center-content {
    padding: 1rem;
  }
}

/* Additional visual differentiation */
.column-left :deep(.context-header),
.column-right :deep(.context-header) {
  background: transparent;
}
</style>
