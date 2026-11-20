<template>
  <div v-if="canRepair" class="q-my-md">
    <q-btn outline no-caps label="Preview table grant" icon="rule" :loading="busy" @click="preview" />
    <q-dialog v-model="open" :persistent="busy">
      <q-card style="width: 32rem; max-width: 95vw">
        <q-card-section>
          <h3 class="text-h6 q-ma-none">PostgreSQL table grant</h3>
          <template v-if="plan">
            <p class="q-mt-md"><strong>{{ plan.principal }}</strong> · {{ plan.database }} · {{ plan.schema }}.{{ plan.table }}</p>
            <p v-if="plan.status === 'already-satisfied'">The account already has {{ plan.privilege }} access.</p>
            <p v-else-if="plan.applied">Fuse verified the {{ plan.privilege }} grant.</p>
            <p v-else>Grant {{ plan.privilege }} directly to this role on the selected table.</p>
            <p v-if="plan.applications.length">Affected applications: {{ plan.applications.map(a => a.name).join(", ") }}</p>
          </template>
          <p v-if="error" class="text-negative" role="alert">{{ error }}</p>
        </q-card-section>
        <q-card-actions align="right">
          <q-btn v-close-popup flat label="Close" :disable="busy" />
          <q-btn v-if="plan?.token" color="primary" label="Apply grant" :loading="busy" @click="apply" />
        </q-card-actions>
      </q-card>
    </q-dialog>
  </div>
</template>
<script setup lang="ts">
import { computed, onBeforeUnmount, ref, watch } from 'vue'
import { useAuthToken } from '../../composables/useAuthToken'
import { useFuseStore } from '../../stores/FuseStore'

const props = defineProps<{ integrationId: string; reviewId: string }>()
const emit = defineEmits<{ applied: [] }>()
interface Plan {
  principal: string; database: string; schema: string; table: string; privilege: string
  changeRequired: boolean; applied: boolean; status: string; token?: string
  applications: { name: string }[]
}
const store = useFuseStore()
const auth = useAuthToken()
const canRepair = computed(() => auth.getToken() && store.hasPermission('postgresqlintegrations:grants:apply'))
const open = ref(false), busy = ref(false), error = ref('')
const plan = ref<Plan>()
let pending: AbortController | undefined
watch(() => [props.integrationId, props.reviewId], () => { pending?.abort(); plan.value = undefined; open.value = false })
onBeforeUnmount(() => pending?.abort())
async function run(action: 'preview' | 'apply', token?: string) {
  pending?.abort()
  const controller = new AbortController()
  pending = controller
  busy.value = true
  error.value = ''
  try {
    const response = await fetch(`${import.meta.env.VITE_API_BASE_URL ?? ''}/api/PostgreSqlIntegration/${props.integrationId}/grant-repair/${action}`, {
      method: 'POST', headers: { 'Content-Type': 'application/json', Authorization: 'Bearer ' + auth.getToken() },
      body: JSON.stringify({ reviewId: props.reviewId, token }), signal: controller.signal
    })
    const data = await response.json().catch(() => null)
    if (!response.ok) throw new Error(data?.error ?? 'Unable to review the grant.')
    if (controller.signal.aborted) return
    plan.value = data
    if (action === 'apply') emit('applied')
  } catch (cause) {
    if (!controller.signal.aborted) { plan.value = undefined; error.value = cause instanceof Error ? cause.message : 'Grant request failed.' }
  } finally {
    if (pending === controller) busy.value = false
  }
}
function preview() { plan.value = undefined; open.value = true; void run('preview') }
function apply() { if (plan.value?.token) void run('apply', plan.value.token) }
</script>
