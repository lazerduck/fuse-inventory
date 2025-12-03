<template>
  <q-dialog v-model="dialogModel" persistent>
    <q-card style="min-width: 400px">
      <q-card-section class="row items-center">
        <q-icon name="download" color="secondary" size="2em" class="q-mr-sm" />
        <span class="text-h6">Import Permissions from SQL</span>
      </q-card-section>

      <q-card-section>
        <p v-if="account">
          Replace <strong>{{ account.principalName }}</strong>'s
          Fuse configuration with actual SQL permissions?
        </p>
        <q-banner dense class="bg-orange-1 text-orange-9 q-mt-md">
          <template #avatar>
            <q-icon name="warning" color="orange" />
          </template>
          This will overwrite the current permission configuration in Fuse with
          the actual permissions found in SQL Server.
        </q-banner>
      </q-card-section>

      <q-card-actions align="right">
        <q-btn flat label="Cancel" color="grey" v-close-popup :disable="isImporting" />
        <q-btn
          flat
          label="Import from SQL"
          color="secondary"
          :loading="isImporting"
          @click="emit('confirm')"
        />
      </q-card-actions>
    </q-card>
  </q-dialog>
</template>

<script setup lang="ts">
import { computed } from 'vue'
import type { SqlAccountPermissionsStatus } from '../../../api/client'

interface Props {
  modelValue: boolean
  account?: SqlAccountPermissionsStatus | null
  isImporting?: boolean
}

const props = withDefaults(defineProps<Props>(), {
  account: null,
  isImporting: false
})

const emit = defineEmits<{
  (e: 'update:modelValue', value: boolean): void
  (e: 'confirm'): void
}>()

const dialogModel = computed({
  get: () => props.modelValue,
  set: (value: boolean) => emit('update:modelValue', value)
})
</script>
