<template>
  <q-card class="stat-card" :to="to" clickable>
    <div class="stat-icon" :class="iconClass">
      <q-icon :name="icon" size="24px" />
    </div>
    <div class="stat-content">
      <p class="stat-value">{{ value }}</p>
      <p class="stat-label">{{ label }}</p>
    </div>
    <q-icon name="chevron_right" size="20px" class="stat-arrow" />
  </q-card>
</template>

<script setup lang="ts">
defineProps<{
  value: number
  label: string
  icon: string
  iconClass: string
  to: string
}>()
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

.stat-value {
  font-size: 1.5rem;
  font-weight: 600;
  margin: 0 0 0.125rem;
  line-height: 1;
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
