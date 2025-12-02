<template>
  <q-dialog v-model="dialogVisible" persistent>
    <q-card class="form-dialog">
      <q-card-section class="dialog-header">
        <div class="text-h6">{{ dialogTitle }}</div>
        <q-btn flat round dense icon="close" @click="handleCancel" />
      </q-card-section>
      <q-separator />
      <q-form @submit.prevent="handleSubmit">
        <q-card-section>
          <div class="form-grid">
            <q-select
              v-if="hasSqlIntegration"
              v-model="grantForm.database"
              label="Database"
              dense
              outlined
              use-input
              hide-selected
              fill-input
              input-debounce="0"
              :options="filteredDatabaseOptions"
              :loading="isDatabasesLoading"
              @filter="filterDatabaseOptions"
              @input-value="handleDatabaseInput"
              hint="Select from available databases or enter a custom name"
              new-value-mode="add"
            >
              <template #no-option>
                <q-item>
                  <q-item-section class="text-grey">
                    {{ isDatabasesLoading ? 'Loading databases...' : 'No databases found. Type to enter a custom name.' }}
                  </q-item-section>
                </q-item>
              </template>
            </q-select>
            <q-input
              v-else
              v-model="grantForm.database"
              label="Database"
              dense
              outlined
              hint="Enter database name"
            />
            <q-input v-model="grantForm.schema" label="Schema" dense outlined />
            <q-select
              v-model="grantForm.privileges"
              label="Privileges"
              dense
              outlined
              use-chips
              multiple
              emit-value
              map-options
              :options="privilegeOptions"
            />
          </div>
        </q-card-section>
        <q-separator />
        <q-card-actions align="right">
          <q-btn flat label="Cancel" @click="handleCancel" />
          <q-btn
            color="primary"
            type="submit"
            :label="isEditing ? 'Save' : 'Create'"
            :loading="loading"
          />
        </q-card-actions>
      </q-form>
    </q-card>
  </q-dialog>
</template>

<script setup lang="ts">
import { computed, reactive, ref, watch } from 'vue'
import type { Privilege } from '../../../api/client'
import type { SelectOption } from '../../../components/accounts/types'
import type { Grant } from '../../../api/client'

interface Props {
  modelValue: boolean
  initialGrant: Grant | null
  hasSqlIntegration: boolean
  databaseOptions: Array<{ label: string; value: string }>
  isDatabasesLoading: boolean
  privilegeOptions: SelectOption<Privilege>[]
  loading: boolean
}

interface GrantFormState {
  database: string
  schema: string
  privileges: Privilege[]
}

const props = defineProps<Props>()
const emit = defineEmits<{
  (e: 'update:modelValue', value: boolean): void
  (e: 'save', payload: GrantFormState): void
}>()

const dialogVisible = computed({
  get: () => props.modelValue,
  set: (value: boolean) => emit('update:modelValue', value)
})

const grantForm = reactive<GrantFormState>({ database: '', schema: '', privileges: [] })
const filteredDatabaseOptions = ref(props.databaseOptions)

const dialogTitle = computed(() => (props.initialGrant ? 'Edit Grant' : 'Add Grant'))
const isEditing = computed(() => !!props.initialGrant)

watch(
  () => props.modelValue,
  (visible) => {
    if (visible) {
      initializeForm()
      filteredDatabaseOptions.value = props.databaseOptions
    }
  }
)

watch(
  () => props.databaseOptions,
  (options) => {
    filteredDatabaseOptions.value = options
  }
)

function initializeForm() {
  if (props.initialGrant) {
    grantForm.database = props.initialGrant.database ?? ''
    grantForm.schema = props.initialGrant.schema ?? ''
    grantForm.privileges = [...(props.initialGrant.privileges ?? [])]
  } else {
    grantForm.database = ''
    grantForm.schema = ''
    grantForm.privileges = []
  }
}

function filterDatabaseOptions(value: string, update: (callback: () => void) => void) {
  update(() => {
    const needle = value.toLowerCase()
    filteredDatabaseOptions.value = props.databaseOptions.filter((option) =>
      option.label.toLowerCase().includes(needle)
    )
  })
}

function handleDatabaseInput(value: string) {
  grantForm.database = value
}

function handleCancel() {
  dialogVisible.value = false
}

function handleSubmit() {
  emit('save', {
    database: grantForm.database,
    schema: grantForm.schema,
    privileges: [...grantForm.privileges]
  })
}
</script>
