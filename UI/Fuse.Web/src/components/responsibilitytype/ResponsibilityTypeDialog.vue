<template>
  <q-dialog v-model="model" persistent>
    <q-card class="form-dialog">
      <q-card-section class="dialog-header">
        <div class="text-h6">{{ title }}</div>
        <q-btn flat round dense icon="close" @click="closeDialog" />
      </q-card-section>
      <q-separator />
      <q-form ref="formRef" @submit.prevent="handleSubmit">
        <q-card-section>
          <div class="form-grid">
            <q-input
              v-model="form.name"
              label="Name*"
              dense
              outlined
              :rules="[value => !!value || 'Name is required']"
            />
            <q-input
              v-model="form.description"
              label="Description"
              dense
              outlined
              type="textarea"
              autogrow
              class="full-span"
            />
          </div>
        </q-card-section>
        <q-separator />
        <q-card-actions align="right">
          <q-btn flat label="Cancel" @click="closeDialog" />
          <q-btn color="primary" type="submit" :label="submitLabel" :loading="loading" />
        </q-card-actions>
      </q-form>
    </q-card>
  </q-dialog>
</template>

<script setup lang="ts">
import { computed, reactive, ref, watch } from 'vue'
import type { ResponsibilityType } from '../../api/client'

interface ResponsibilityTypeFormModel {
  name: string
  description: string
}

interface Props {
  responsibilityType?: ResponsibilityType | null
  loading?: boolean
}

interface Emits {
  (e: 'save', payload: ResponsibilityTypeFormModel): void
  (e: 'update:modelValue', value: boolean): void
}

const model = defineModel<boolean>({ required: true })

const props = withDefaults(defineProps<Props>(), {
  responsibilityType: null,
  loading: false
})

const emit = defineEmits<Emits>()

const formRef = ref()

const form = reactive<ResponsibilityTypeFormModel>({
  name: '',
  description: ''
})

const isEdit = computed(() => !!props.responsibilityType?.id)
const title = computed(() => (isEdit.value ? 'Edit Responsibility Type' : 'Create Responsibility Type'))
const submitLabel = computed(() => (isEdit.value ? 'Save' : 'Create'))
const loading = computed(() => props.loading)

function applyInitialValue(value?: ResponsibilityType | null) {
  if (!value) {
    form.name = ''
    form.description = ''
    return
  }
  form.name = value.name ?? ''
  form.description = value.description ?? ''
}

watch(
  () => props.responsibilityType,
  (value) => {
    if (model.value) {
      applyInitialValue(value)
    }
  },
  { immediate: true }
)

watch(model, (value) => {
  if (value) {
    applyInitialValue(props.responsibilityType)
  }
})

function closeDialog() {
  emit('update:modelValue', false)
}

async function handleSubmit() {
  const valid = await formRef.value?.validate?.()
  if (valid === false) return
  emit('save', { ...form })
}
</script>

<style scoped>
@import '../../styles/pages.css';
</style>
