<template>
  <div class="form-grid">
    <q-select
      v-model="form.targetKind"
      label="Target Kind"
      dense
      outlined
      emit-value
      map-options
      :options="targetKindOptions"
    />
    <q-select
      v-model="form.targetId"
      label="Target"
      dense
      outlined
      emit-value
      map-options
      :options="targetOptions"
    />
    <q-select
      v-model="form.authKind"
      label="Auth Kind"
      dense
      outlined
      emit-value
      map-options
      :options="authKindOptions"
    />
    <q-input v-model="form.userName" label="Username" dense outlined />
    <q-input v-model="form.secretRef" label="Secret Reference" dense outlined />
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

  <div class="parameters-section">
    <div class="section-header q-mt-md">
      <div class="text-subtitle1">Parameters</div>
      <q-btn dense flat icon="add" label="Add" @click="addParameter" />
    </div>
    <div v-if="form.parameters.length" class="parameter-grid">
      <div v-for="(pair, index) in form.parameters" :key="index" class="parameter-row">
        <q-input v-model="pair.key" label="Key" dense outlined />
        <q-input v-model="pair.value" label="Value" dense outlined />
        <q-btn flat dense round icon="delete" color="negative" @click="removeParameter(index)" />
      </div>
    </div>
    <div v-else class="text-grey">No parameters.</div>
  </div>
</template>

<script setup lang="ts">
import { toRefs } from 'vue'
import type { AccountFormModel, TargetOption, SelectOption } from './types'
import type { AuthKind, TargetKind } from '../../api/client'

const form = defineModel<AccountFormModel>({ required: true })

const props = defineProps<{
  targetKindOptions: SelectOption<TargetKind>[]
  targetOptions: TargetOption[]
  authKindOptions: SelectOption<AuthKind>[]
  tagOptions: TargetOption[]
}>()

const { targetKindOptions, targetOptions, authKindOptions, tagOptions } = toRefs(props)

function addParameter() {
  form.value.parameters.push({ key: '', value: '' })
}

function removeParameter(index: number) {
  form.value.parameters.splice(index, 1)
}
</script>

<style scoped>
.form-grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(220px, 1fr));
  gap: 1rem;
}

.parameters-section {
  margin-top: 1.5rem;
}

.section-header {
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
  gap: 0.5rem;
}

.parameter-grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(220px, 1fr));
  gap: 1rem;
}

.parameter-row {
  display: contents;
}

.parameter-row > *:nth-child(3) {
  align-self: center;
}
</style>
