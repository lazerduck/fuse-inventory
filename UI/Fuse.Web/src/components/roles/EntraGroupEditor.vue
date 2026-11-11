<template>
  <div class="q-mt-md">
    <div class="text-subtitle2">Entra groups</div>
    <p class="text-caption">Members of any linked group inherit this role. Direct Fuse assignments remain available.</p>
    <div v-for="(group, index) in modelValue" :key="index" class="row q-col-gutter-sm q-mb-sm">
      <q-input
        class="col-12"
        outlined
        dense
        label="Group object ID"
        :model-value="group.groupId"
        @update:model-value="update(index, 'groupId', String($event ?? ''))"
        :rules="[validId]"
      />
      <q-input
        class="col"
        outlined
        dense
        label="Group display name"
        :model-value="group.displayName"
        @update:model-value="update(index, 'displayName', String($event ?? ''))"
        :rules="[(v) => !!v || 'Name required']"
      />
      <q-btn flat icon="delete" aria-label="Remove group mapping" @click="removeGroup(index)" />
    </div>
    <q-btn flat color="primary" icon="add" label="Link Entra group" @click="addGroup" />
  </div>
</template>
<script setup lang="ts">
import type { IEntraGroupMapping } from 'api/client'

interface GroupForm extends IEntraGroupMapping {
  groupId: string
  displayName: string
}

interface Props {
  modelValue: GroupForm[]
}

const props = defineProps<Props>()
const emit = defineEmits<{ 'update:modelValue': [value: GroupForm[]] }>()

function validId(value: string) {
  return /^[0-9a-f]{8}(-[0-9a-f]{4}){3}-[0-9a-f]{12}$/i.test(value) || 'Enter a group object ID'
}

function update(index: number, field: keyof GroupForm, value: string) {
  const groups = props.modelValue.map((group, groupIndex) =>
    groupIndex === index ? { ...group, [field]: value } : group
  )
  emit('update:modelValue', groups)
}

function addGroup() {
  emit('update:modelValue', [...props.modelValue, { groupId: '', displayName: '' }])
}

function removeGroup(index: number) {
  emit(
    'update:modelValue',
    props.modelValue.filter((_, groupIndex) => groupIndex !== index)
  )
}
</script>
