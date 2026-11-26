<template>
  <section v-if="canRead" class="instance-access" aria-label="Instance access">
    <h2>Access</h2>
    <p v-if="error" role="alert" class="request-error">
      {{ error }} <q-btn flat dense no-caps color="primary" label="Retry" @click="load(failedReview)" />
    </p>
    <q-linear-progress v-if="loading" indeterminate aria-label="Loading access" />
    <template v-else-if="rows.length">
      <article v-for="row in rows" :key="[row.dependencyId, row.reviewId].join('/')" class="access-card">
        <header class="access-header">
          <div>
            <div class="provider"><q-icon :name="row.provider === 'sql' ? 'storage' : 'move_to_inbox'" />
              {{ providerNames[row.provider ?? ''] || row.provider }}</div>
            <h3>{{ row.targetName }}</h3>
            <div class="principal">
              {{ row.provider === 'sql' ? 'SQL account' : 'Entra managed identity' }} ·
              <router-link v-if="row.principalId" :to="principalRoute(row)">{{ row.principalName }}</router-link>
              <span v-else>{{ row.principalName }}</span>
            </div>
          </div>
          <span class="access-status" :class="row.status">
            <q-icon :name="statusIcons[row.status ?? ''] || 'help_outline'" />
            {{ statusLabels[row.status ?? ''] || 'Unknown' }}
          </span>
        </header>
        <dl class="comparison">
          <div v-for="column in columns" :key="column.key"><dt>{{ column.label }}</dt>
            <dd>
              <template v-if="row[column.key]?.length">
                <p v-for="(item, index) in row[column.key]?.slice(0, previewCount(row, column.key))" :key="index">{{ item }}</p>
                <details v-if="row[column.key]!.length > previewCount(row, column.key)">
                  <summary>{{ column.key === 'expected' ? 'More expectations' : 'Evidence details' }}
                    ({{ row[column.key]!.length - previewCount(row, column.key) }})</summary>
                  <ul><li v-for="(item, index) in row[column.key]?.slice(previewCount(row, column.key))" :key="index">{{ item }}</li></ul>
                </details>
              </template>
              <span v-else-if="column.key === 'expected' && !row.principalId">Expectation unavailable.</span>
              <span v-else-if="column.key === 'expected' && row.provider === 'servicebus' && !canReadReviews">
                Queue expectations require access to integration reviews.
              </span>
              <span v-else-if="column.key === 'expected'">No expectation documented.</span>
              <span v-else>No observation available.</span>
            </dd>
          </div>
        </dl>
        <footer v-if="row.checkedAt || row.reviewId || canSetUp(row) || canDocumentGrants(row)" class="access-footer">
          <span v-if="row.checkedAt" class="checked">Last checked
            <time :datetime="row.checkedAt.toISOString()">{{ row.checkedAt.toLocaleString() }}</time>
          </span>
          <div class="actions">
            <q-btn v-if="canReadReviews && row.reviewId" flat dense no-caps color="primary" label="View review"
              :to="{ name: 'integrationReviews', query: { review: row.reviewId } }" />
            <q-btn v-if="canDocumentGrants(row)" flat dense no-caps color="primary" label="Document account grants" :to="principalRoute(row)" />
            <q-btn v-if="canSetUp(row)" flat dense no-caps
              color="primary" icon="add" :label="row.provider === 'sql' ? 'Set up grant review' : 'Add queue expectation'"
              :to="newReviewRoute(row)" />
            <q-btn v-if="!readonly && row.reviewId && canRefresh" outline dense no-caps color="primary" icon="refresh"
              label="Check now" :loading="refreshing === row.reviewId" :disable="!!refreshing"
              @click="load(row.reviewId)" />
          </div>
        </footer>
      </article>
    </template>
    <div v-else-if="!error" class="empty">
      <p>No SQL account or Service Bus managed identity dependencies are visible for this instance.</p>
      <q-btn v-if="!readonly" flat no-caps color="primary" label="View dependencies" @click="$emit('show-dependencies')" />
    </div>
  </section>
</template>

<script setup lang="ts">
import { computed, onBeforeUnmount, ref, watch } from 'vue'
import { useFuseClient } from '../../composables/useFuseClient'
import { useFuseStore } from '../../stores/FuseStore'
import { providerNames, reviewError } from '../integrations/reviewDisplay'

const props = defineProps<{ applicationId: string; instanceId: string; readonly?: boolean }>()
defineEmits<{ 'show-dependencies': [] }>()
const client = useFuseClient()
const store = useFuseStore()
type Row = NonNullable<Awaited<ReturnType<typeof client.instanceAccessGet>>['rows']>[number]
const rows = ref<Row[]>([])
const loading = ref(false)
const refreshing = ref<string>()
const failedReview = ref<string>()
const error = ref('')
const canRead = computed(() => store.hasPermission('application:read'))
const canReadReviews = computed(() => store.hasPermission('integrationreviews:read'))
const canManageReviews = computed(() => canReadReviews.value && store.hasPermission('integrationreviews:manage'))
const canRefresh = computed(() => canReadReviews.value && store.hasPermission('integrationreviews:refresh'))
const columns = [{ key: 'expected', label: 'Expected access' }, { key: 'observed', label: 'Observed access' }] as const
const previewCount = (row: Row, column: 'expected' | 'observed') => row.provider === 'sql' && column === 'observed' ? 2 : 1
const canSetUp = (row: Row) => !props.readonly && canManageReviews.value && row.principalId &&
  row.canConfigureReview && (!row.reviewId || row.provider === 'servicebus')
