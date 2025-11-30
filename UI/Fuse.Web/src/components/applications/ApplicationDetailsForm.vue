<template>
  <q-card class="content-card q-mb-md">
    <q-card-section class="dialog-header">
      <div class="text-h6">Application Details</div>
    </q-card-section>
    <q-separator />
    <q-form @submit.prevent="handleSubmit">
      <q-card-section>
        <div class="form-grid">
          <q-input v-model="form.name" label="Name" dense outlined />
          <q-input v-model="form.version" label="Version" dense outlined />
          <q-input v-model="form.owner" label="Owner" dense outlined />
          <q-input v-model="form.framework" label="Framework" dense outlined />
          <q-input v-model="form.repositoryUri" label="Repository URI" dense outlined />
          <q-select
            v-model="form.icon"
            :options="iconOptions"
            label="Icon"
            dense
            outlined
            emit-value
            map-options
            clearable
          >
            <template #prepend>
              <q-icon :name="form.icon || DEFAULT_APPLICATION_ICON" />
            </template>
            <template #option="scope">
              <q-item v-bind="scope.itemProps">
                <q-item-section avatar>
                  <q-icon :name="scope.opt.value" />
                </q-item-section>
                <q-item-section>
                  <q-item-label>{{ scope.opt.label }}</q-item-label>
                </q-item-section>
              </q-item>
            </template>
          </q-select>
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
          <q-input
            v-model="form.notes"
            type="textarea"
            label="Notes"
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
        <q-btn v-if="showDelete" flat label="Delete Application" color="negative" class="q-mr-auto" @click="emit('delete')" />
        <q-btn color="primary" type="submit" label="Save" :loading="loading" />
      </q-card-actions>
    </q-form>
  </q-card>
</template>

<script setup lang="ts">
import { computed, onMounted, reactive, watch } from 'vue'
import type { Application } from '../../api/client'
import TagSelect from '../tags/TagSelect.vue'
import { APPLICATION_ICON_OPTIONS, DEFAULT_APPLICATION_ICON } from '../../constants/applicationIcons'

interface ApplicationFormModel {
  name: string
  version: string
  description: string
  owner: string
  notes: string
  framework: string
  repositoryUri: string
  icon: string
  tagIds: string[]
}

interface Props {
  initialValue?: Partial<Application> | null
  loading?: boolean
  showDelete?: boolean
}

interface Emits {
  (e: 'submit', value: ApplicationFormModel): void
  (e: 'cancel'): void
  (e: 'delete'): void
}

const props = withDefaults(defineProps<Props>(), {
  initialValue: null,
  loading: false,
  showDelete: true
})
const emit = defineEmits<Emits>()

const iconOptions = APPLICATION_ICON_OPTIONS

const form = reactive<ApplicationFormModel>({
  name: '',
  version: '',
  description: '',
  owner: '',
  notes: '',
  framework: '',
  repositoryUri: '',
  icon: '',
  tagIds: []
})

const loading = computed(() => props.loading)

function applyInitial(value?: Partial<Application> | null) {
  if (!value) {
    form.name = ''
    form.version = ''
    form.description = ''
    form.owner = ''
    form.notes = ''
    form.framework = ''
    form.repositoryUri = ''
    form.icon = ''
    form.tagIds = []
    return
  }
  form.name = value.name ?? ''
  form.version = value.version ?? ''
  form.description = value.description ?? ''
  form.owner = value.owner ?? ''
  form.notes = value.notes ?? ''
  form.framework = value.framework ?? ''
  form.repositoryUri = value.repositoryUri ?? ''
  form.icon = value.icon ?? ''
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
</style>
