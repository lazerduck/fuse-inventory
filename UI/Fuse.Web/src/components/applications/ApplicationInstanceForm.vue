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
            v-model="form.serverId"
            label="Server"
            dense
            outlined
            emit-value
            map-options
            clearable
            :options="serverOptions"
            :disable="serverOptions.length === 0"
          />
          <q-input v-model="form.version" label="Version" dense outlined />
          <q-input v-model="form.baseUri" label="Base URI" dense outlined />
          <q-input v-model="form.healthUri" label="Health URI" dense outlined />
          <q-input v-model="form.openApiUri" label="OpenAPI URI" dense outlined />
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
import { useServers } from '../../composables/useServers'
import { useTags } from '../../composables/useTags'

type Mode = 'create' | 'edit'

interface ApplicationInstanceFormModel {
  environmentId: string | null
  serverId: string | null
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
const serversStore = useServers()
const tagsStore = useTags()

const environmentOptions = environmentsStore.options
const serverOptions = serversStore.options
const tagOptions = tagsStore.options

const form = reactive<ApplicationInstanceFormModel>({
  environmentId: null,
  serverId: null,
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
    form.serverId = null
    form.baseUri = ''
    form.healthUri = ''
    form.openApiUri = ''
    form.version = ''
    form.tagIds = []
    return
  }
  form.environmentId = value.environmentId ?? null
  form.serverId = value.serverId ?? null
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
