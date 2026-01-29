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
            <TagSelect v-model="form.tagIds" />
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
import type { Position } from '../../api/client'
import TagSelect from '../tags/TagSelect.vue'

interface PositionFormModel {
  name: string
  description: string
  tagIds: string[]
}

interface Props {
  position?: Position | null
  loading?: boolean
}

interface Emits {
  (e: 'save', payload: PositionFormModel): void
  (e: 'update:modelValue', value: boolean): void
}

const model = defineModel<boolean>({ required: true })

const props = withDefaults(defineProps<Props>(), {
  position: null,
  loading: false
})

const emit = defineEmits<Emits>()

const formRef = ref()

const form = reactive<PositionFormModel>({
  name: '',
  description: '',
  tagIds: []
})

const isEdit = computed(() => !!props.position?.id)
const title = computed(() => (isEdit.value ? 'Edit Position' : 'Create Position'))
const submitLabel = computed(() => (isEdit.value ? 'Save' : 'Create'))
const loading = computed(() => props.loading)

function applyInitialValue(value?: Position | null) {
  if (!value) {
    form.name = ''
    form.description = ''
    form.tagIds = []
    return
  }
  form.name = value.name ?? ''
  form.description = value.description ?? ''
  form.tagIds = [...(value.tagIds ?? [])]
}

watch(
  () => props.position,
  (value) => {
    if (model.value) {
      applyInitialValue(value)
    }
  },
  { immediate: true }
)

watch(model, (value) => {
  if (value) {
    applyInitialValue(props.position)
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
