<template>
  <ReadOnlyShell
    :title="`Instance: ${id}`"
    :higher="[]"
    :lower="[]"
  >
    <div class="placeholder-content">
      <q-icon name="layers" size="48px" color="primary" />
      <h2>Read-only view coming soon</h2>
      <p>Entity ID: <code>{{ id }}</code></p>
    </div>
  </ReadOnlyShell>
</template>

<script setup lang="ts">
import { useRoute } from 'vue-router'
import { computed } from 'vue'
import ReadOnlyShell from '../../components/readonly/ReadOnlyShell.vue'

const route = useRoute()
const id = computed(() => route.params.id as string)
</script>

<style scoped>
.placeholder-content {
  text-align: center;
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 0.75rem;
  padding: 2rem;
}

.placeholder-content h2 {
  margin: 0.5rem 0;
  font-size: 1.5rem;
  font-weight: 600;
}

.placeholder-content p {
  margin: 0;
  color: var(--fuse-text-muted);
}

.placeholder-content code {
  background: var(--fuse-panel-bg);
  padding: 0.125rem 0.5rem;
  border-radius: 4px;
  font-family: monospace;
}
</style>
