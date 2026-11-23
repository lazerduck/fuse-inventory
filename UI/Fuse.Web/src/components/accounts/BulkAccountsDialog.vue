<template>
  <q-dialog :model-value="modelValue" persistent maximized>
    <q-card class="q-pa-lg">
      <div class="row items-center justify-between q-mb-md">
        <h2 class="text-h5 q-ma-none">{{ grantsOnly ? 'Assign grants' : source ? 'Clone accounts' : 'Create multiple accounts' }}</h2>
        <q-btn flat icon="close" aria-label="Close bulk editor" :disable="busy" @click="close" />
      </div>
      <p>Save account records and documented grants in Fuse. SQL provisioning is a separate action.</p>
      <q-banner v-if="error" class="bg-red-1 text-negative q-mb-md">{{ error }}</q-banner>
      <div :inert="busy || undefined">
      <template v-if="!review">
        <template v-if="!grantsOnly">
          <p v-if="source">Starts from the saved account. Review every target and secret reference; copied references point to the same secret.</p>
          <p v-if="source && !targets.length">{{ cloneTargets.isLoading.value ? 'Loading compatible targets…' : cloneTargets.error.value ? 'Unable to load clone targets. Close and try again.' : 'No compatible targets are available for this account.' }}</p>
          <q-select v-model="targetIds" :options="targets" label="Targets and environments" multiple use-chips emit-value map-options outlined class="q-mb-md" />
          <q-input v-if="!source" v-model="names" type="textarea" rows="3" outlined label="Usernames — one per line" hint="Paste usernames, or two spreadsheet columns: username and secret reference. One account will be added for each selected target." />
          <div class="row q-gutter-md q-my-md">
            <q-select v-model="auth" :options="Object.values(AuthKind)" label="Default authentication" outlined />
            <q-select v-model="tagIds" :options="tagOptions" label="Tags for new rows" multiple use-chips emit-value map-options outlined />
            <q-btn label="Add rows" color="primary" :disable="!targetIds.length || (!source && !names.trim())" @click="addRows" />
          </div>
          <div v-for="(row, index) in rows" :key="row.id" class="q-pa-md q-mb-md rounded-borders bulk-row">
          <div class="row q-gutter-sm items-start">
            <q-input v-model="row.account.userName" outlined label="Username" class="col" />
            <q-select v-model="row.account.targetId" :options="targets" emit-value map-options outlined label="Target" class="col" @update:model-value="setKind(row)" />
            <q-select v-model="row.account.authKind" :options="Object.values(AuthKind)" outlined label="Authentication" class="col" />
            <q-select v-model="row.account.secretBinding!.kind" :options="Object.values(SecretBindingKind)" outlined label="Secret type" class="col" />
            <q-input v-if="row.account.secretBinding!.kind === SecretBindingKind.PlainReference" v-model="row.account.secretBinding!.plainReference" outlined label="Secret reference" class="col" />
            <template v-if="row.account.secretBinding!.kind === SecretBindingKind.AzureKeyVault">
              <q-select v-model="row.account.secretBinding!.azureKeyVault!.providerId" :options="providerOptions" emit-value map-options outlined label="Secret provider" class="col" />
              <q-input v-model="row.account.secretBinding!.azureKeyVault!.secretName" outlined label="Secret name" class="col" />
              <q-input v-model="row.account.secretBinding!.azureKeyVault!.version" outlined label="Secret version (optional)" class="col" />
            </template>
            <q-btn flat icon="delete" :aria-label="`Remove row ${index + 1}`" @click="rows.splice(index, 1)" />
          </div>
          <q-expansion-item label="Row tags and grants" dense class="q-mt-sm">
            <q-select v-model="row.account.tagIds" :options="tagOptions" label="Row tags" multiple use-chips emit-value map-options outlined />
            <div v-for="(grant, grantIndex) in row.account.grants" :key="grantIndex" class="row q-gutter-sm q-mt-sm">
              <q-input v-model="grant.database" outlined label="Grant database" class="col" />
              <q-input v-model="grant.schema" outlined label="Grant schema" class="col" />
              <q-select v-model="grant.privileges" :options="Object.values(Privilege)" multiple use-chips outlined label="Row privileges" class="col" />
              <q-btn flat icon="delete" :aria-label="`Remove grant ${grantIndex + 1} from row ${index + 1}`" @click="row.account.grants.splice(grantIndex, 1)" />
            </div>
            <q-btn flat label="Add row grant" @click="row.account.grants.push({ database: '', privileges: [] })" />
          </q-expansion-item>
          </div>
          <q-checkbox v-model="skipExisting" label="Skip usernames already present on the target (never overwrite)" />
        </template>
        <template v-else>
          <p>{{ selected.length }} selected accounts. Choose accounts on one target at a time so database names are unambiguous.</p>
          <ul><li v-for="a in selected" :key="a.id">{{ a.userName }} — {{ targetLabel(a.targetId) }}</li></ul>
        </template>
        <q-separator class="q-my-md" />
        <h3 class="text-subtitle1">{{ grantsOnly ? 'Add permissions' : 'Additional grants for every new account' }}</h3>
        <p>Existing privileges are preserved. Database and schema names describe intended permissions; this does not check or change the live database.</p>
        <div class="row q-gutter-md">
          <q-input v-model="databases" outlined label="Databases (one per line)" type="textarea" rows="2" class="col" />
          <q-input v-model="schema" outlined label="Schema (optional)" class="col" />
          <q-select v-model="privileges" :options="Object.values(Privilege)" multiple use-chips outlined label="Privileges" class="col" />
        </div>
        <div class="q-mt-lg row q-gutter-sm">
          <q-btn flat label="Cancel" :disable="busy" @click="close" />
          <q-btn color="primary" label="Review changes" :disable="!canSubmit" :loading="busy" @click="submit(true)" />
        </div>
      </template>
      <template v-else>
        <h3 class="text-subtitle1">{{ result?.applied ? 'Results — saved in Fuse' : 'Review changes' }}</h3>
        <q-markup-table flat bordered wrap-cells>
          <thead><tr><th>Account / target</th><th>Details</th><th>Status</th></tr></thead>
          <tbody><tr v-for="item in result?.results" :key="item.id">
            <td>{{ rowLabel(item.id) }}</td><td class="text-left" style="white-space: pre-wrap">{{ detail(item.id) }}</td>
            <td>{{ item.status }}<div class="text-negative">{{ item.error }}</div></td>
          </tr></tbody>
        </q-markup-table>
        <p v-if="blocked">Nothing has been saved. Correct the blocked rows and review again.</p>
        <p v-else-if="!result?.applied">Only the changes shown above will be saved. No SQL operations will run.</p>
        <div class="row q-gutter-sm q-mt-lg">
          <q-btn v-if="!result?.applied" flat label="Back to editing" :disable="busy" @click="review = false" />
          <q-btn v-if="!result?.applied" color="primary" :label="grantsOnly ? 'Assign grants' : 'Create accounts'" :disable="blocked || !canSubmit" :loading="busy" @click="submit(false)" />
          <q-btn v-else color="primary" label="Done" @click="close" />
        </div>
      </template>
      </div>
      <q-inner-loading :showing="busy" label="Checking and saving the batch…" />
    </q-card>
  </q-dialog>
