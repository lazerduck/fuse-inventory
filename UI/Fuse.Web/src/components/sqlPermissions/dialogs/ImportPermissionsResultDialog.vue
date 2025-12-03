<template>
  <q-dialog v-model="dialogModel">
    <q-card style="min-width: 400px">
      <q-card-section class="row items-center">
        <q-icon
          :name="result?.success ? 'check_circle' : 'error'"
          :color="result?.success ? 'positive' : 'negative'"
          size="2em"
          class="q-mr-sm"
        />
        <span class="text-h6">
          {{ result?.success ? 'Permissions Imported' : 'Import Failed' }}
        </span>
      </q-card-section>

      <q-card-section v-if="result">
        <div v-if="result.success && result.importedGrants?.length" class="q-mb-md">
          <div class="text-subtitle2 q-mb-sm">Imported {{ result.importedGrants.length }} grant(s):</div>
          <div v-for="grant in result.importedGrants" :key="grant.id" class="text-caption q-mb-xs">
            <q-icon name="check" color="positive" size="xs" />
            {{ grant.database ?? 'default' }}{{ grant.schema ? `.${grant.schema}` : '' }}:
            {{ Array.from(grant.privileges || []).join(', ') }}
          </div>
        </div>
        <div v-if="result.errorMessage" class="text-negative">
          {{ result.errorMessage }}
        </div>
        <div v-if="result.updatedStatus" class="q-mt-md">
          <span class="text-subtitle2">Updated status: </span>
          <StatusBadge :status="result.updatedStatus.status" />
        </div>
      </q-card-section>

      <q-card-actions align="right">
        <q-btn flat label="Close" color="primary" v-close-popup />
      </q-card-actions>
    </q-card>
  </q-dialog>
</template>

<script setup lang="ts">
import { computed } from 'vue'
import StatusBadge from '../../sqlPermissions/StatusBadge.vue'
import type { ImportPermissionsResponse } from '../../../api/client'

interface Props {
  modelValue: boolean
  result?: ImportPermissionsResponse | null
}

const props = withDefaults(defineProps<Props>(), {
  result: null
})

const emit = defineEmits<{
  (e: 'update:modelValue', value: boolean): void
}>()

const dialogModel = computed({
  get: () => props.modelValue,
  set: (value: boolean) => emit('update:modelValue', value)
})
</script>
