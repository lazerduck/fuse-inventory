<template>
  <q-card class="content-card q-mb-md">
    <q-card-section class="dialog-header">
      <div>
        <div class="text-h6">Risks</div>
        <div class="text-caption text-grey-7">
          Track risks associated with this {{ targetType }}.
        </div>
      </div>
      <q-btn
        color="primary"
        label="Add Risk"
        dense
        icon="add"
        :disable="disableActions"
        @click="openRiskDialog()"
      />
    </q-card-section>
    <q-separator />
    <q-table
      flat
      bordered
      dense
      :rows="risks"
      :columns="columns"
      row-key="id"
      :loading="loading"
    >
      <template #body-cell-impact="props">
        <q-td :props="props">
          <q-badge
            :color="getImpactColor(props.row.impact)"
            :label="props.row.impact"
          />
        </q-td>
      </template>
      <template #body-cell-likelihood="props">
        <q-td :props="props">
          <q-badge
            :color="getLikelihoodColor(props.row.likelihood)"
            :label="props.row.likelihood"
          />
        </q-td>
      </template>
      <template #body-cell-status="props">
        <q-td :props="props">
          <q-badge
            :color="getStatusColor(props.row.status)"
            :label="props.row.status"
          />
        </q-td>
      </template>
      <template #body-cell-owner="props">
        <q-td :props="props">
          {{ positionLookup[props.row.ownerPositionId] ?? '—' }}
        </q-td>
      </template>
      <template #body-cell-actions="props">
        <q-td :props="props" class="text-right">
          <q-btn
            dense
            flat
            round
            icon="edit"
            color="primary"
            :disable="disableActions"
            @click="openRiskDialog(props.row)"
          />
          <q-btn
            dense
            flat
            round
            icon="delete"
            color="negative"
            class="q-ml-xs"
            :disable="disableActions"
            @click="confirmDelete(props.row)"
          />
        </q-td>
      </template>
      <template #no-data>
        <div class="q-pa-sm text-grey-7">No risks defined.</div>
      </template>
    </q-table>

    <RiskDialog
      v-if="riskDialog"
      :risk="selectedRisk"
      :target-type="targetType"
      :target-id="targetId"
      :positions="positions"
      @close="riskDialog = false"
      @save="handleSaveRisk"
    />
  </q-card>
</template>

<script setup lang="ts">
import { ref, computed } from 'vue'
import { useQuasar, type QTableColumn } from 'quasar'
import type { Risk, Position } from '../../api/client'
import RiskDialog from './RiskDialog.vue'

interface Props {
  risks: readonly Risk[]
  targetType: string
  targetId: string
  positions: readonly Position[]
  loading?: boolean
  disableActions?: boolean
}

const props = withDefaults(defineProps<Props>(), {
  loading: false,
  disableActions: false
})

const emit = defineEmits<{
  (e: 'save', risk: Partial<Risk>): void
  (e: 'delete', riskId: string): void
}>()

const $q = useQuasar()
const riskDialog = ref(false)
const selectedRisk = ref<Risk | null>(null)

const positionLookup = computed(() => {
  const lookup: Record<string, string> = {}
  props.positions.forEach(p => {
    lookup[p.id!] = p.name!
  })
  return lookup
})

const columns: QTableColumn[] = [
  {
    name: 'title',
    label: 'Title',
    field: 'title',
    align: 'left',
    sortable: true
  },
  {
    name: 'impact',
    label: 'Impact',
    field: 'impact',
    align: 'center',
    sortable: true
  },
  {
    name: 'likelihood',
    label: 'Likelihood',
    field: 'likelihood',
    align: 'center',
    sortable: true
  },
  {
    name: 'status',
    label: 'Status',
    field: 'status',
    align: 'center',
    sortable: true
  },
  {
    name: 'owner',
    label: 'Owner',
    field: 'ownerPositionId',
    align: 'left',
    sortable: true
  },
  {
    name: 'actions',
    label: 'Actions',
    field: 'actions',
    align: 'right'
  }
]

function getImpactColor(impact: string): string {
  switch (impact) {
    case 'Critical': return 'red-10'
    case 'High': return 'red-7'
    case 'Medium': return 'orange-7'
    case 'Low': return 'green-7'
    default: return 'grey'
  }
}

function getLikelihoodColor(likelihood: string): string {
  switch (likelihood) {
    case 'High': return 'red-7'
    case 'Medium': return 'orange-7'
    case 'Low': return 'green-7'
    default: return 'grey'
  }
}

function getStatusColor(status: string): string {
  switch (status) {
    case 'Identified': return 'orange-7'
    case 'Mitigated': return 'blue-7'
    case 'Accepted': return 'purple-7'
    case 'Closed': return 'green-7'
    default: return 'grey'
  }
}

function openRiskDialog(risk?: Risk) {
  selectedRisk.value = risk ?? null
  riskDialog.value = true
}

function handleSaveRisk(risk: Partial<Risk>) {
  emit('save', risk)
  riskDialog.value = false
}

function confirmDelete(risk: Risk) {
  $q.dialog({
    title: 'Confirm Delete',
    message: `Are you sure you want to delete the risk "${risk.title}"?`,
    cancel: true,
    persistent: true
  }).onOk(() => {
    emit('delete', risk.id!)
  })
}
</script>

<style scoped>
.content-card {
  border-radius: 8px;
}

.dialog-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
}
</style>
