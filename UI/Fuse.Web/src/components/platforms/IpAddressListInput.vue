<template>
  <q-select
    :model-value="modelValue"
    :input-value="draft"
    :label="label"
    multiple
    use-input
    use-chips
    hide-dropdown-icon
    :error="!!errorMessage"
    :error-message="errorMessage"
    @update:model-value="emit('update:modelValue', $event)"
    @input-value="onInputValue"
    @new-value="onNewValue"
    @blur="commitDraft"
  />
</template>

<script setup lang="ts">
import { ref } from 'vue'
import { z } from 'zod'

interface Props {
  modelValue: string[]
  label?: string
}

const props = withDefaults(defineProps<Props>(), { label: 'IP Addresses' })
const emit = defineEmits<{ (event: 'update:modelValue', value: string[]): void }>()

const ipAddressSchema = z.union([z.ipv4(), z.ipv6()])
const draft = ref('')
const errorMessage = ref('')

function onInputValue(value: string) {
  draft.value = value
  errorMessage.value = ''
}

function validate(value: string): string | null {
  const address = value.trim()
  if (!address) return null
  if (!ipAddressSchema.safeParse(address).success) {
    errorMessage.value = 'Enter a valid IPv4 or IPv6 address'
    return null
  }
  errorMessage.value = ''
  return address
}

function onNewValue(value: string, done: (item?: string, mode?: 'add' | 'add-unique' | 'toggle') => void) {
  const address = validate(value)
  if (!address) return
  draft.value = ''
  done(address, 'add-unique')
}

function commitDraft() {
  if (!draft.value.trim()) return
  const address = validate(draft.value)
  if (!address) return
  if (!props.modelValue.includes(address)) emit('update:modelValue', [...props.modelValue, address])
  draft.value = ''
}
</script>
