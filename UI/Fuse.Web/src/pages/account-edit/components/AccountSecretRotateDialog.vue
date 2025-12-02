<template>
  <q-dialog v-model="dialogVisible" persistent>
    <q-card style="min-width: 400px">
      <q-card-section class="dialog-header">
        <div class="text-h6">Rotate Secret</div>
        <q-btn flat round dense icon="close" @click="handleCancel" />
      </q-card-section>
      <q-separator />
      <q-form @submit.prevent="$emit('submit')">
        <q-card-section>
          <q-banner dense class="bg-blue-1 text-blue-9 q-mb-md">
            <template #avatar>
              <q-icon name="info" color="blue" />
            </template>
            Generate a new value for this secret. The operation will be audited.
          </q-banner>
          <q-input
            v-model="secretValue"
            label="New Secret Value"
            type="password"
            dense
            outlined
            required
          />
        </q-card-section>
        <q-separator />
        <q-card-actions align="right">
          <q-btn flat label="Cancel" @click="handleCancel" />
          <q-btn
            color="primary"
            type="submit"
            label="Rotate"
            :loading="loading"
            :disable="!secretValue"
          />
        </q-card-actions>
      </q-form>
    </q-card>
  </q-dialog>
</template>

<script setup lang="ts">
import { computed } from 'vue'

interface Props {
  modelValue: boolean
  newSecretValue: string
  loading: boolean
}

const props = defineProps<Props>()
const emit = defineEmits<{
  (e: 'update:modelValue', value: boolean): void
  (e: 'update:newSecretValue', value: string): void
  (e: 'cancel'): void
  (e: 'submit'): void
}>()

const dialogVisible = computed({
  get: () => props.modelValue,
  set: (value: boolean) => emit('update:modelValue', value)
})

const secretValue = computed({
  get: () => props.newSecretValue,
  set: (value: string) => emit('update:newSecretValue', value)
})

function handleCancel() {
  emit('cancel')
  dialogVisible.value = false
}
</script>
