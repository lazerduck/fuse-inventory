<template>
  <q-dialog v-model="dialogModel">
    <q-card style="min-width: 550px; max-width: 700px">
      <q-card-section class="row items-center">
        <q-icon
          :name="result?.success ? 'check_circle' : 'warning'"
          :color="result?.success ? 'positive' : 'orange'"
          size="2em"
          class="q-mr-sm"
        />
        <span class="text-h6">
          {{ result?.success ? 'Bulk Resolve Complete' : 'Bulk Resolve Completed with Issues' }}
        </span>
      </q-card-section>

      <q-card-section v-if="result">
        <div class="q-mb-md">
          <div class="text-subtitle2 q-mb-sm">Summary</div>
          <div class="row q-gutter-md">
            <div class="col-auto">
              <q-badge color="primary" :label="`${result.summary?.totalProcessed ?? 0} Processed`" />
            </div>
            <div v-if="result.summary?.accountsCreated" class="col-auto">
              <q-badge color="positive" :label="`${result.summary.accountsCreated} Created`" />
            </div>
            <div v-if="result.summary?.driftsResolved" class="col-auto">
              <q-badge color="info" :label="`${result.summary.driftsResolved} Resolved`" />
            </div>
            <div v-if="result.summary?.skipped" class="col-auto">
              <q-badge color="orange" :label="`${result.summary.skipped} Skipped`" />
            </div>
            <div v-if="result.summary?.failed" class="col-auto">
              <q-badge color="negative" :label="`${result.summary.failed} Failed`" />
            </div>
          </div>
        </div>

        <div v-if="result.results?.length" class="q-mb-md">
          <div class="text-subtitle2 q-mb-sm">Details</div>
          <q-table
            flat
            bordered
            dense
            :rows="result.results"
            :columns="columns"
            row-key="accountId"
            :pagination="{ rowsPerPage: 10 }"
          >
            <template #body-cell-status="props">
              <q-td :props="props">
                <q-icon
                  :name="props.row.success ? 'check_circle' : (props.row.errorMessage?.includes('skipped') ? 'warning' : 'error')"
                  :color="props.row.success ? 'positive' : (props.row.errorMessage?.includes('skipped') ? 'orange' : 'negative')"
                  size="sm"
                />
              </q-td>
            </template>
            <template #body-cell-message="props">
              <q-td :props="props">
                <span v-if="props.row.success" class="text-positive">Success</span>
                <span v-else :class="props.row.errorMessage?.includes('skipped') ? 'text-orange' : 'text-negative'">
                  {{ props.row.errorMessage ?? 'Failed' }}
                </span>
              </q-td>
            </template>
          </q-table>
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
import type { QTableColumn } from 'quasar'
import type { BulkResolveAccountResult, BulkResolveResponse } from '../../../api/client'

interface Props {
  modelValue: boolean
  result?: BulkResolveResponse | null
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

const columns: QTableColumn<BulkResolveAccountResult>[] = [
  { name: 'status', label: '', field: (row) => row.accountId, align: 'center', style: 'width: 40px' },
  { name: 'accountName', label: 'Account', field: 'accountName', align: 'left' },
  { name: 'principalName', label: 'Principal', field: 'principalName', align: 'left' },
  { name: 'operationType', label: 'Operation', field: 'operationType', align: 'left' },
  { name: 'message', label: 'Result', field: (row) => row.accountId, align: 'left' }
]
</script>
