<template>
  <q-card class="stat-card" clickable role="link" tabindex="0" @click="navigate" @keyup.enter="navigate" @keyup.space.prevent="navigate">
    <div class="stat-icon" :class="iconClass">
      <q-icon :name="icon" size="24px" />
    </div>
    <div class="stat-content">
      <div class="stat-value-container">
        <p class="stat-value">{{ value }}</p>
        <div v-if="healthBreakdown" class="health-breakdown">
          <span v-if="healthBreakdown.healthy > 0" class="health-stat healthy">{{ healthBreakdown.healthy }}</span>
          <span v-else class="health-stat">-</span>
          <span class="health-separator">|</span>
          <span v-if="healthBreakdown.unhealthy > 0" class="health-stat unhealthy">{{ healthBreakdown.unhealthy }}</span>
          <span v-else class="health-stat">-</span>
        </div>
      </div>
      <p class="stat-label">{{ label }}</p>
    </div>
    <q-icon name="chevron_right" size="20px" class="stat-arrow" />
  </q-card>
</template>

<script setup lang="ts">
import { useRouter } from 'vue-router'

const props = defineProps<{
  value: number
  label: string
  icon: string
  iconClass: string
  to?: string
  healthBreakdown?: {
    healthy: number
    unhealthy: number
  }
}>()

const router = useRouter()

function navigate() {
  if (props.to) {
    router.push(props.to)
  }
}
</script>

<style scoped>
.stat-card {
  display: flex;
  align-items: center;
  gap: 0.875rem;
  padding: 1rem 1.125rem;
  border-radius: 12px;
  box-shadow: var(--fuse-shadow-1);
  transition: box-shadow 0.2s ease, transform 0.2s ease;
  cursor: pointer;
}

.stat-card:hover {
  box-shadow: var(--fuse-shadow-2);
  transform: translateY(-2px);
}

.stat-icon {
  width: 40px;
  height: 40px;
  border-radius: 10px;
  display: grid;
  place-items: center;
  flex-shrink: 0;
}

.stat-content {
  flex: 1;
  min-width: 0;
}

.stat-value-container {
  display: flex;
  align-items: baseline;
  gap: 0.5rem;
  margin: 0 0 0.125rem;
  line-height: 1;
}

.stat-value {
  font-size: 1.5rem;
  font-weight: 600;
  margin: 0;
  line-height: 1;
}

.health-breakdown {
  display: flex;
  align-items: baseline;
  gap: 0.25rem;
  font-size: 0.875rem;
  font-weight: 500;
}

.health-stat {
  font-weight: 600;
}

.health-stat.healthy {
  color: #21ba45;
}

.health-stat.unhealthy {
  color: #c10015;
}

.health-separator {
  color: var(--fuse-text-subtle);
}

.stat-label {
  margin: 0;
  font-size: 0.875rem;
  color: var(--fuse-text-muted);
  line-height: 1;
}

.stat-arrow {
  flex-shrink: 0;
  color: var(--fuse-text-subtle);
}
</style>
