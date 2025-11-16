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
          <q-select
            v-model="form.tagIds"
            label="Tags"
            dense
            outlined
            use-chips
            multiple
            emit-value
            map-options
            :options="tagOptions"
          />
          <q-input
            v-model="form.description"
            type="textarea"
            label="Description"
            dense
            outlined
            autogrow
            class="full-span"
          />
          <q-checkbox
            v-model="form.autoCreateInstances"
            label="Auto-create instances for new applications"
            class="full-span"
          />
          <q-input
            v-model="form.baseUriTemplate"
            label="Base URI Template"
            dense
            outlined
            hint="e.g., https://{appname}.{env}.company.com"
            class="full-span"
          />
          <q-input
            v-model="form.healthUriTemplate"
            label="Health URI Template"
            dense
            outlined
            hint="e.g., https://{appname}.{env}.company.com/health"
            class="full-span"
          />
          <q-input
            v-model="form.openApiUriTemplate"
            label="OpenAPI URI Template"
            dense
            outlined
            hint="e.g., https://{appname}.{env}.company.com/swagger"
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
import { useTags } from '../../composables/useTags'
import type { EnvironmentInfo } from '../../api/client'

type Mode = 'create' | 'edit'

interface EnvironmentFormModel {
  name: string
  description: string
  tagIds: string[]
  autoCreateInstances: boolean
  baseUriTemplate: string
  healthUriTemplate: string
  openApiUriTemplate: string
}

interface Props {
  mode?: Mode
  initialValue?: Partial<EnvironmentInfo> | null
  loading?: boolean
}

interface Emits {
  (e: 'submit', payload: EnvironmentFormModel): void
  (e: 'cancel'): void
}

const props = withDefaults(defineProps<Props>(), {
  mode: 'create',
  initialValue: null,
  loading: false
})
const emit = defineEmits<Emits>()

const tagsStore = useTags()
const tagOptions = tagsStore.options

const form = reactive<EnvironmentFormModel>({
  name: '',
  description: '',
  tagIds: [],
  autoCreateInstances: false,
  baseUriTemplate: '',
  healthUriTemplate: '',
  openApiUriTemplate: ''
})

const isCreate = computed(() => props.mode === 'create')
const title = computed(() => (isCreate.value ? 'Create Environment' : 'Edit Environment'))
const submitLabel = computed(() => (isCreate.value ? 'Create' : 'Save'))
const loading = computed(() => props.loading)

function applyInitialValue(value?: Partial<EnvironmentInfo> | null) {
  if (!value) {
    form.name = ''
    form.description = ''
    form.tagIds = []
    form.autoCreateInstances = false
    form.baseUriTemplate = ''
    form.healthUriTemplate = ''
    form.openApiUriTemplate = ''
    return
  }
  form.name = value.name ?? ''
  form.description = value.description ?? ''
  form.tagIds = [...(value.tagIds ?? [])]
  form.autoCreateInstances = value.autoCreateInstances ?? false
  form.baseUriTemplate = value.baseUriTemplate ?? ''
  form.healthUriTemplate = value.healthUriTemplate ?? ''
  form.openApiUriTemplate = value.openApiUriTemplate ?? ''
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
