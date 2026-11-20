<template>
  <div class="coverage">
    <template v-if="database">
      <div class="database">
        <q-icon name="storage" size="15px" aria-hidden="true" />
        <q-btn-dropdown v-if="databases.length > 1" flat dense no-caps :label="database"
          :aria-label="'Choose database, currently ' + database">
          <q-list dense>
            <q-item v-for="name in databases" :key="name" v-close-popup clickable
              :active="name === database" @click="selectedDatabase = name; showAll = false; showRestrictions = false">
              <q-item-section>{{ name }}</q-item-section>
            </q-item>
          </q-list>
        </q-btn-dropdown>
        <span v-else>{{ database }}</span>
      </div>
      <div class="privileges">
        <button v-for="entry in visibleEntries" :key="entry.permission" class="privilege"
          :aria-label="`Inspect ${entry.permission}: ${entry.level} in ${database}`"
          @click="open(entry)">
          <span>{{ entry.permission }}</span>
          <span class="level" :class="entry.level?.toLowerCase()">{{ entry.level }}</span>
        </button>
        <button v-if="entries.length > previewEntries.length" class="overflow-link" :aria-expanded="showAll" @click="showAll = !showAll">
          {{ showAll ? 'Show less' : '+' + (entries.length - previewEntries.length) + ' more' }}
        </button>
        <span v-if="!entries.length && !warning" class="muted">No observed access</span>
      </div>
      <div v-if="restrictions.length" class="restriction-links">
        <button class="overflow-link" :aria-expanded="showRestrictions" @click="showRestrictions = !showRestrictions">
          {{ showRestrictions ? 'Hide additional evidence' : 'Additional evidence (' + restrictions.length + ')' }}
        </button>
        <template v-if="showRestrictions">
        <button v-for="entry in restrictions" :key="entry.permission" class="overflow-link"
          @click="open(entry)">{{ entry.permission }}</button>
        </template>
      </div>
    </template>
    <span v-else-if="!warning" class="muted">No observed access</span>
    <div v-if="warning" class="coverage-warning" role="status"><q-icon name="info_outline" /> {{ warning }}</div>
    <q-dialog v-model="dialogOpen" aria-labelledby="sql-coverage-title">
      <q-card v-if="selected" class="evidence-card">
        <q-card-section class="evidence-header">
          <div>
            <div class="context"><q-icon name="storage" size="16px" />{{ selected.database }}
              <template v-if="principal"><span aria-hidden="true">·</span>{{ principal }}</template>
            </div>
            <div class="evidence-title"><h2 id="sql-coverage-title">{{ selected.permission }} access</h2>
              <span class="level" :class="selected.level?.toLowerCase()">{{ selected.level }}</span>
            </div>
          </div>
          <q-btn flat round dense icon="close" aria-label="Close permission details" @click="dialogOpen = false" />
        </q-card-section>
        <q-card-section class="evidence-content">
          <p class="explanation">{{ explanation }}</p>
          <q-input v-if="(selected.evidence?.length ?? 0) > 8" v-model="search" outlined dense clearable
            label="Filter permission evidence" class="q-mb-md" @update:model-value="limit = 12">
            <template #prepend><q-icon name="search" /></template>
          </q-input>
          <div v-if="filtered.length" class="evidence-table">
            <table aria-label="SQL permission evidence">
              <thead><tr><th>Scope</th><th>Permission state</th><th>Assigned to</th></tr></thead>
              <tbody><tr v-for="(entry, index) in filtered.slice(0,limit)" :key="index">
                <td><strong>{{ scopeName(entry, selected.database) }}</strong><small>{{ entry.scope }}</small></td>
                <td><span :class="{ exception: entry.state === 'DENY' || entry.state === 'REVOKE' }">{{ stateLabel(entry.state) }}</span><small v-if="entry.permission && entry.permission !== selected.permission">{{ entry.permission }}</small></td>
                <td><strong>{{ entry.grantee }}</strong><small>{{ entry.inherited ? 'Inherited' : 'Direct' }}<template v-if="entry.grantor"> · by {{ entry.grantor }}</template></small></td>
              </tr></tbody>
            </table>
          </div>
          <p v-else class="muted">{{ search ? 'No matching evidence.' : 'No permission entries were visible.' }}</p>
          <q-btn v-if="filtered.length > limit" flat dense color="primary"
            :label="`Show more (${filtered.length - limit} remaining)`" @click="limit += 12" />
        </q-card-section>
      </q-card>
    </q-dialog>
  </div>
