<template>
  <div class="readonly-placeholder">
    <div class="placeholder-content">
      <q-icon :name="icon" size="48px" color="primary" />
      <h2>Read-only view coming soon</h2>
      <p>Entity type: <strong>{{ entityType }}</strong></p>
      <p>Entity ID: <code>{{ entityId }}</code></p>
      <q-btn flat color="primary" icon="arrow_back" label="Back to Search" @click="goBack" />
    </div>
  </div>
</template>

<script setup lang="ts">
import { useRouter } from 'vue-router'

defineProps<{
  icon: string
  entityType: string
  entityId: string
}>()

const router = useRouter()

function goBack() {
  router.push('/view')
}
</script>

<style scoped>
.readonly-placeholder {
  display: flex;
  align-items: center;
  justify-content: center;
  min-height: calc(100vh - 100px);
  padding: 2rem;
}

.placeholder-content {
  text-align: center;
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 0.75rem;
  max-width: 400px;
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
