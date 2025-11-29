<template>
  <div>
    <div class="flex justify-between items-center q-mb-md">
      <div class="text-body1">{{ assignments.length }} assignment(s)</div>
      <q-btn
        dense
        flat
        icon="add"
        label="Add Assignment"
        color="primary"
        :disable="disableActions"
        @click="emit('add')"
      />
    </div>

    <q-list v-if="assignments.length" bordered separator class="rounded-borders">
      <q-item v-for="assignment in assignments" :key="assignment.id" class="q-pa-md">
        <q-item-section>
          <q-item-label class="text-weight-medium">
            {{ formatTargetKind(assignment.targetKind) }}
          </q-item-label>
          <q-item-label caption>
            <span class="text-grey-8">Target ID:</span> {{ assignment.targetId ?? '—' }}
          </q-item-label>
          <q-item-label v-if="assignment.role" caption>
            <span class="text-grey-8">Role:</span> {{ assignment.role }}
          </q-item-label>
          <q-item-label v-if="assignment.notes" caption class="q-mt-xs">
            <span class="text-grey-8">Notes:</span> {{ assignment.notes }}
          </q-item-label>
        </q-item-section>
        <q-item-section side>
          <div class="flex q-gutter-xs">
            <q-btn
              flat
              dense
              round
              icon="edit"
              color="primary"
              :disable="disableActions"
              @click="emit('edit', { assignment })"
            />
            <q-btn
              flat
              dense
              round
              icon="delete"
              color="negative"
              :disable="disableActions"
              @click="emit('delete', { assignment })"
            />
          </div>
        </q-item-section>
      </q-item>
    </q-list>

    <div v-else class="text-grey-7 q-pa-md text-center">
      No assignments configured.
    </div>
  </div>
</template>

<script setup lang="ts">
import type { IdentityAssignment, TargetKind } from '../../api/client'

interface Props {
  assignments: IdentityAssignment[]
  disableActions: boolean
}

defineProps<Props>()
const emit = defineEmits<{
  (event: 'add'): void
  (event: 'edit', payload: { assignment: IdentityAssignment }): void
  (event: 'delete', payload: { assignment: IdentityAssignment }): void
}>()

function formatTargetKind(kind: TargetKind | undefined): string {
  switch (kind) {
    case 'Application':
      return 'Application'
    case 'DataStore':
      return 'Data Store'
    case 'External':
      return 'External Resource'
    default:
      return kind ?? '—'
  }
}
</script>

<style scoped>
.rounded-borders {
  border-radius: 8px;
}
</style>