</template>

<script setup lang="ts">
import { computed, ref, shallowRef } from 'vue'
import type { SqlPermissionEvidence, SqlPrivilegeCoverage } from 'api/client'

const props = defineProps<{
  coverage?: SqlPrivilegeCoverage[] | null
  warning?: string | null
  principal?: string | null
}>()
const selectedDatabase = ref('')
const showAll = ref(false)
const showRestrictions = ref(false)
const selected = shallowRef<SqlPrivilegeCoverage>()
const dialogOpen = ref(false)
const search = ref<string | null>('')
const limit = ref(12)

const systemDatabases = new Set(['master', 'model', 'msdb', 'tempdb'])
const databases = computed(() => [...new Set((props.coverage ?? []).flatMap(e => e.database ? [e.database] : []))]
  .sort((a,b) => Number(systemDatabases.has(a.toLowerCase())) - Number(systemDatabases.has(b.toLowerCase())) || a.localeCompare(b)))
const database = computed(() => databases.value.includes(selectedDatabase.value) ? selectedDatabase.value : databases.value[0])
const inDatabase = computed(() => (props.coverage ?? []).filter(e => e.database === database.value))
const order = ['EXECUTE', 'SELECT', 'INSERT', 'UPDATE', 'DELETE', 'REFERENCES', 'CONNECT']
function rank(permission?: string) {
  const index = order.indexOf(permission ?? '')
  return index < 0 ? order.length : index
}
const entries = computed(() => inDatabase.value.filter(e => e.level !== 'None')
  .sort((a,b) => rank(a.permission) - rank(b.permission) || (a.permission ?? '').localeCompare(b.permission ?? '')))
// Public encryption-key metadata grants are present in most SQL Server databases.
const previewEntries = computed(() => {
  const regular = entries.value.filter(e => !(
    (e.permission === 'VIEW ANY COLUMN ENCRYPTION KEY DEFINITION' || e.permission === 'VIEW ANY COLUMN MASTER KEY DEFINITION') &&
    e.evidence?.every(item => item.grantee?.toLowerCase() === 'public')
  ))
  return regular.length ? regular.slice(0, 4) : entries.value.slice(0, 1)
})
const visibleEntries = computed(() => showAll.value ? entries.value : previewEntries.value)
const restrictions = computed(() => inDatabase.value.filter(e => e.level === 'None' && e.evidence?.length))

function open(entry: SqlPrivilegeCoverage) {
  selected.value = entry
  search.value = ''
  limit.value = 12
  dialogOpen.value = true
}
const explanation = computed(() => {
  switch (selected.value?.level) {
    case 'All': return selected.value.basis === 'CurrentObjects'
      ? 'Every current callable object is covered. New objects may have different permissions.'
      : 'SQL Server reports database-wide access for this privilege.'
    case 'Some': return 'This privilege applies to part of the database. The entries below show its scope and exceptions.'
    case 'None': return selected.value.basis === 'CurrentObjects'
      ? 'These entries cover no current callable objects.'
      : 'SQL Server reports a restriction for this privilege.'
    default: return 'Fuse could not establish the extent of this privilege from the visible evidence.'
  }
})
function stateLabel(state?: string) {
  switch (state) {
    case 'GRANT': return 'Grant'
    case 'GRANT_WITH_GRANT_OPTION': return 'Grant with grant option'
    case 'DENY': return 'Deny'
    case 'REVOKE': return 'Revoke'
    default: return state ?? 'Unknown'
  }
}
function scopeName(item: SqlPermissionEvidence, db?: string) {
  if (item.scope === 'Fixed database role') return 'Database objects'
  return [item.schema, item.objectName, item.columnName].filter(Boolean).join('.') || db || 'Unknown'
}
const filtered = computed(() => {
  const needle = search.value?.trim().toLowerCase()
  const evidence = selected.value?.evidence ?? []
  if (!needle) return evidence
  return evidence.filter(e => [scopeName(e, selected.value?.database), e.scope, e.permission, e.state, stateLabel(e.state),
    e.grantee, e.grantor, e.inherited ? 'inherited' : 'direct'].some(value => value?.toLowerCase().includes(needle)))
})
</script>

