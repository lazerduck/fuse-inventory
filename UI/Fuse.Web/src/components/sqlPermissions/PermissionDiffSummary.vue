<template>
  <div class="permission-summary">
    <template v-if="missingCount > 0">
      <q-icon name="remove_circle" color="negative" size="xs" />
      <span class="text-negative text-caption q-ml-xs">
        {{ missingCount }} missing
      </span>
    </template>
    <template v-if="extraCount > 0">
      <q-icon name="add_circle" color="orange" size="xs" class="q-ml-sm" />
      <span class="text-orange text-caption q-ml-xs">
        {{ extraCount }} extra
      </span>
    </template>
    <template v-if="showAllInSync && missingCount === 0 && extraCount === 0">
      <q-icon name="check_circle" color="positive" size="xs" />
      <span class="text-positive text-caption q-ml-xs">All in sync</span>
    </template>
    <slot v-if="missingCount === 0 && extraCount === 0 && !showAllInSync" />
  </div>
</template>

<script setup lang="ts">
interface Props {
  missingCount: number
  extraCount: number
  showAllInSync?: boolean
}

defineProps<Props>()
</script>

<style scoped>
.permission-summary {
  display: flex;
  align-items: center;
  flex-wrap: wrap;
  gap: 0.25rem;
}
</style>
