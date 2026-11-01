<template>
  <div class="search-bar">
    <q-input :model-value="modelValue" dense outlined clearable :placeholder="placeholder" aria-label="Search documentation"
      :loading="isLoading" @update:model-value="updateQuery" @clear="handleClear" @keyup.enter="handleSubmit" class="search-input">
      <template #prepend><q-icon name="search" /></template>
      <template #append>
        <q-btn v-if="showSearchButton" aria-label="Search" flat dense icon="arrow_forward" @click="handleSubmit" />
      </template>
    </q-input>
  </div>
</template>

<script setup lang="ts">
const props = withDefaults(defineProps<{
  modelValue?: string
  placeholder?: string
  isLoading?: boolean
  showSearchButton?: boolean
}>(), { modelValue: '', placeholder: 'Search documentation…', isLoading: false, showSearchButton: false })
const emit = defineEmits<{
  (event: 'update:modelValue', value: string): void
  (event: 'search', value: string): void
  (event: 'clear'): void
}>()
function updateQuery(value: string | number | null) { emit('update:modelValue', value == null ? '' : String(value)) }
function handleSubmit() { emit('search', props.modelValue.trim()) }
function handleClear() { emit('update:modelValue', ''); emit('clear') }
</script>

<style scoped>
.search-bar, .search-input { width: 100%; }
</style>
