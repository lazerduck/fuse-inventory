<template>
  <q-card class="form-dialog">
    <q-card-section class="dialog-header">
      <div class="text-h6">{{ isCreate ? 'New App Configuration Entry' : 'Edit App Configuration Entry' }}</div>
      <q-btn flat round dense icon="close" @click="emit('cancel')" />
    </q-card-section>
    <q-separator />

    <q-form @submit.prevent="handleSubmit">
      <q-card-section>
        <div class="form-grid">
          <q-input
            v-model="form.key"
            label="Key*"
            dense
            outlined
            required
            :readonly="!isCreate"
            :hint="isCreate ? '' : 'Key cannot be changed when editing'"
            :rules="[val => !!val || 'Key is required']"
          />
          <q-input
            v-model="form.label"
            label="Label"
            dense
            outlined
            clearable
            :readonly="!isCreate"
            :hint="isCreate ? 'Optional label to distinguish between environments (e.g. prod, dev)' : 'Label cannot be changed when editing'"
          />

          <div class="full-span">
            <q-input
              v-model="form.value"
              label="Value*"
              dense
              outlined
              required
              type="textarea"
              autogrow
              :rules="[val => val !== null && val !== undefined && val !== '' || 'Value is required']"
            />
          </div>

          <!-- Show diff when editing -->
          <template v-if="!isCreate && initialEntry?.value !== undefined && initialEntry?.value !== null">
            <div class="full-span">
              <div class="text-caption text-grey-7 q-mb-xs">Previous value:</div>
              <div class="diff-box text-body2">{{ initialEntry.value }}</div>
            </div>
          </template>

          <template v-if="initialEntry?.contentType">
            <div class="full-span">
              <div class="text-caption text-grey-7">Content Type: <span class="text-body2 text-grey-9">{{ initialEntry.contentType }}</span></div>
            </div>
          </template>
        </div>
      </q-card-section>

      <q-separator />
      <q-card-actions align="right">
        <q-btn flat label="Cancel" @click="emit('cancel')" />
        <q-btn
          color="primary"
          type="submit"
          :label="isCreate ? 'Create' : 'Save'"
          :loading="loading"
          :disable="loading"
        />
      </q-card-actions>
    </q-form>
  </q-card>
</template>

<script setup lang="ts">
import { computed, reactive, watch } from 'vue'
import type { AppConfigurationEntry } from '../../composables/useAppConfigurationEntries'

interface FormModel {
  key: string
  label: string
  value: string
}

interface Props {
  initialEntry?: AppConfigurationEntry | null
  loading?: boolean
}

interface Emits {
  (e: 'submit', payload: FormModel): void
  (e: 'cancel'): void
}

const props = withDefaults(defineProps<Props>(), {
  initialEntry: null,
  loading: false
})
const emit = defineEmits<Emits>()

const isCreate = computed(() => !props.initialEntry?.key)

const form = reactive<FormModel>({
  key: '',
  label: '',
  value: ''
})

function applyInitialEntry(entry?: AppConfigurationEntry | null) {
  if (!entry) {
    form.key = ''
    form.label = ''
    form.value = ''
    return
  }
  form.key = entry.key ?? ''
  form.label = entry.label ?? ''
  form.value = entry.value ?? ''
}

watch(() => props.initialEntry, (val) => applyInitialEntry(val), { immediate: true })

function handleSubmit() {
  emit('submit', { ...form })
}
</script>

<style scoped>
@import '../../styles/pages.css';

.form-dialog {
  min-width: 520px;
  max-width: 650px;
}

.diff-box {
  background: var(--q-dark-page, #f5f5f5);
  border-left: 3px solid var(--q-warning, #f59e0b);
  border-radius: 4px;
  padding: 8px 12px;
  font-family: monospace;
  font-size: 0.85em;
  white-space: pre-wrap;
  word-break: break-all;
  color: #666;
}
</style>
