<template>
  <q-dialog v-model="model" persistent>
    <q-card class="form-dialog large">
      <q-card-section class="dialog-header">
        <div class="text-h6">{{ title }}</div>
        <q-btn flat round dense icon="close" @click="closeDialog" />
      </q-card-section>
      <q-separator />
      <q-form ref="formRef" @submit.prevent="handleSubmit">
        <q-card-section>
          <q-banner v-if="loadError" dense class="bg-red-1 text-negative q-mb-md">
            {{ loadError }}
          </q-banner>
          <div class="form-grid">
            <q-select
              v-model="form.positionId"
              label="Position*"
              dense
              outlined
              emit-value
              map-options
              :options="positionOptions"
              :loading="isLoading"
              :rules="[value => !!value || 'Position is required']"
            />
            <q-select
              v-model="form.responsibilityTypeId"
              label="Responsibility Type*"
              dense
              outlined
              emit-value
              map-options
              :options="responsibilityTypeOptions"
              :loading="isLoading"
              :rules="[value => !!value || 'Responsibility type is required']"
            />
            <div class="full-span">
              <div class="text-subtitle2 q-mb-xs">Scope</div>
              <q-option-group
                v-model="form.scope"
                type="radio"
                inline
                :options="scopeOptions"
              />
            </div>
            <q-select
              v-model="form.environmentId"
              label="Environment"
              dense
              outlined
              emit-value
              map-options
              :options="environmentOptions"
              :loading="isLoading"
              :disable="form.scope !== ResponsibilityScope.Environment"
              :rules="environmentRules"
            />
            <q-checkbox v-model="form.primary" label="Primary responsibility" />
            <q-input
              v-model="form.notes"
              label="Notes"
              dense
              outlined
              type="textarea"
              autogrow
              class="full-span"
            />
          </div>
        </q-card-section>
        <q-separator />
        <q-card-actions align="right">
          <q-btn flat label="Cancel" @click="closeDialog" />
          <q-btn color="primary" type="submit" :label="submitLabel" :loading="loading" />
        </q-card-actions>
      </q-form>
    </q-card>
  </q-dialog>
</template>

<script setup lang="ts">
import { computed, reactive, ref, watch } from 'vue'
import { useQuery } from '@tanstack/vue-query'
import { ResponsibilityAssignment, ResponsibilityScope } from '../../api/client'
import { useFuseClient } from '../../composables/useFuseClient'
import { useEnvironments } from '../../composables/useEnvironments'
import { getErrorMessage } from '../../utils/error'

interface ResponsibilityAssignmentFormModel {
  positionId: string | null
  responsibilityTypeId: string | null
  scope: ResponsibilityScope
  environmentId: string | null
  notes: string
  primary: boolean
}

interface Props {
  applicationId: string
  assignment?: ResponsibilityAssignment | null
  loading?: boolean
}

interface Emits {
  (e: 'save', payload: ResponsibilityAssignmentFormModel): void
  (e: 'update:modelValue', value: boolean): void
}

const model = defineModel<boolean>({ required: true })

const props = withDefaults(defineProps<Props>(), {
  assignment: null,
  loading: false
})

const emit = defineEmits<Emits>()

const formRef = ref()

const client = useFuseClient()
const environmentsStore = useEnvironments()

const positionsQuery = useQuery({
  queryKey: ['positions'],
  queryFn: () => client.positionAll()
})

const responsibilityTypesQuery = useQuery({
  queryKey: ['responsibilityTypes'],
  queryFn: () => client.responsibilityTypeAll()
})

const form = reactive<ResponsibilityAssignmentFormModel>({
  positionId: null,
  responsibilityTypeId: null,
  scope: ResponsibilityScope.All,
  environmentId: null,
  notes: '',
  primary: false
})

const isEdit = computed(() => !!props.assignment?.id)
const title = computed(() => (isEdit.value ? 'Edit Responsibility Assignment' : 'Add Responsibility Assignment'))
const submitLabel = computed(() => (isEdit.value ? 'Save' : 'Add'))
const loading = computed(() => props.loading)

const isLoading = computed(
  () => positionsQuery.isLoading.value || responsibilityTypesQuery.isLoading.value || environmentsStore.isLoading.value
)

const loadError = computed(() => {
  const error = positionsQuery.error.value || responsibilityTypesQuery.error.value || environmentsStore.error.value
  return error ? getErrorMessage(error) : null
})

const scopeOptions = [
  { label: 'All environments', value: ResponsibilityScope.All },
  { label: 'Environment', value: ResponsibilityScope.Environment }
]

const positionOptions = computed(() =>
  (positionsQuery.data.value ?? [])
    .filter((position) => !!position.id)
    .map((position) => ({
      label: position.name ?? formatGuid(position.id),
      value: position.id!
    }))
)

const responsibilityTypeOptions = computed(() =>
  (responsibilityTypesQuery.data.value ?? [])
    .filter((type) => !!type.id)
    .map((type) => ({
      label: type.name ?? formatGuid(type.id),
      value: type.id!
    }))
)

const environmentOptions = computed(() => environmentsStore.options.value)

const environmentRules = [
  (value: string | null) =>
    form.scope !== ResponsibilityScope.Environment || !!value || 'Environment is required for environment scope'
]

function applyInitialValue(value?: ResponsibilityAssignment | null) {
  if (!value) {
    form.positionId = null
    form.responsibilityTypeId = null
    form.scope = ResponsibilityScope.All
    form.environmentId = null
    form.notes = ''
    form.primary = false
    return
  }
  form.positionId = value.positionId ?? null
  form.responsibilityTypeId = value.responsibilityTypeId ?? null
  form.scope = value.scope ?? ResponsibilityScope.All
  form.environmentId = value.environmentId ?? null
  form.notes = value.notes ?? ''
  form.primary = value.primary ?? false
}

watch(
  () => props.assignment,
  (value) => {
    if (model.value) {
      applyInitialValue(value)
    }
  },
  { immediate: true }
)

watch(model, (value) => {
  if (value) {
    applyInitialValue(props.assignment)
  }
})

watch(
  () => form.scope,
  (value) => {
    if (value !== ResponsibilityScope.Environment) {
      form.environmentId = null
    }
  }
)

function closeDialog() {
  emit('update:modelValue', false)
}

async function handleSubmit() {
  const valid = await formRef.value?.validate?.()
  if (valid === false) return
  emit('save', { ...form })
}

function formatGuid(value?: string | null) {
  return value ? value.toUpperCase() : '—'
}
</script>

<style scoped>
@import '../../styles/pages.css';
</style>
