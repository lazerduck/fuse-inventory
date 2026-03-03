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
            :type="showValue ? 'text' : 'password'"
            dense
            outlined
            required
          >
            <template #append>
              <q-btn flat dense round :icon="showValue ? 'visibility_off' : 'visibility'" @click="showValue = !showValue" />
            </template>
          </q-input>
          <div class="q-mt-sm">
            <q-btn
              flat
              dense
              icon="autorenew"
              label="Generate Password"
              color="secondary"
              :loading="isGenerating"
              @click="handleGenerate"
            />
            <div v-if="secretValue" class="text-caption text-grey-6 q-mt-xs">
              Note the password above – you will need to apply it to the system using this secret.
            </div>
          </div>
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
import { computed, ref } from 'vue'
import { Notify } from 'quasar'
import { useFuseClient } from '../../../composables/useFuseClient'
import { getErrorMessage } from '../../../utils/error'

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

const client = useFuseClient()
const isGenerating = ref(false)
const showValue = ref(false)

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

async function handleGenerate() {
  isGenerating.value = true
  showValue.value = true
  try {
    const response = await client.passwordGeneratorGenerate()
    emit('update:newSecretValue', response.password ?? '')
  } catch (err) {
    Notify.create({ type: 'negative', message: getErrorMessage(err, 'Unable to generate password') })
  } finally {
    isGenerating.value = false
  }
}
</script>
