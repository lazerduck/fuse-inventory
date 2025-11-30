<template>
  <div class="readonly-placeholder">
    <div class="placeholder-content">
      <q-icon name="vpn_key" size="48px" color="primary" />
      <h2>Read-only view coming soon</h2>
      <p>Entity type: <strong>Account</strong></p>
      <p>Entity ID: <code>{{ id }}</code></p>
      <q-btn flat color="primary" icon="arrow_back" label="Back to Search" @click="goBack" />
    </div>
  </div>
</template>

<script setup lang="ts">
import { useRoute, useRouter } from 'vue-router'
import { computed } from 'vue'

const route = useRoute()
const router = useRouter()

const id = computed(() => route.params.id as string)

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
