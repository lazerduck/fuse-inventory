<template>
  <q-card class="form-dialog">
    <q-card-section class="dialog-header">
      <div class="text-h6">{{ title }}</div>
      <q-btn flat round dense icon="close" @click="emit('cancel')" />
    </q-card-section>
    <q-separator />
    <q-form @submit.prevent="handleSubmit">
      <q-card-section>
        <div class="form-grid">
          <q-input v-model="form.name" label="Name" dense outlined />
          <q-input v-model="form.pipelineUri" label="Pipeline URI" dense outlined />
        </div>
      </q-card-section>
      <q-separator />
      <q-card-actions align="right">
        <q-btn flat label="Cancel" @click="emit('cancel')" />
        <q-btn color="primary" type="submit" :label="submitLabel" :loading="loading" />
      </q-card-actions>
    </q-form>
  </q-card>
</template>

<script setup lang="ts">
import { computed, onMounted, reactive, watch } from 'vue'
import type { ApplicationPipeline } from '../../api/client'

type Mode = 'create' | 'edit'

interface ApplicationPipelineFormModel {
  name: string
  pipelineUri: string
}

interface Props {
  initialValue?: Partial<ApplicationPipeline> | null
  mode?: Mode
  loading?: boolean
}

interface Emits {
  (e: 'submit', value: ApplicationPipelineFormModel): void
  (e: 'cancel'): void
}

const props = withDefaults(defineProps<Props>(), {
  initialValue: null,
  mode: 'create',
  loading: false
})
const emit = defineEmits<Emits>()

const form = reactive<ApplicationPipelineFormModel>({
  name: '',
  pipelineUri: ''
})

const isCreate = computed(() => props.mode === 'create')
const title = computed(() => (isCreate.value ? 'Add Pipeline' : 'Edit Pipeline'))
const submitLabel = computed(() => (isCreate.value ? 'Create' : 'Save'))
const loading = computed(() => props.loading)

function applyInitial(value?: Partial<ApplicationPipeline> | null) {
  if (!value) {
    form.name = ''
    form.pipelineUri = ''
    return
  }
  form.name = value.name ?? ''
  form.pipelineUri = value.pipelineUri ?? ''
}

onMounted(() => applyInitial(props.initialValue))
watch(() => props.initialValue, (v) => applyInitial(v))

function handleSubmit() {
  emit('submit', { ...form })
}
</script>

<style scoped>
@import '../../styles/pages.css';

.form-dialog { min-width: 500px; max-width: 700px; }
</style>