</template>
<script setup lang="ts">
import { uid } from 'quasar'
import { computed, ref, watch } from 'vue'
import { useQuery, useQueryClient } from '@tanstack/vue-query'
import { AuthKind, SecretBindingKind, Privilege, TargetKind, type Account, type IGrant } from 'api/client'
import { useFuseClient } from '../../composables/useFuseClient'
import { useApplications } from '../../composables/useApplications'
import { useDataStores } from '../../composables/useDataStores'
import { useMessageBrokers } from '../../composables/useMessageBrokers'
import { useExternalResources } from '../../composables/useExternalResources'
import { useEnvironments } from '../../composables/useEnvironments'
import { useSecretProviders } from '../../composables/useSecretProviders'
import { useTags } from '../../composables/useTags'
import { useDialogDraft } from '../../composables/useDialogDraft'
import { useBulkAccounts, type BulkRow, type BulkResponse } from '../../composables/useBulkAccounts'
const props = withDefaults(defineProps<{ modelValue: boolean; source?: Account | null; selected?: Account[]; grantsOnly?: boolean; canSubmit?: boolean }>(), { selected: () => [], canSubmit: true })
const emit = defineEmits<{ (e: 'update:modelValue', value: boolean): void }>()
const apps = useApplications(), stores = useDataStores(), brokers = useMessageBrokers(), externals = useExternalResources(), envs = useEnvironments(), providers = useSecretProviders(), tags = useTags()
const api = useBulkAccounts(), cache = useQueryClient()
const client = useFuseClient()
const cloneTargets = useQuery({
  queryKey: computed(() => ['account-clone-targets', props.source?.id]),
  enabled: computed(() => props.modelValue && !!props.source?.id),
  queryFn: () => client.accountCloneTargets(props.source!.id!)
})
const rows = ref<BulkRow[]>([]), names = ref(''), targetIds = ref<string[]>([]), tagIds = ref<string[]>([])
const auth = ref(AuthKind.UserPassword), skipExisting = ref(false), databases = ref(''), schema = ref(''), privileges = ref<Privilege[]>([])
const review = ref(false), busy = ref(false), error = ref(''), result = ref<BulkResponse>()
const targets = computed(() => {
  const env = (id?: string) => id ? envs.lookup.value[id] ?? id : 'No environment'
  const all = [
    ...(apps.data.value ?? []).flatMap(a => (a.instances ?? []).map(i => ({ value: i.id!, label: `${a.name} — ${env(i.environmentId)}`, kind: TargetKind.Application }))),
    ...(stores.data.value ?? []).map(a => ({ value: a.id!, label: `${a.name} — ${env(a.environmentId)}`, kind: TargetKind.DataStore })),
    ...(brokers.data.value ?? []).map(a => ({ value: a.id!, label: `${a.name} — ${env(a.environmentId)}`, kind: TargetKind.MessageBroker })),
    ...(externals.data.value ?? []).map(a => ({ value: a.id!, label: a.name ?? a.id!, kind: TargetKind.External }))
  ]
  return props.source ? all.filter(t => (cloneTargets.data.value ?? []).some(c => c.id === t.value)) : all
})
const tagOptions = computed(() => Object.entries(tags.tagInfoLookup.value).map(([value, t]) => ({ value, label: t.name })))
const providerOptions = computed(() => (providers.data.value ?? []).map(p => ({ value: p.id!, label: p.name })))
function targetLabel(id?: string) { return targets.value.find(t => t.value === id)?.label ?? id ?? '' }
function setKind(row: BulkRow) { row.account.targetKind = targets.value.find(t => t.value === row.account.targetId)?.kind }
const draft = useDialogDraft(() => ({ rows: rows.value, names: names.value, targetIds: targetIds.value, tagIds: tagIds.value, auth: auth.value, skipExisting: skipExisting.value, databases: databases.value, schema: schema.value, privileges: privileges.value }), () => props.modelValue)
watch(() => props.modelValue, open => {
  if (!open) return
  rows.value = []; names.value = ''; targetIds.value = []; tagIds.value = [...(props.source?.tagIds ?? [])]; auth.value = props.source?.authKind ?? AuthKind.UserPassword
  skipExisting.value = false; databases.value = ''; schema.value = ''; privileges.value = []; review.value = false; result.value = undefined; error.value = ''
})
function close() { if (!busy.value) void draft.discard(() => emit('update:modelValue', false)) }
function addRows() {
  const entries = props.source ? [[props.source.userName ?? '', '']] : names.value.split(/\r?\n/).filter(x => x.trim()).map(x => x.split('\t'))
  if (rows.value.length + entries.length * targetIds.value.length > 100) { error.value = 'A batch can contain at most 100 accounts.'; return }
  for (const targetId of targetIds.value) for (const [userName, reference] of entries) {
    const binding = props.source?.secretBinding ? JSON.parse(JSON.stringify(props.source.secretBinding)) : { kind: reference ? SecretBindingKind.PlainReference : SecretBindingKind.None, plainReference: reference ?? '' }
    binding.azureKeyVault ??= { providerId: '', secretName: '' }
    rows.value.push({ id: uid(), account: { targetId, targetKind: targets.value.find(t => t.value === targetId)!.kind, userName: userName?.trim(), authKind: auth.value, secretBinding: binding, tagIds: [...tagIds.value], parameters: props.source?.parameters ? { ...props.source.parameters } : undefined, grants: JSON.parse(JSON.stringify(props.source?.grants ?? [])) } })
  }
  error.value = ''
}
function grants(): IGrant[] { return [...new Set(databases.value.split(/\r?\n/).map(x => x.trim()).filter(Boolean))].map(database => ({ database, schema: schema.value.trim() || undefined, privileges: [...privileges.value] })) }
function payloadRows() {
  return rows.value.map(row => {
    const merged: IGrant[] = JSON.parse(JSON.stringify(row.account.grants ?? []))
    for (const g of grants()) {
      const found = merged.find(x => (x.database ?? '').trim().toLowerCase() === g.database!.toLowerCase() && (x.schema ?? '').trim().toLowerCase() === (g.schema ?? '').toLowerCase())
      if (found) found.privileges = [...new Set([...(found.privileges ?? []), ...g.privileges!])]; else merged.push(g)
    }
    const b = row.account.secretBinding
    const secretBinding = { kind: b.kind, plainReference: b.kind === SecretBindingKind.PlainReference ? b.plainReference : undefined, azureKeyVault: b.kind === SecretBindingKind.AzureKeyVault ? { ...b.azureKeyVault, providerId: b.azureKeyVault?.providerId || '00000000-0000-0000-0000-000000000000' } : undefined }
    return { ...row, account: { ...row.account, grants: merged, secretBinding } }
  })
}
const blocked = computed(() => result.value?.results.some(r => r.status === 'Blocked') ?? true)
function rowLabel(id: string) { const a = props.grantsOnly ? props.selected.find(x => x.id === id) : rows.value.find(x => x.id === id)?.account; return a ? `${a.userName} — ${targetLabel(a.targetId)}` : 'Batch' }
function detail(id: string) {
  const row = payloadRows().find(r => r.id === id)?.account
  const gs = props.grantsOnly ? grants() : row?.grants ?? []
  const grantText = gs.map(g => `${g.database || '(all databases)'}${g.schema ? '.' + g.schema : ''}: ${(g.privileges ?? []).join(', ')}`).join('\n') || 'No grants'
  if (!row) return grantText
  const b = row.secretBinding
  const secret = b?.kind === SecretBindingKind.AzureKeyVault ? `${providerOptions.value.find(p => p.value === b.azureKeyVault?.providerId)?.label ?? b.azureKeyVault?.providerId} / ${b.azureKeyVault?.secretName}${b.azureKeyVault?.version ? ' @ ' + b.azureKeyVault.version : ''}` : b?.plainReference ?? 'None'
  return `${row.authKind}; secret: ${secret}\nTags: ${(row.tagIds ?? []).map(id => tags.tagInfoLookup.value[id]?.name ?? id).join(', ') || 'None'}\n${grantText}`
}
async function submit(preview: boolean) {
  error.value = ''
  if (!props.canSubmit) { error.value = 'You no longer have permission for this operation.'; return }
  if (props.grantsOnly && new Set(props.selected.map(a => `${a.targetKind}:${a.targetId}`)).size !== 1) { error.value = 'Select accounts on one target for this grant batch.'; return }
  busy.value = true
  try {
    result.value = props.grantsOnly ? await api.grants(props.selected.map(a => a.id!), grants(), preview) : await api.create(payloadRows(), preview, skipExisting.value)
    review.value = true
    if (result.value.applied) { draft.markClean(); await cache.invalidateQueries({ queryKey: ['accounts'] }); await cache.invalidateQueries({ queryKey: ['account'] }); await cache.invalidateQueries({ queryKey: ['account-sql-status'] }); await cache.invalidateQueries({ queryKey: ['sql-permissions-overview'] }) }
  } catch (e) { error.value = e instanceof Error ? e.message : 'Unable to complete batch.' }
  finally { busy.value = false }
}
</script>

<style scoped>
.bulk-row { border: 1px solid rgba(128, 128, 128, .35); }
</style>
