<template>
  <q-card class="form-dialog">
    <q-card-section class="dialog-header">
      <div class="text-h6">{{ title }}</div>
      <q-btn aria-label="Close dialog" flat round dense icon="close" @click="cancelEdit" />
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
        <q-btn flat label="Cancel" @click="cancelEdit" />
        <q-btn color="primary" type="submit" :label="submitLabel" :loading="loading" :disable="disabled" />
      </q-card-actions>
    </q-form>
  </q-card>
</template>

<script setup lang="ts">
import { useUnsavedChanges } from '../../composables/useUnsavedChanges'
import { computed, onMounted, reactive, watch } from 'vue'
import type { ApplicationPipeline } from 'api/client'

type Mode = 'create' | 'edit'

interface ApplicationPipelineFormModel {
  name: string
  pipelineUri: string
}

interface Props {
  initialValue?: Partial<ApplicationPipeline> | null
  mode?: Mode
  loading?: boolean
  disabled?: boolean
}

interface Emits {
  (e: 'submit', value: ApplicationPipelineFormModel): void
  (e: 'cancel'): void
}

const props = withDefaults(defineProps<Props>(), {
  initialValue: null,
  mode: 'create',
  loading: false,
  disabled: false
})
const emit = defineEmits<Emits>()

const form = reactive<ApplicationPipelineFormModel>({
  name: '',
  pipelineUri: ''
})

const isCreate = computed(() => props.mode === 'create')
const title = computed(() => (isCreate.value ? 'Add Pipeline' : 'Edit Pipeline'))
const submitLabel = computed(() => (isCreate.value ? 'Create' : 'Save and close'))
const loading = computed(() => props.loading)

const draft = useUnsavedChanges(form)
function cancelEdit() {
  if (props.loading) return
  void draft.discard(() => emit('cancel'))
}

function applyInitial(value?: Partial<ApplicationPipeline> | null) {
  if (!value) {
    form.name = ''
    form.pipelineUri = ''
    return
  }
  form.name = value.name ?? ''
  form.pipelineUri = value.pipelineUri ?? ''
}

onMounted(() => { applyInitial(props.initialValue); draft.markClean() })
watch(() => props.initialValue, (v) => { if (!draft.isDirty.value) { applyInitial(v); draft.markClean() } })

function handleSubmit() {
  if (props.loading) return
  emit('submit', { ...form })
}
</script>

<style scoped>
@import '../../styles/pages.css';

.form-dialog { min-width: 500px; max-width: 700px; }
</style>
