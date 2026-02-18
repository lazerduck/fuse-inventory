<template>
  <q-dialog :model-value="true" persistent @update:model-value="handleClose">
    <q-card class="form-dialog">
      <q-card-section class="dialog-header">
        <div class="text-h6">{{ dialogTitle }}</div>
        <q-btn flat round dense icon="close" @click="handleClose" />
      </q-card-section>
      <q-separator />
      <q-form @submit.prevent="handleSubmit">
        <q-card-section>
          <div class="form-grid">
            <q-input
              v-model="form.title"
              label="Title *"
              dense
              outlined
              :rules="[val => !!val || 'Title is required']"
            />
            
            <q-input
              v-model="form.description"
              label="Description"
              type="textarea"
              dense
              outlined
              rows="3"
            />

            <q-select
              v-model="form.impact"
              label="Impact *"
              dense
              outlined
              emit-value
              map-options
              :options="impactOptions"
              :rules="[val => !!val || 'Impact is required']"
            />

            <q-select
              v-model="form.likelihood"
              label="Likelihood *"
              dense
              outlined
              emit-value
              map-options
              :options="likelihoodOptions"
              :rules="[val => !!val || 'Likelihood is required']"
            />

            <q-select
              v-model="form.status"
              label="Status *"
              dense
              outlined
              emit-value
              map-options
              :options="statusOptions"
              :rules="[val => !!val || 'Status is required']"
            />

            <q-select
              v-model="form.ownerPositionId"
              label="Owner Position *"
              dense
              outlined
              emit-value
              map-options
              :options="positionOptions"
              :rules="[val => !!val || 'Owner position is required']"
            />

            <q-select
              v-model="form.approverPositionId"
              label="Approver Position (Optional)"
              dense
              outlined
              emit-value
              map-options
              clearable
              :options="positionOptions"
            />

            <q-input
              v-model="form.mitigation"
              label="Mitigation"
              type="textarea"
              dense
              outlined
              rows="2"
            />

            <q-input
              v-model="form.notes"
              label="Notes"
              type="textarea"
              dense
              outlined
              rows="2"
            />

            <q-input
              v-model="form.reviewDate"
              label="Review Date"
              type="date"
              dense
              outlined
            />

            <q-input
              v-model="form.approvalDate"
              label="Approval Date"
              type="date"
              dense
              outlined
            />
          </div>
        </q-card-section>
        <q-separator />
        <q-card-actions align="right">
          <q-btn flat label="Cancel" @click="handleClose" />
          <q-btn
            color="primary"
            type="submit"
            :label="isEditing ? 'Save' : 'Create'"
          />
        </q-card-actions>
      </q-form>
    </q-card>
  </q-dialog>
</template>

<script setup lang="ts">
import { computed, reactive, watch } from 'vue'
import type { Risk, Position, RiskImpact, RiskLikelihood, RiskStatus } from '../../api/client'

interface Props {
  risk: Risk | null
  targetType: string
  targetId: string
  positions: readonly Position[]
}

interface RiskForm {
  title: string
  description: string
  impact: RiskImpact | null
  likelihood: RiskLikelihood | null
  status: RiskStatus | null
  ownerPositionId: string | null
  approverPositionId: string | null
  mitigation: string
  notes: string
  reviewDate: string
  approvalDate: string
}

const props = defineProps<Props>()
const emit = defineEmits<{
  (e: 'close'): void
  (e: 'save', risk: Partial<Risk>): void
}>()

const form = reactive<RiskForm>({
  title: '',
  description: '',
  impact: null,
  likelihood: null,
  status: null,
  ownerPositionId: null,
  approverPositionId: null,
  mitigation: '',
  notes: '',
  reviewDate: '',
  approvalDate: ''
})

const dialogTitle = computed(() => props.risk ? 'Edit Risk' : 'Add Risk')
const isEditing = computed(() => !!props.risk)

const impactOptions = [
  { label: 'Low', value: 'Low' },
  { label: 'Medium', value: 'Medium' },
  { label: 'High', value: 'High' },
  { label: 'Critical', value: 'Critical' }
]

const likelihoodOptions = [
  { label: 'Low', value: 'Low' },
  { label: 'Medium', value: 'Medium' },
  { label: 'High', value: 'High' }
]

const statusOptions = [
  { label: 'Identified', value: 'Identified' },
  { label: 'Mitigated', value: 'Mitigated' },
  { label: 'Accepted', value: 'Accepted' },
  { label: 'Closed', value: 'Closed' }
]

const positionOptions = computed(() =>
  props.positions.map(p => ({
    label: p.name!,
    value: p.id!
  }))
)

watch(() => props.risk, (risk) => {
  if (risk) {
    form.title = risk.title ?? ''
    form.description = risk.description ?? ''
    form.impact = (risk.impact as any) ?? null
    form.likelihood = (risk.likelihood as any) ?? null
    form.status = (risk.status as any) ?? null
    form.ownerPositionId = risk.ownerPositionId ?? null
    form.approverPositionId = risk.approverPositionId ?? null
    form.mitigation = risk.mitigation ?? ''
    form.notes = risk.notes ?? ''
    const reviewDate = risk.reviewDate ? String(risk.reviewDate).split('T')[0] : ''
    const approvalDate = risk.approvalDate ? String(risk.approvalDate).split('T')[0] : ''
    form.reviewDate = reviewDate || ''
    form.approvalDate = approvalDate || ''
  } else {
    // Reset form for new risk
    form.title = ''
    form.description = ''
    form.impact = null
    form.likelihood = null
    form.status = 'Identified' as any
    form.ownerPositionId = null
    form.approverPositionId = null
    form.mitigation = ''
    form.notes = ''
    form.reviewDate = ''
    form.approvalDate = ''
  }
}, { immediate: true })

function handleClose() {
  emit('close')
}

function handleSubmit() {
  const riskData: Partial<Risk> = {
    title: form.title,
    description: form.description || undefined,
    impact: form.impact!,
    likelihood: form.likelihood!,
    status: form.status!,
    ownerPositionId: form.ownerPositionId!,
    approverPositionId: form.approverPositionId || undefined,
    targetType: props.targetType,
    targetId: props.targetId,
    mitigation: form.mitigation || undefined,
    notes: form.notes || undefined,
    reviewDate: form.reviewDate ? new Date(form.reviewDate) : undefined,
    approvalDate: form.approvalDate ? new Date(form.approvalDate) : undefined,
    tagIds: []
  }
  
  if (props.risk) {
    riskData.id = props.risk.id
  }
  
  emit('save', riskData)
}
</script>

<style scoped>
.form-dialog {
  width: 700px;
  max-width: 90vw;
}

.dialog-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
}

.form-grid {
  display: grid;
  gap: 1rem;
  grid-template-columns: 1fr;
}
</style>
