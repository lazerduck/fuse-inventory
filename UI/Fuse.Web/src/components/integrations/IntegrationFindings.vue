<template>
  <section v-if="store.hasPermission('integrationreviews:read')" class="review-findings">
    <h2 class="text-h6">Integration findings</h2>
    <p v-if="error" role="alert">{{ error }}</p>
    <q-linear-progress v-else-if="busy" indeterminate aria-label="Loading integration findings" />
    <p v-else-if="!rows.length">No integration reviews are linked here.</p>
    <q-list v-else bordered separator>
      <q-item v-for="{ review } in rows" :key="review?.id" clickable
        :to="{ name:'integrationReviews', query:{ review: review?.id } }">
        <q-item-section><q-item-label>{{ review?.name }}</q-item-label>
          <q-item-label caption>{{ review?.lastAttempt?.evidence?.summary || 'Awaiting first check' }}</q-item-label>
          <q-item-label caption v-if="review?.lastVerified">Last verified {{ review.lastVerified.checkedAt?.toLocaleString() }}</q-item-label>
        </q-item-section>
        <q-item-section side><q-icon name="chevron_right" /></q-item-section>
      </q-item>
    </q-list>
    <template v-if="timeline.length">
      <h3 class="text-subtitle1">Health and dependency timeline</h3>
      <q-list dense><q-item v-for="(event, index) in timeline" :key="index"><q-item-section>
        <q-item-label>{{ event.label }}</q-item-label><q-item-label caption>{{ event.at.toLocaleString() }}</q-item-label>
      </q-item-section></q-item></q-list>
    </template>
    <p v-if="healthError" role="status">{{ healthError }}</p>
  </section>
</template>
<script setup lang="ts">
import { computed, onBeforeUnmount, ref, watch } from 'vue'
import type { ReviewView, InstanceHealthTransition } from 'api/client'
import { useFuseStore } from '../../stores/FuseStore'
import { useFuseClient } from '../../composables/useFuseClient'
import { reviewError } from './reviewDisplay'
const props = defineProps<{accountId?: string; applicationId?: string; instanceId?: string; targetId?: string}>()
const client = useFuseClient(), store = useFuseStore()
const rows = ref<ReviewView[]>([]), health = ref<InstanceHealthTransition[]>([])
const busy = ref(false), error = ref(''), healthError = ref('')
let pending: AbortController | undefined
onBeforeUnmount(() => pending?.abort())
watch(() => [props.accountId, props.applicationId, props.instanceId, props.targetId, store.hasPermission('integrationreviews:read'), store.appSettings?.healthCheckProvider], async () => {
  pending?.abort()
  const ctl = new AbortController()
  pending = ctl
  rows.value = []; health.value = []; error.value = ''; healthError.value = ''; busy.value = false
  if (!store.hasPermission('integrationreviews:read') || ![props.accountId,props.applicationId,props.instanceId,props.targetId].some(Boolean)) return
  busy.value = true
  try {
    const result = await client.reviewsList(props.accountId, props.applicationId, props.instanceId, props.targetId, ctl.signal)
    if (!ctl.signal.aborted) rows.value = result
    if (props.instanceId && store.appSettings?.healthCheckProvider && store.appSettings.healthCheckProvider !== 'None') {
      try {
        const transitions = await client.healthMonitoringHistory(props.instanceId, ctl.signal)
        if (!ctl.signal.aborted) health.value = transitions
      } catch (e) { if (!ctl.signal.aborted) healthError.value = reviewError(e) }
    }
  } catch (e) { if (!ctl.signal.aborted) error.value = reviewError(e) }
  finally { if (!ctl.signal.aborted) busy.value = false }
}, { immediate:true })
const timeline = computed(() => {
  if (!props.instanceId) return []
  const events = health.value.filter(e => e.checkedAt).map(e => ({at:e.checkedAt!,label:`${e.provider} health · ${e.previousState} → ${e.state}`}))
  for (const {review} of rows.value) for (const entry of review?.history ?? []) {
    if (entry.checkedAt && entry.evidence) events.push({at:entry.checkedAt, label:`${review?.name} · ${entry.evidence.summary}`})
  }
  return events.sort((a,b) => b.at.getTime()-a.at.getTime()).slice(0,25)
})
</script>
<style scoped>.review-findings { margin: 1.4rem 0; overflow-wrap: anywhere; }.review-findings h2 { margin: 0 0 .65rem; }</style>
