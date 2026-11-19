<template>
  <q-btn v-if="canBind" flat dense round icon="vpn_key" aria-label="Configure Key Vault password" @click="open">
    <q-tooltip>Key Vault password</q-tooltip>
  </q-btn>
  <q-dialog v-model="visible">
    <q-card class="credential-editor">
      <q-card-section><h2 class="text-h6 q-ma-none">Key Vault password</h2></q-card-section>
      <q-form @submit="save(false)">
        <q-card-section class="q-pt-none q-gutter-md">
          <div v-if="target" class="destination">{{ target.username }} · {{ target.endpoint }} · {{ target.scope }}</div>
          <div>The connector reads this secret when it connects. Saving replaces its stored password.</div>
          <q-select v-if="vaults.length" v-model="providerId" label="Key Vault" :options="vaults" emit-value map-options
            :rules="[required]" @update:model-value="selectVault" />
          <q-input v-else v-model.trim="providerId" label="Key Vault provider ID" :rules="[required]" />
          <q-input v-model.trim="secretName" label="Secret name" list="integration-password-secret-names"
            :rules="[v => /^[a-z0-9-]{1,127}$/i.test(v || '') || 'Use 1–127 letters, digits or hyphens.']" />
          <datalist id="integration-password-secret-names"><option v-for="name in names" :key="name" :value="name" /></datalist>
          <q-input v-model.trim="version" label="Pinned version" hint="Leave blank to follow the latest version."
            :rules="[v => !v || /^[0-9a-f]{32}$/i.test(v) || 'Enter a 32-digit hexadecimal version.']" />
          <div v-if="error" class="text-negative" role="alert">{{ error }}</div>
        </q-card-section>
        <q-card-actions align="right">
          <q-btn v-if="target?.reference" flat label="Detach" color="negative" :disable="busy" @click="save(true)" />
          <q-btn v-close-popup flat label="Cancel" />
          <q-btn type="submit" color="primary" label="Save reference" :loading="busy" :disable="!target" />
        </q-card-actions>
      </q-form>
    </q-card>
  </q-dialog>
</template>

<script setup lang="ts">
import { computed, ref } from 'vue'
import { useQueryClient } from '@tanstack/vue-query'
import { useQuasar } from 'quasar'
import { azureRequest } from '../../api/azureDiscovery'
import { useFuseClient } from '../../composables/useFuseClient'
import { useFuseStore } from '../../stores/FuseStore'

const props = defineProps<{ provider: string; integrationId?: string }>()
const emit = defineEmits<{ saved: [] }>()
interface Target { revision: string; endpoint: string; username: string; scope: string; reference?: {providerId: string; secretName: string; version?: string} }
const store = useFuseStore(), api = useFuseClient(), queries = useQueryClient(), q = useQuasar()
const canBind = computed(() => !!props.integrationId && !!store.currentUser && (store.isAdmin ||
  ['secretproviders:secrets:bindintegration','secretproviders:secrets:reveal',props.provider + 'integrations:update'].every(k => store.userPermissions?.includes(k))))
const path = computed(() => '/api/integration-credentials/' + props.provider + '/' + props.integrationId)
const visible = ref(false), busy = ref(false), error = ref(''), target = ref<Target>()
const vaults = ref<{value:string;label:string}[]>([]), names = ref<string[]>([])
const providerId = ref(''), secretName = ref(''), version = ref('')
const required = (s: string) => !!s || 'Select a Key Vault provider.'
function message(e: unknown) { return (e as {error?: string})?.error || 'Unable to update the credential reference.' }
async function suggestions(id: string) {
  names.value = []
  if (!id || !store.hasPermission('secretproviders:read')) return
  try {
    const entries = await api.secretsAll(id)
    if (id === providerId.value) names.value = entries.flatMap(s => s.name ? [s.name] : [])
  } catch { /* A vault with read-only capability accepts a known secret name. */ }
}
async function selectVault(id: string) { secretName.value = ''; version.value = ''; await suggestions(id) }
async function open() {
  visible.value = true; busy.value = true; error.value = ''; target.value = undefined
  vaults.value = []; names.value = []; providerId.value = ''; secretName.value = ''; version.value = ''
  try {
    const current = await azureRequest<Target>(path.value, undefined, 'GET')
    target.value = current
    providerId.value = current.reference?.providerId ?? ''; secretName.value = current.reference?.secretName ?? ''; version.value = current.reference?.version ?? ''
    if (store.hasPermission('secretproviders:read')) {
      const all = await api.secretProviderAll()
      vaults.value = all.filter(v => v.id && !v.vaultUri?.includes('.azconfig.io') && String(v.capabilities).includes('Read'))
        .map(v => ({value:v.id!, label:v.name || v.id!}))
      await suggestions(providerId.value)
    }
  } catch(e) { error.value = message(e) } finally { busy.value = false }
}
async function save(detach: boolean) {
  if (!target.value || busy.value) return
  busy.value = true; error.value = ''
  try {
    await azureRequest(path.value, {expectedRevision: target.value.revision, reference: detach ? null :
      {providerId: providerId.value, secretName: secretName.value, version: version.value || null}}, 'PUT')
    visible.value = false
    emit('saved'); void queries.invalidateQueries({queryKey:['redisIntegrations']})
    q.notify({type:'positive', message: detach ? 'Password reference detached.' : 'Password reference saved.'})
  } catch(e) { error.value = message(e) } finally { busy.value = false }
}
</script>

<style scoped>
.credential-editor {width: 34rem; max-width: 94vw}
.destination {overflow-wrap:anywhere; font-weight: 600}
</style>
