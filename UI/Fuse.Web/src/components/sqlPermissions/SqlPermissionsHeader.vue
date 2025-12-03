<template>
  <div class="page-header">
    <div>
      <div class="row items-center q-gutter-sm">
        <q-btn
          flat
          round
          dense
          icon="arrow_back"
          @click="$emit('back')"
        />
        <h1>{{ integrationName ?? 'SQL Permissions Overview' }}</h1>
      </div>
      <p class="subtitle">
        Permission drift dashboard for accounts associated with this SQL integration.
      </p>
    </div>
    <q-btn
      flat
      dense
      icon="refresh"
      label="Refresh"
      :loading="isFetching"
      @click="$emit('refresh')"
    />
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
