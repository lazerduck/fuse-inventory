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
          {{ result?.success ? 'Account Imported' : 'Import Failed' }}
        </span>
      </q-card-section>

      <q-card-section v-if="result">
        <div v-if="result.success" class="q-mb-md">
          <div class="text-subtitle2 q-mb-sm">
            Account created for <strong>{{ result.principalName }}</strong>
          </div>
          <div v-if="result.importedGrants?.length" class="text-caption text-grey-7 q-mb-xs">
            Imported {{ result.importedGrants.length }} grant(s)
          </div>
          <q-btn
            flat
            dense
            color="primary"
            label="View Account"
            icon="visibility"
            class="q-mt-sm"
            @click="emit('view-account', result.accountId)"
          />
        </div>
        <div v-if="result.errorMessage" class="text-negative">
          {{ result.errorMessage }}
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
import type { ImportOrphanPrincipalResponse } from '../../../api/client'

interface Props {
  modelValue: boolean
  result?: ImportOrphanPrincipalResponse | null
}

const props = withDefaults(defineProps<Props>(), {
  result: null
})

const emit = defineEmits<{
  (e: 'update:modelValue', value: boolean): void
  (e: 'view-account', accountId: string | undefined | null): void
}>()

const dialogModel = computed({
  get: () => props.modelValue,
  set: (value: boolean) => emit('update:modelValue', value)
})
</script>
