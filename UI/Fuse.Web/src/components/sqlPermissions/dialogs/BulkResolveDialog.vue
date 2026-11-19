<template>
  <q-dialog v-model="dialogModel" persistent>
    <q-card style="width: 38rem; max-width: 95vw">
      <q-card-section class="row items-center">
        <q-icon name="auto_fix_high" color="primary" size="2em" class="q-mr-sm" />
        <span class="text-h6">Bulk Resolve All</span>
      </q-card-section>

      <q-card-section>
        <div v-if="loading" role="status"><q-spinner class="q-mr-sm" />Inspecting current SQL permissions…</div>
        <q-banner v-else-if="error" dense role="alert">
          {{ error }}
          <template #action><q-btn flat label="Refresh" @click="emit('refresh')" /></template>
        </q-banner>
        <q-list v-else-if="operations.length" bordered separator aria-label="Planned SQL operations">
          <q-item v-for="(item, index) in operations" :key="index">
            <q-item-section>
              <q-item-label>{{ item.operation }} {{ item.privilege ?? '' }} · {{ item.principal }}</q-item-label>
              <q-item-label caption>{{ item.reason || (item.schema ? item.database + ' / schema ' + item.schema : item.database || 'Server login') }}</q-item-label>
            </q-item-section>
          </q-item>
        </q-list>
        <p v-else>There are no pending SQL operations.</p>
      </q-card-section>

      <q-card-actions align="right">
        <q-btn flat label="Cancel" color="grey" v-close-popup :disable="isBulkResolving" />
        <q-btn
          flat
          label="Bulk Resolve"
          color="primary"
          :loading="isBulkResolving"
          :disable="loading || !canConfirm || !operations.some(item => item.operation !== 'Skip')"
          @click="emit('confirm')"
        />
      </q-card-actions>
    </q-card>
  </q-dialog>
</template>

<script setup lang="ts">
import { computed } from 'vue'
import type { SqlBulkOperation } from 'api/client'

interface Props {
  modelValue: boolean
  operations: SqlBulkOperation[]
  canConfirm: boolean
  loading: boolean
  error: string
  isBulkResolving?: boolean
}

const props = withDefaults(defineProps<Props>(), {
  isBulkResolving: false
})

const emit = defineEmits<{
  (e: 'update:modelValue', value: boolean): void
  (e: 'confirm'): void
  (e: 'refresh'): void
}>()

const dialogModel = computed({
  get: () => props.modelValue,
  set: (value: boolean) => emit('update:modelValue', value)
})
</script>
