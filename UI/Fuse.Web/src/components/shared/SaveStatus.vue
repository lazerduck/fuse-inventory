<template>
  <span
    class="save-status text-caption self-center"
    :class="dirty ? 'text-warning' : 'text-grey-7'"
    role="status"
    aria-live="polite"
  >{{ saving ? 'Saving…' : dirty ? 'Unsaved changes' : saved ? 'Saved' : '' }}</span>
</template>

<script setup lang="ts">
defineProps<{ dirty: boolean; saving?: boolean; saved?: boolean }>()
</script>

<style scoped>
.save-status {
  min-width: 7rem;
  text-align: right;
}
</style>
