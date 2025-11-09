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
          <q-input v-model="form.displayName" label="Name*" dense outlined :rules="[v => !!v || 'Name is required']" />
          <q-input v-model="form.dnsName" label="DNS Name*" dense outlined :rules="[v => !!v || 'DNS Name is required']" />
          <q-select
            v-model="form.os"
            label="Operating System"
            dense
            outlined
            emit-value
            map-options
            clearable
            :options="osOptions"
          />
          <q-select
            v-model="form."
            label="*"
            dense
            outlined
            emit-value
            map-options
            :options="environmentOptions"
            :rules="[v => !!v || ' is required']"
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
import { uses } from '../../composables/uses'
import { useTags } from '../../composables/useTags'
import { PlatformOperatingSystem, type Platform } from '../../api/client'

type Mode = 'create' | 'edit'

interface PlatformFormModel {
  name: string
  dnsName: string
  os: PlatformOperatingSystem | null
  : string | null
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

const environmentsStore = uses()
const tagsStore = useTags()

const environmentOptions = environmentsStore.options
const tagOptions = tagsStore.options

const osOptions = Object.values(PlatformOperatingSystem)
  .map(value => ({ label: value, value: value as PlatformOperatingSystem }))

const form = reactive<PlatformFormModel>({
  name: '',
  dnsName: '',
  os: null,
  : null,
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
    form. = null
    form.tagIds = []
    return
  }
  form.displayName = value.displayName ?? ''
  form.dnsName = value.dnsName ?? ''
  form.os = (value.os as any) ?? null
  form. = value. ?? null
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
