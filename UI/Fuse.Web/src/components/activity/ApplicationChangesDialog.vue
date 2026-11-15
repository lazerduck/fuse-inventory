<template>
  <q-dialog :model-value="true" @update:model-value="emit('close')">
    <q-card class="history-dialog" aria-labelledby="history-title">
      <q-card-section class="row items-start no-wrap">
        <div class="col">
          <div class="text-caption">Application activity · Version {{ entry.version }}</div>
          <h2 id="history-title" class="text-h6 q-my-none">{{ result?.applicationName || 'Application changes' }}</h2>
          <div class="text-caption">{{ entry.userName || 'System' }} · {{ entry.timestamp?.toLocaleString() }}</div>
        </div>
        <q-btn flat round dense icon="close" aria-label="Close changes" @click="emit('close')" />
      </q-card-section>
      <q-separator />
      <q-card-section v-if="loading" role="status"><q-spinner class="q-mr-sm" />Loading changes…</q-card-section>
      <q-card-section v-else-if="error" role="alert">{{ error }}
        <q-btn flat dense color="primary" label="Retry" @click="load" />
      </q-card-section>
      <q-card-section v-else-if="result?.notice || !result?.groups?.length">
        {{ result?.notice || 'No application fields or relationships changed.' }}
      </q-card-section>
      <q-card-section v-for="group in result?.groups" :key="group.instanceId || 'application'" class="change-group">
        <h3 class="text-subtitle1 text-weight-medium q-my-none">{{ group.title }}</h3>
        <div v-if="group.instanceId" class="text-caption">Instance ID {{ group.instanceId }}</div>
        <table>
          <thead><tr><th>Field</th><th>Before</th><th>After</th></tr></thead>
          <tbody>
            <tr v-for="(change, index) in group.changes" :key="index">
              <th scope="row">{{ change.field }}</th>
              <td>{{ change.before ?? '—' }}</td>
              <td>{{ change.after ?? '—' }}</td>
            </tr>
          </tbody>
        </table>
      </q-card-section>
    </q-card>
  </q-dialog>
</template>

<script setup lang="ts">
import { onBeforeUnmount, onMounted, ref } from 'vue'
import type { ActivityFeedItem, ApplicationChanges } from 'api/client'
import { useFuseClient } from '../../composables/useFuseClient'
import { getErrorMessage } from '../../utils/error'

const props = defineProps<{ entry: ActivityFeedItem }>()
const emit = defineEmits<{ close: [] }>()
const client = useFuseClient()
const request = new AbortController()
const result = ref<ApplicationChanges>()
const error = ref('')
const loading = ref(true)

async function load() {
  if (!props.entry.versionId) return
  loading.value = true
  error.value = ''
  try {
    result.value = await client.applicationChanges(props.entry.versionId, request.signal)
  } catch (err) {
    if (!request.signal.aborted) error.value = getErrorMessage(err, 'Unable to load this application change.')
  } finally {
    loading.value = false
  }
}
onMounted(load)
onBeforeUnmount(() => request.abort())
</script>

<style scoped>
.history-dialog { width: 760px; max-width: 95vw; }
.change-group + .change-group { border-top: 1px solid var(--fuse-panel-border); }
table { width: 100%; table-layout: fixed; border-collapse: collapse; margin-top: 8px; }
th, td { text-align: start; vertical-align: top; padding: 6px 8px; overflow-wrap: anywhere; white-space: pre-wrap; }
th { font-weight: 500; }
thead { background: var(--fuse-accent-soft); }
th:first-child { width: 24%; }
tbody tr + tr { border-top: 1px solid var(--fuse-panel-border); }
</style>
