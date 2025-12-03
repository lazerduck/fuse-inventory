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
          {{ result?.success ? 'Account Created' : 'Creation Failed' }}
        </span>
      </q-card-section>

      <q-card-section v-if="result">
        <div v-if="result.success" class="q-mb-md">
          <span class="text-subtitle2">Password source: </span>
          <q-badge outline color="primary" :label="passwordSourceLabel" />
        </div>

        <div v-if="result.operations?.length" class="q-mb-md">
          <div class="text-subtitle2 q-mb-sm">Operations performed:</div>
          <div v-for="(op, index) in result.operations" :key="index" class="text-caption q-mb-xs">
            <q-icon :name="op.success ? 'check' : 'close'" :color="op.success ? 'positive' : 'negative'" size="xs" />
            {{ op.operationType }}
            <span v-if="op.database">in {{ op.database }}</span>
            <span v-if="!op.success" class="text-negative"> - {{ op.errorMessage }}</span>
          </div>
        </div>

        <div v-if="result.errorMessage" class="text-negative">
          {{ result.errorMessage }}
        </div>

        <div v-if="result.updatedStatus" class="q-mt-md">
          <span class="text-subtitle2">Account status: </span>
          <StatusBadge :status="result.updatedStatus.status" />
          <div
            v-if="result.success && result.updatedStatus.status === SyncStatus.DriftDetected"
            class="text-caption text-orange q-mt-sm"
          >
            <q-icon name="info" size="xs" />
            Account created but permissions need to be applied. Use "Resolve" to apply expected permissions.
          </div>
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
import { PasswordSourceUsed, SyncStatus, type CreateSqlAccountResponse } from '../../../api/client'

interface Props {
  modelValue: boolean
  result?: CreateSqlAccountResponse | null
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

const passwordSourceLabel = computed(() => {
  switch (props.result?.passwordSource) {
    case PasswordSourceUsed.SecretProvider:
      return 'Secret Provider'
    case PasswordSourceUsed.Manual:
      return 'Manual Entry'
    case PasswordSourceUsed.NewSecret:
      return 'New Secret Created'
    default:
      return 'Unknown'
  }
})
</script>