<style scoped>
.coverage { min-width: 0; white-space: normal; }
.database, .context { display: flex; align-items: center; gap: .38rem; color: var(--fuse-text-muted); overflow-wrap: anywhere; }
.database { margin-bottom: .35rem; font-size: .77rem; font-weight: 600; }
.database .q-btn { min-height: 0; padding: 0 .2rem; font-size: inherit; }
.privileges, .restriction-links { display: flex; flex-wrap: wrap; align-items: center; gap: .35rem; }
.privilege { display: inline-flex; align-items: center; gap: .45rem; border: 1px solid var(--fuse-panel-border); padding: .22rem .35rem .22rem .5rem; color: var(--fuse-text); background: var(--fuse-card-bg); border-radius: 5px; font: inherit; font-size: .74rem; font-weight: 600; cursor: pointer; }
.privilege:hover, .privilege:focus-visible { border-color: var(--q-primary); background: var(--fuse-hover-bg); }
.level { padding: .08rem .35rem; border-radius: 3px; font-size: .72rem; line-height: 1.45; font-weight: 650; color: var(--fuse-text-muted); background: var(--fuse-panel-bg); }
.level.some { color: #295485; background: #e6eefb; }
.level.all { color: #256247; background: #e4f3e9; }
body.body--dark .level.some { color: #b0cfff; background: #2b3f56; }
body.body--dark .level.all { color: #a2dfbd; background: #29483b; }
.overflow-link { appearance: none; background: none; border: none; cursor: pointer; color: var(--q-primary); font: inherit; font-size: .78rem; padding: .25rem; }
.muted, .coverage-warning, .explanation { color: var(--fuse-text-muted); }
.coverage-warning { margin-top: .3rem; max-width: 42rem; font-size: .8rem; white-space: normal; }
.evidence-card { width: 46rem; max-width: calc(100vw - 2rem); }
.evidence-header { display: flex; align-items: flex-start; justify-content: space-between; gap: 1rem; padding: 1.4rem 1.5rem .3rem; }
.context { font-size: .82rem; flex-wrap: wrap; }
.evidence-title { display: flex; gap: .7rem; align-items: center; margin-top: .5rem; }
h2 { margin: 0; font-size: 1.5rem; font-weight: 650; line-height: 1.3; letter-spacing: -.025em; }
.evidence-content { padding: .4rem 1.5rem 1.5rem; }
.explanation { margin: 0 0 1.2rem; line-height: 1.5; }
.evidence-table { overflow: auto; max-height: 50vh; border: 1px solid var(--fuse-panel-border); border-radius: 5px; }
table { width: 100%; border-collapse: separate; border-spacing: 0; }
th, td { padding: .6rem .8rem; text-align: left; overflow-wrap: anywhere; }
th { position: sticky; top: 0; background: var(--fuse-card-bg); font-size: .77rem; color: var(--fuse-text-muted); }
td { border-top: 1px solid var(--fuse-panel-border); font-size: .85rem; }
strong { font-weight: 550; }
small { display: block; color: var(--fuse-text-muted); font-size: .75rem; margin-top: .1rem; }
.exception { font-weight: 650; }
@media (max-width: 600px) {
  .evidence-header { padding: 1rem 1rem .3rem; }
  .evidence-content { padding: .4rem 1rem 1rem; }
  th, td { padding: .45rem; }
}
</style>
