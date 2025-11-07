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
          <q-input v-model="form.hostname" label="Hostname*" dense outlined :rules="[v => !!v || 'Hostname is required']" />
          <q-select
            v-model="form.operatingSystem"
            label="Operating System"
            dense
            outlined
            emit-value
            map-options
            clearable
            :options="operatingSystemOptions"
          />
          <q-select
            v-model="form.environmentId"
            label="Environment*"
            dense
            outlined
            emit-value
            map-options
            :options="environmentOptions"
            :rules="[v => !!v || 'Environment is required']"
          />
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
import { computed, reactive, onMounted, watch } from 'vue'
import { useEnvironments } from '../../composables/useEnvironments'
import { useTags } from '../../composables/useTags'
import type { Server, ServerOperatingSystem } from '../../api/client'

type Mode = 'create' | 'edit'

interface ServerFormModel {
  name: string
  hostname: string
  operatingSystem: ServerOperatingSystem | null
  environmentId: string | null
  tagIds: string[]
}

interface Props {
  mode?: Mode
  initialValue?: Partial<Server> | null
  loading?: boolean
}

interface Emits {
  (e: 'submit', payload: ServerFormModel): void
  (e: 'cancel'): void
}

const props = withDefaults(defineProps<Props>(), {
  mode: 'create',
  initialValue: null,
  loading: false
})
const emit = defineEmits<Emits>()

const environmentsStore = useEnvironments()
const tagsStore = useTags()

const environmentOptions = environmentsStore.options
const tagOptions = tagsStore.options

const operatingSystemOptions = Object.values(({} as any as typeof import('../../api/client').ServerOperatingSystem))
  .map((value: any) => ({ label: value, value }))

const form = reactive<ServerFormModel>({
  name: '',
  hostname: '',
  operatingSystem: null,
  environmentId: null,
  tagIds: []
})

const isCreate = computed(() => props.mode === 'create')
const title = computed(() => (isCreate.value ? 'Create Server' : 'Edit Server'))
const submitLabel = computed(() => (isCreate.value ? 'Create' : 'Save'))
const loading = computed(() => props.loading)

function applyInitialValue(value?: Partial<Server> | null) {
  if (!value) {
    form.name = ''
    form.hostname = ''
    form.operatingSystem = null
    form.environmentId = null
    form.tagIds = []
    return
  }
  form.name = value.name ?? ''
  form.hostname = value.hostname ?? ''
  form.operatingSystem = (value.operatingSystem as any) ?? null
  form.environmentId = value.environmentId ?? null
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

.form-dialog {
  min-width: 520px;
}
</style>