const canDocumentGrants = (row: Row) => !props.readonly && row.principalId && row.provider === 'sql' &&
  !row.expected?.length && store.hasPermission('accounts:update')
const statusLabels: Record<string, string> = {
  Matched: 'Matches expectation', Drift: 'Access differs', Unknown: 'Unknown coverage',
  Unchecked: 'Unchecked', Undocumented: 'Undocumented'
}
const statusIcons: Record<string, string> = {
  Matched: 'check_circle_outline', Drift: 'difference', Unknown: 'help_outline',
  Unchecked: 'schedule', Undocumented: 'description'
}

function principalRoute(row: Row) {
  const name = row.provider === 'sql'
    ? (props.readonly ? 'viewAccount' : 'accountEdit')
    : (props.readonly ? 'viewIdentity' : 'identityEdit')
  return { name, params: { id: row.principalId } }
}
function newReviewRoute(row: Row) {
  return { name: 'integrationReviews', query: {
    newReview: '1', provider: row.provider, integration: row.integrationId, target: row.targetId,
    principal: row.principalId, instance: props.instanceId
  } }
}

let pending: AbortController | undefined
async function load(reviewId?: string) {
  pending?.abort()
  const controller = new AbortController()
  pending = controller
  const { signal } = controller
  error.value = ''
  failedReview.value = undefined
  loading.value = !reviewId
  refreshing.value = reviewId
  let retryReview = reviewId
  try {
    if (reviewId) {
      await client.reviewsRefresh(reviewId, signal)
      retryReview = undefined
    }
    if (signal.aborted) return
    const result = await client.instanceAccessGet(props.applicationId, props.instanceId, signal)
    if (!signal.aborted) rows.value = result.rows ?? []
  } catch (e) {
    if (!signal.aborted) {
      error.value = reviewError(e)
      failedReview.value = retryReview
    }
  } finally {
    if (!signal.aborted) {
      loading.value = false
      refreshing.value = undefined
    }
  }
}
watch(() => [props.applicationId, props.instanceId, canRead.value, store.currentUser,
  store.userPermissions, store.posturePermissions], () => {
  pending?.abort()
  rows.value = []
  error.value = ''
  loading.value = false
  refreshing.value = undefined
  if (canRead.value && props.applicationId && props.instanceId) void load()
}, { immediate: true, deep: true })
onBeforeUnmount(() => pending?.abort())
</script>

<style scoped>
.instance-access { color: var(--fuse-text); }
h2, h3, p, dl, dd, ul { margin: 0; }
h2 { font-size: 1.35rem; font-weight: 600; line-height: 1.4; margin-bottom: 1rem; }
h3 { font-size: 1.15rem; font-weight: 600; line-height: 1.4; }
.access-card, .empty { border: 1px solid var(--fuse-panel-border); border-radius: 8px; background: var(--fuse-card-bg); }
.access-card { overflow-wrap: anywhere; }
.access-card + .access-card { margin-top: 1rem; }
.access-header, .access-footer { display: flex; align-items: flex-start; justify-content: space-between; flex-wrap: wrap; gap: .75rem; }
.access-header { padding: 1rem 1.25rem; }
.provider, .checked { color: var(--fuse-text-muted); font-size: .83rem; }
.provider, .access-status { display: inline-flex; align-items: center; gap: .35rem; }
.principal { margin-top: .2rem; }
a { color: var(--q-primary); }
.access-status { padding: .25rem .65rem; border-radius: 1rem; font-size: .82rem; font-weight: 600; background: var(--fuse-status-na-bg); }
.access-status.Matched { background: var(--fuse-status-in-sync-bg); }
.access-status.Drift { background: var(--fuse-status-drift-bg); }
.comparison { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); border-top: 1px solid var(--fuse-panel-border); }
.comparison > div { padding: .85rem 1.25rem; }
.comparison > div + div { border-left: 1px solid var(--fuse-panel-border); }
dt { color: var(--fuse-text-muted); font-size: .82rem; font-weight: 600; margin-bottom: .25rem; }
dd p + p, details { margin-top: .3rem; }
summary { cursor: pointer; color: var(--q-primary); width: fit-content; }
ul { padding-left: 1.1em; margin-top: .3rem; }
.access-footer { align-items: center; border-top: 1px solid var(--fuse-panel-border); padding: .55rem 1.25rem; }
.actions { display: flex; flex-wrap: wrap; align-items: center; gap: .5rem; margin-left: auto; }
.empty { padding: 1rem 1.25rem; }
.empty .q-btn { margin-top: .5rem; }
.request-error { margin-bottom: .75rem; }
@media (max-width: 600px) {
  .comparison { grid-template-columns: minmax(0, 1fr); }
  .comparison > div + div { border: 0; padding-top: 0; }
  .actions { margin-left: 0; }
}
</style>
