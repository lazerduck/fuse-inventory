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
          <q-input v-model="form.displayName" label="Name*" dense outlined :rules="[v => !!v || 'Display Name is required']" />
          <q-input v-model="form.dnsName" label="DNS Name" dense outlined />
          <q-input v-model="form.os" label="Operating System" dense outlined />
          <q-select
            v-model="form.kind"
            label="Kind"
            dense
            outlined
            emit-value
            map-options
            clearable
            :options="kindOptions"
          />
          <q-input v-model="form.ipAddress" label="IP Address" dense outlined />
          <q-input v-model="form.notes" label="Notes" type="textarea" dense outlined />
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
import { useTags } from '../../composables/useTags'
import { PlatformKind, type Platform } from '../../api/client'

type Mode = 'create' | 'edit'

export interface PlatformFormModel {
  displayName: string
  dnsName: string
  os: string | null
  kind: PlatformKind | null
  ipAddress: string | null
  notes: string | null
  tagIds: string[]
}

interface Props {
  mode?: Mode
  initialValue?: Partial<Platform> | null
  loading?: boolean
}

interface Emits {
  (e: 'submit', payload: PlatformFormModel): void
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

const kindOptions = Object.values(PlatformKind)
  .map(value => ({ label: value, value: value as PlatformKind }))

const form = reactive<PlatformFormModel>({
  displayName: '',
  dnsName: '',
  os: null,
  kind: null,
  ipAddress: null,
  notes: null,
  tagIds: []
})

const isCreate = computed(() => props.mode === 'create')
const title = computed(() => (isCreate.value ? 'Create Platform' : 'Edit Platform'))
const submitLabel = computed(() => (isCreate.value ? 'Create' : 'Save'))
const loading = computed(() => props.loading)

function applyInitialValue(value?: Partial<Platform> | null) {
  if (!value) {
    form.displayName = ''
    form.dnsName = ''
    form.os = null
    form.tagIds = []
    form.kind = null
    form.ipAddress = null
    form.notes = null
    return
  }
  form.displayName = value.displayName ?? ''
  form.dnsName = value.dnsName ?? ''
  form.os = value.os ?? null
  form.kind = value.kind ?? null
  form.ipAddress = value.ipAddress ?? null
  form.notes = value.notes ?? null
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
