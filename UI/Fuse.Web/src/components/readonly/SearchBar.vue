<template>
  <div class="search-bar">
    <q-input
      v-model="localQuery"
      dense
      outlined
      clearable
      :placeholder="placeholder"
      :loading="isLoading"
      @clear="handleClear"
      @keyup.enter="handleSubmit"
      class="search-input"
    >
      <template #prepend>
        <q-icon name="search" />
      </template>
      <template #append>
        <q-btn
          v-if="showSearchButton"
          flat
          dense
          icon="arrow_forward"
          :disable="!localQuery.trim()"
          @click="handleSubmit"
        />
      </template>
    </q-input>
  </div>
</template>

<script setup lang="ts">
import { ref, watch } from 'vue'

const props = withDefaults(
  defineProps<{
    modelValue?: string
    placeholder?: string
    isLoading?: boolean
    showSearchButton?: boolean
  }>(),
  {
    modelValue: '',
    placeholder: 'Search applications, instances, accounts...',
    isLoading: false,
    showSearchButton: false
  }
)

const emit = defineEmits<{
  (e: 'update:modelValue', value: string): void
  (e: 'search', value: string): void
  (e: 'clear'): void
}>()

const localQuery = ref(props.modelValue)

watch(
  () => props.modelValue,
  (newValue) => {
    localQuery.value = newValue
  }
)

watch(localQuery, (newValue) => {
  emit('update:modelValue', newValue)
})

function handleSubmit() {
  emit('search', localQuery.value.trim())
}

function handleClear() {
  localQuery.value = ''
  emit('clear')
}
</script>

<style scoped>
.search-bar {
  width: 100%;
  max-width: 600px;
}

.search-input {
  width: 100%;
}
</style>
