<template>
  <div class="placeholder-content">
    <q-icon :name="icon" size="48px" color="primary" />
    <h2>Read-only view coming soon</h2>
    <p>Entity ID: <code>{{ entityId }}</code></p>
  </div>
</template>

<script setup lang="ts">
defineProps<{
  icon: string
  entityId: string
}>()
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
