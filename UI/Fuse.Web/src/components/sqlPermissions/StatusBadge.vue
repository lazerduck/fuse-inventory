<template>
  <q-badge :color="color" :label="label" />
</template>

<script setup lang="ts">
import { computed } from 'vue'
import { SyncStatus } from '../../api/client'

interface Props {
  status?: SyncStatus
  labelOverride?: string
}

const props = defineProps<Props>()

const color = computed(() => {
  switch (props.status) {
    case SyncStatus.InSync:
      return 'positive'
    case SyncStatus.DriftDetected:
      return 'warning'
    case SyncStatus.MissingPrincipal:
      return 'orange'
    case SyncStatus.Error:
      return 'negative'
    case SyncStatus.NotApplicable:
      return 'grey'
    default:
      return 'grey'
  }
})

const label = computed(() => {
  if (props.labelOverride) return props.labelOverride
  switch (props.status) {
    case SyncStatus.InSync:
      return 'In Sync'
    case SyncStatus.DriftDetected:
      return 'Drift'
    case SyncStatus.MissingPrincipal:
      return 'Missing Principal'
    case SyncStatus.Error:
      return 'Error'
    case SyncStatus.NotApplicable:
      return 'N/A'
    default:
      return 'Unknown'
  }
})
</script>
