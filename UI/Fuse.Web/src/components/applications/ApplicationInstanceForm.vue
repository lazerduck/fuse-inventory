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
          <q-select
            v-model="form.environmentId"
            label="Environment*"
            dense
            outlined
            emit-value
            map-options
            :options="environmentOptions"
            :rules="[v => !!v || 'Environment is required']"
            :disable="environmentOptions.length === 0"
          />
          <q-select
            v-model="form.platformId"
            label="Platform"
            dense
            outlined
            emit-value
            map-options
            clearable
            :options="platformOptions"
            :disable="platformOptions.length === 0"
          />
          <q-input v-model="form.version" label="Version" dense outlined />
          <q-input v-model="form.baseUri" label="Base URI" dense outlined />
          <q-input v-model="form.healthUri" label="Health URI" dense outlined />
          <q-input v-model="form.openApiUri" label="OpenAPI URI" dense outlined />
          <TagSelect v-model="form.tagIds" />
        </div>

        <slot />
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
import type { ApplicationInstance } from '../../api/client'
import { useEnvironments } from '../../composables/useEnvironments'
import { usePlatforms } from '../../composables/usePlatforms'
import TagSelect from '../tags/TagSelect.vue'

type Mode = 'create' | 'edit'

interface ApplicationInstanceFormModel {
  environmentId: string | null
  platformId: string | null
  baseUri: string
  healthUri: string
  openApiUri: string
  version: string
  tagIds: string[]
}

interface Props {
  initialValue?: Partial<ApplicationInstance> | null
  mode?: Mode
  loading?: boolean
}

interface Emits {
  (e: 'submit', value: ApplicationInstanceFormModel): void
  (e: 'cancel'): void
}

const props = withDefaults(defineProps<Props>(), {
  initialValue: null,
  mode: 'create',
  loading: false
})
const emit = defineEmits<Emits>()

const environmentsStore = useEnvironments()
const platformsStore = usePlatforms()

const environmentOptions = environmentsStore.options
const platformOptions = platformsStore.options

const form = reactive<ApplicationInstanceFormModel>({
  environmentId: null,
  platformId: null,
  baseUri: '',
  healthUri: '',
  openApiUri: '',
  version: '',
  tagIds: []
})

const isCreate = computed(() => props.mode === 'create')
const title = computed(() => (isCreate.value ? 'Add Instance' : 'Edit Instance'))
const submitLabel = computed(() => (isCreate.value ? 'Create' : 'Save'))
const loading = computed(() => props.loading)

function applyInitial(value?: Partial<ApplicationInstance> | null) {
  if (!value) {
    form.environmentId = null
    form.platformId = null
    form.baseUri = ''
    form.healthUri = ''
    form.openApiUri = ''
    form.version = ''
    form.tagIds = []
    return
  }
  form.environmentId = value.environmentId ?? null
  form.platformId = value.platformId ?? null
  form.baseUri = value.baseUri ?? ''
  form.healthUri = value.healthUri ?? ''
  form.openApiUri = value.openApiUri ?? ''
  form.version = value.version ?? ''
  form.tagIds = [...(value.tagIds ?? [])]
}

onMounted(() => applyInitial(props.initialValue))
watch(() => props.initialValue, (v) => applyInitial(v))

function handleSubmit() {
  emit('submit', { ...form })
}
</script>

<style scoped>
@import '../../styles/pages.css';

.form-dialog { min-width: 700px; }
</style>
