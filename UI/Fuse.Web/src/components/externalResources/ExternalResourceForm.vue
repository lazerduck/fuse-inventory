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
          <q-input v-model="form.name" label="Name*" dense outlined :rules="[v => !!v || 'Name is required']" />
          <q-input v-model="form.resourceUri" label="Resource URI" dense outlined />
          <TagSelect v-model="form.tagIds" />
          <q-input
            v-model="form.description"
            type="textarea"
            label="Description"
            dense
            outlined
            autogrow
            class="full-span"
          />
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
import { computed, reactive, watch, onMounted } from 'vue'
import type { ExternalResource } from '../../api/client'
import TagSelect from '../tags/TagSelect.vue'

type Mode = 'create' | 'edit'

interface ExternalResourceFormModel {
  name: string
  resourceUri: string
  description: string
  tagIds: string[]
}

interface Props {
  mode?: Mode
  initialValue?: Partial<ExternalResource> | null
  loading?: boolean
}

interface Emits {
  (e: 'submit', payload: ExternalResourceFormModel): void
  (e: 'cancel'): void
}

const props = withDefaults(defineProps<Props>(), {
  mode: 'create',
  initialValue: null,
  loading: false
})
const emit = defineEmits<Emits>()

const form = reactive<ExternalResourceFormModel>({
  name: '',
  resourceUri: '',
  description: '',
  tagIds: []
})

const isCreate = computed(() => props.mode === 'create')
const title = computed(() => (isCreate.value ? 'Create Resource' : 'Edit Resource'))
const submitLabel = computed(() => (isCreate.value ? 'Create' : 'Save'))
const loading = computed(() => props.loading)

function applyInitialValue(value?: Partial<ExternalResource> | null) {
  if (!value) {
    form.name = ''
    form.resourceUri = ''
    form.description = ''
    form.tagIds = []
    return
  }
  form.name = value.name ?? ''
  form.resourceUri = value.resourceUri ?? ''
  form.description = value.description ?? ''
  form.tagIds = [...(value.tagIds ?? [])]
}

onMounted(() => applyInitialValue(props.initialValue))
watch(() => props.initialValue, (v) => applyInitialValue(v))

function handleSubmit() {
  emit('submit', { ...form })
}
</script>

<style scoped>
@import '../../styles/pages.css';

.form-dialog { min-width: 480px; }
</style>
