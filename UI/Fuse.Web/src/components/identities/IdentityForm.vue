<template>
  <div class="form-grid">
    <q-input v-model="form.name" label="Name" dense outlined required />
    <q-select
      v-model="form.kind"
      label="Identity Kind"
      dense
      outlined
      emit-value
      map-options
      :options="identityKindOptions"
    />
    <q-select
      v-model="form.ownerInstanceId"
      label="Owner Instance"
      dense
      outlined
      emit-value
      map-options
      :options="instanceOptions"
      clearable
      hint="Optional: If set, only this instance can use this identity"
    />
    <q-input 
      v-model="form.notes" 
      label="Notes" 
      dense 
      outlined 
      type="textarea"
      rows="3"
    />
    <TagSelect v-model="form.tagIds" />
  </div>
</template>

<script setup lang="ts">
import { toRefs } from 'vue'
import type { IdentityFormModel, SelectOption, TargetOption } from './types'
import type { IdentityKind } from '../../api/client'
import TagSelect from '../tags/TagSelect.vue'

const form = defineModel<IdentityFormModel>({ required: true })

const props = defineProps<{
  identityKindOptions: SelectOption<IdentityKind>[]
  instanceOptions: TargetOption[]
}>()

const { identityKindOptions, instanceOptions } = toRefs(props)
</script>

<style scoped>
.form-grid {
  display: grid;
  gap: 1rem;
  grid-template-columns: 1fr;
}

@media (min-width: 768px) {
  .form-grid {
    grid-template-columns: 1fr 1fr;
  }
}
</style>
