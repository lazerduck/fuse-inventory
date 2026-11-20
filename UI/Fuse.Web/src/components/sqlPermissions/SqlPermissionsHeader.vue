<template>
  <div class="page-header">
    <div>
      <div class="row items-center q-gutter-sm">
        <q-btn aria-label="Go back"
          flat
          round
          dense
          icon="arrow_back"
          @click="$emit('back')"
        />
        <h1>{{ integrationName ?? 'SQL Permissions Overview' }}</h1>
      </div>
      <p class="subtitle">
        Inspect SQL access and compare whole-database grants with Fuse.
      </p>
    </div>
    <div class="row items-center q-gutter-x-md">
      <slot />
      <q-btn
        flat
        no-caps
        dense
        icon="refresh"
        label="Refresh"
        :loading="isFetching"
        @click="$emit('refresh')"
      />
    </div>
  </div>
</template>

<script setup lang="ts">
interface Props {
  integrationName?: string | null
  isFetching?: boolean
}

defineProps<Props>()

defineEmits<{ (e: 'back'): void; (e: 'refresh'): void }>()
</script>

<style scoped>
@import '../../styles/pages.css';
</style>
