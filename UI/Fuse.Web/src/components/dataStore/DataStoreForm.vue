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
          <q-input
            v-model="form.name"
            label="Name*"
            dense
            outlined
            required
            :rules="[val => !!val || 'Data store name is required']"
          />
          <q-input
            v-model="form.kind"
            label="Kind*"
            dense
            outlined
            required
            :rules="[val => !!val || 'Data store kind is required']"
          />
          <q-select
            v-model="form.environmentId"
            label="Environment*"
            dense
            outlined
            emit-value
            map-options
            :options="environmentOptions"
            required
            :rules="[val => !!val || 'Data store environment is required']"
          />
          <q-select
            v-model="form.platformId"
            label="Server"
            dense
            outlined
            emit-value
            map-options
            clearable
            :options="serverOptions"
          />
          <q-input v-model="form.connectionUri" label="Connection URI" dense outlined />
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
import { useEnvironments } from '../../composables/useEnvironments'
import { usePlatforms } from '../../composables/usePlatforms'
import { useTags } from '../../composables/useTags'
import type { DataStore } from '../../api/client'

type Mode = 'create' | 'edit'

interface DataStoreFormModel {
  name: string
  kind: string
  environmentId: string | null
  platformId: string | null
  connectionUri: string
  tagIds: string[]
}

interface Props {
  mode?: Mode
  initialValue?: Partial<DataStore> | null
  loading?: boolean
}

interface Emits {
  (e: 'submit', payload: DataStoreFormModel): void
  (e: 'cancel'): void
}

const props = withDefaults(defineProps<Props>(), {
  mode: 'create',
  initialValue: null,
  loading: false
})
const emit = defineEmits<Emits>()

const environmentsStore = useEnvironments()
const serversStore = usePlatforms()
const tagsStore = useTags()

const environmentOptions = environmentsStore.options
const serverOptions = serversStore.options
const tagOptions = tagsStore.options

const form = reactive<DataStoreFormModel>({
  name: '',
  kind: '',
  environmentId: null,
  platformId: null,
  connectionUri: '',
  tagIds: []
})

const isCreate = computed(() => props.mode === 'create')
const title = computed(() => (isCreate.value ? 'Create Data Store' : 'Edit Data Store'))
const submitLabel = computed(() => (isCreate.value ? 'Create' : 'Save'))
const loading = computed(() => props.loading)

function applyInitialValue(value?: Partial<DataStore> | null) {
  if (!value) {
    form.name = ''
    form.kind = ''
    form.environmentId = null
    form.platformId = null
    form.connectionUri = ''
    form.tagIds = []
    return
  }
  form.name = value.name ?? ''
  form.kind = value.kind ?? ''
  form.environmentId = value.environmentId ?? null
  form.platformId = value.platformId ?? null
  form.connectionUri = value.connectionUri ?? ''
  form.tagIds = [...(value.tagIds ?? [])]
}

onMounted(() => applyInitialValue(props.initialValue))
watch(() => props.initialValue, (val) => applyInitialValue(val))

function handleSubmit() {
  emit('submit', { ...form })
}
</script>

<style scoped>
@import '../../styles/pages.css';

.form-dialog {
  min-width: 520px;
}
</style>