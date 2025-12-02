<template>
  <q-dialog v-model="dialogVisible" persistent>
    <q-card style="min-width: 500px">
      <q-card-section class="dialog-header">
        <div class="text-h6">Reveal Secret</div>
        <q-btn flat round dense icon="close" @click="handleClose" />
      </q-card-section>
      <q-separator />
      <q-card-section>
        <q-banner dense class="bg-red-1 text-negative q-mb-md">
          <template #avatar>
            <q-icon name="warning" color="red" />
          </template>
          <div class="text-weight-bold">Security Warning</div>
          <div class="text-body2">
            You are about to reveal a secret value. This operation:
            <ul class="q-pl-md q-my-sm">
              <li>Will be audited with your username and timestamp</li>
              <li>Should only be done when absolutely necessary</li>
              <li>Exposes sensitive credential information</li>
            </ul>
            Only proceed if you understand the security implications.
          </div>
        </q-banner>

        <q-input
          v-if="revealedSecret"
          :model-value="revealedSecret"
          label="Secret Value"
          :type="showSecret ? 'text' : 'password'"
          readonly
          dense
          outlined
        >
          <template #append>
            <q-icon
              :name="showSecret ? 'visibility_off' : 'visibility'"
              class="cursor-pointer"
              @click="$emit('toggle-visibility')"
            />
            <q-icon
              name="content_copy"
              class="cursor-pointer q-ml-sm"
              @click="$emit('copy')"
            />
          </template>
        </q-input>

        <div v-else class="text-body2 text-grey-7">
          Click "Reveal" to display the secret value.
        </div>
      </q-card-section>
      <q-separator />
      <q-card-actions align="right">
        <q-btn flat label="Close" @click="handleClose" />
        <q-btn
          v-if="!revealedSecret"
          color="negative"
          label="Reveal"
          :loading="loading"
          @click="$emit('reveal')"
        />
      </q-card-actions>
    </q-card>
  </q-dialog>
</template>

<script setup lang="ts">
import { computed } from 'vue'

interface Props {
  modelValue: boolean
  revealedSecret: string
  showSecret: boolean
  loading: boolean
}

const props = defineProps<Props>()
const emit = defineEmits<{
  (e: 'update:modelValue', value: boolean): void
  (e: 'close'): void
  (e: 'reveal'): void
  (e: 'toggle-visibility'): void
  (e: 'copy'): void
}>()

const dialogVisible = computed({
  get: () => props.modelValue,
  set: (value: boolean) => emit('update:modelValue', value)
})

function handleClose() {
  emit('close')
  dialogVisible.value = false
}
</script>
