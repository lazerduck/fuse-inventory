<template>
  <div class="page-header">
    <div>
      <h1>{{ title }}</h1>
      <p class="subtitle">{{ subtitle }}</p>
    </div>
    <div class="flex q-gutter-sm">
      <q-btn flat label="Cancel" @click="$emit('cancel')" />
      <q-btn
        v-if="showClone"
        flat
        color="secondary"
        icon="content_copy"
        label="Clone across environments"
        @click="$emit('clone')"
      />
      <q-btn
        color="primary"
        :label="primaryLabel"
        :loading="isSaving"
        :disable="!canModify"
        @click="$emit('save')"
      />
    </div>
  </div>
</template>

<script setup lang="ts">
interface Props {
  title: string
  subtitle: string
  primaryLabel: string
  isSaving: boolean
  canModify: boolean
  showClone?: boolean
}

defineProps<Props>()

defineEmits<{
  (e: 'cancel'): void
  (e: 'save'): void
  (e: 'clone'): void
}>()
</script>
