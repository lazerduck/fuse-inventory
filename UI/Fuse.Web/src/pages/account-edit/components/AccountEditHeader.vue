<template>
  <div class="page-header">
    <div>
      <h1 class="page-title">{{ title }}</h1>
      <p class="subtitle">{{ subtitle }}</p>
    </div>
    <div class="flex q-gutter-sm">
      <SaveStatus :dirty="dirty" :saving="isSaving" :saved="saved" />
      <q-btn flat label="Cancel" @click="$emit('cancel')" />
      <q-btn
        v-if="showClone"
        flat
        color="secondary"
        icon="content_copy"
        label="Clone across environments"
        @click="$emit('clone')"
      />
      <q-btn v-if="primaryLabel === 'Save'" outline color="primary" label="Save and close" :loading="isSaving" :disable="!canModify" @click="$emit('save-close')" />
      <q-btn
        color="primary"
        :label="primaryLabel"
        aria-keyshortcuts="Control+s Meta+s"
        title="Save (Ctrl/Cmd+S)"
        :loading="isSaving"
        :disable="!canModify"
        @click="$emit('save')"
      />
    </div>
  </div>
</template>

<script setup lang="ts">
import SaveStatus from '../../../components/shared/SaveStatus.vue'
interface Props {
  title: string
  subtitle: string
  primaryLabel: string
  isSaving: boolean
  canModify: boolean
  dirty: boolean
  saved: boolean
  showClone?: boolean
}

defineProps<Props>()

defineEmits<{
  (e: 'cancel'): void
  (e: 'save'): void
  (e: 'save-close'): void
  (e: 'clone'): void
}>()
</script>

<style scoped>
@import '../../../styles/pages.css';
</style>
