<template>
  <div>
    <div class="redis-observation q-mb-md">
      <span>Observed {{ observedAt }}</span>
      <q-badge :color="redisStatusColor(discovery.connectionStatus)" :label="redisStatusLabel(discovery.connectionStatus)" />
    </div>
    <q-banner v-for="warning in discovery.warnings || []" :key="warning" dense class="bg-orange-1 text-orange-9 q-mb-sm">{{ warning }}</q-banner>
    <q-banner v-if="!discovery.aclComplete" dense class="bg-blue-1 text-blue-9 q-mb-md" data-testid="acl-incomplete">
      Redis ACL discovery is incomplete. Accounts without an observed matching user have unknown status; they are not marked missing.
    </q-banner>

    <q-card flat bordered class="q-mb-lg">
      <q-card-section class="redis-section-heading"><h2 class="text-h6 q-my-none">Server metadata</h2>
        <q-badge :color="redisStatusColor(discovery.serverStatus)" :label="redisStatusLabel(discovery.serverStatus)" /></q-card-section>
      <q-card-section v-if="discovery.server">
        <dl class="redis-server-details">
          <dt>Version</dt><dd>{{ discovery.server.version || 'Unknown' }}</dd>
          <dt>Mode</dt><dd>{{ discovery.server.mode || 'Unknown' }}</dd>
          <dt>Role</dt><dd>{{ discovery.server.role || 'Unknown' }}</dd>
          <dt>Connected clients</dt><dd>{{ displayNumber(discovery.server.connectedClients) }}</dd>
          <dt>Memory used</dt><dd>{{ formatBytes(discovery.server.usedMemoryBytes) }}</dd>
          <dt>Uptime</dt><dd>{{ discovery.server.uptimeSeconds == null ? 'Unknown' : displayNumber(discovery.server.uptimeSeconds) + ' seconds' }}</dd>
        </dl>
        <h3 class="text-subtitle1 q-mt-lg">Populated logical databases</h3>
        <q-markup-table v-if="discovery.server.keyspaces?.length" flat dense>
          <thead><tr><th class="text-left">Database</th><th class="text-right">Keys</th><th class="text-right">Keys with expiry</th></tr></thead>
          <tbody><tr v-for="database in discovery.server.keyspaces" :key="database.database">
            <td>DB {{ database.database }}</td><td class="text-right">{{ displayNumber(database.keys) }}</td><td class="text-right">{{ displayNumber(database.expires) }}</td>
          </tr></tbody>
        </q-markup-table>
        <p v-else class="text-grey-7">{{ discovery.serverStatus === 'complete' ? 'No non-empty logical databases were reported.' : 'Logical database information is unavailable.' }}</p>
        <p class="text-caption text-grey-7 q-mb-none">Counts come from Redis metadata. Key names and values are not read.</p>
      </q-card-section>
      <q-card-section v-else class="text-grey-7">Server metadata could not be read. {{ redisStatusLabel(discovery.serverStatus) }}.</q-card-section>
    </q-card>

    <q-card flat bordered class="q-mb-lg">
      <q-card-section class="redis-section-heading"><div><h2 class="text-h6 q-my-none">Redis users and access rules</h2>
        <p class="text-caption text-grey-7 q-mb-none">{{ discovery.aclComplete ? `${users.length} users reported` : `${users.length} users inspected · inventory incomplete` }}</p></div>
        <q-badge :color="redisStatusColor(discovery.aclStatus)" :label="redisStatusLabel(discovery.aclStatus)" /></q-card-section>
      <q-list v-if="users.length" separator>
        <q-expansion-item v-for="user in users" :key="user.username" expand-separator>
          <template #header>
            <q-item-section><q-item-label class="text-weight-medium">{{ user.username }}</q-item-label>
              <q-item-label caption>{{ user.enabled == null ? 'Enabled state unknown' : user.enabled ? 'Enabled' : 'Disabled' }}</q-item-label></q-item-section>
            <q-item-section side><q-badge outline :color="redisStatusColor(user.status)" :label="redisStatusLabel(user.status)" /></q-item-section>
          </template>
          <q-card-section class="q-pt-none">
            <div class="q-gutter-xs q-mb-md"><q-badge v-for="flag in user.flags || []" :key="flag" color="grey-3" text-color="grey-9" :label="flag" /></div>
            <dl class="redis-rules">
              <dt>Commands</dt><dd><code>{{ ruleText(user.commands) }}</code></dd>
              <dt>Key patterns</dt><dd><code>{{ ruleText(user.keyPatterns) }}</code></dd>
              <dt>Channel patterns</dt><dd><code>{{ ruleText(user.channelPatterns) }}</code></dd>
            </dl>
            <div v-if="user.selectors?.length" class="q-mt-md">
              <div class="text-subtitle2">Additional selectors</div>
              <p class="text-caption text-grey-7">A selector is another allowed combination of commands and key/channel patterns.</p>
              <dl v-for="(selector, index) in user.selectors" :key="index" class="redis-rules redis-selector">
                <dt>Commands</dt><dd><code>{{ ruleText(selector.commands) }}</code></dd>
                <dt>Key patterns</dt><dd><code>{{ ruleText(selector.keyPatterns) }}</code></dd>
                <dt>Channel patterns</dt><dd><code>{{ ruleText(selector.channelPatterns) }}</code></dd>
              </dl>
            </div>
            <div v-if="user.matchedAccountIds?.length" class="q-mt-md">Fuse accounts:
              <router-link v-for="id in user.matchedAccountIds" :key="id" :to="{ name: 'accountEdit', params: { id } }" class="q-ml-sm">{{ accountNames[id] || id }}</router-link>
            </div>
          </q-card-section>
        </q-expansion-item>
      </q-list>
      <q-card-section v-else class="text-grey-7">{{ discovery.aclComplete ? 'Redis reported no ACL users.' : 'No user details were available from this inspection.' }}</q-card-section>
    </q-card>

    <q-card flat bordered>
      <q-card-section><h2 class="text-h6 q-my-none">Fuse account comparison</h2>
        <p class="text-caption text-grey-7 q-mb-none">Exact username matches for accounts targeting this integration’s data store. This inspection does not change Redis permissions.</p></q-card-section>
      <q-table v-if="accounts.length" flat bordered :rows="accounts" :columns="accountColumns" row-key="accountId" :pagination="{ rowsPerPage: 10 }">
        <template #body-cell-username="props"><q-td :props="props">
          <router-link :to="{ name: 'accountEdit', params: { id: props.row.accountId } }">{{ props.row.username || '(username not recorded)' }}</router-link>
        </q-td></template>
        <template #body-cell-status="props"><q-td :props="props">
          <q-badge :color="redisStatusColor(redisAccountStatus(props.row.status, discovery.aclComplete))"
            :label="redisStatusLabel(redisAccountStatus(props.row.status, discovery.aclComplete))" />
        </q-td></template>
      </q-table>
      <q-card-section v-else class="text-grey-7">No Fuse accounts target this data store.</q-card-section>
    </q-card>
  </div>
</template>

<script setup lang="ts">
import { computed } from 'vue'
import type { QTableColumn } from 'quasar'
import type { RedisAccountMatchResponse } from 'api/client'
import type { RedisDiscoveryResponse } from '../../api/redisIntegrations'
import { redisAccountStatus, redisStatusColor, redisStatusLabel } from '../../utils/redisIntegration'

const props = defineProps<{ discovery: RedisDiscoveryResponse }>()
const users = computed(() => props.discovery.users ?? [])
const accounts = computed(() => props.discovery.accounts ?? [])
const accountNames = computed(() => Object.fromEntries(accounts.value.map(account => [account.accountId, account.username])))
const observedAt = computed(() => props.discovery.observedAt ? new Date(props.discovery.observedAt).toLocaleString() : 'at an unknown time')
const accountColumns: QTableColumn<RedisAccountMatchResponse>[] = [
  { name: 'username', label: 'Fuse account', field: 'username', align: 'left', sortable: true },
  { name: 'status', label: 'Redis status', field: 'status', align: 'left' },
  { name: 'redisUsername', label: 'Matched Redis user', field: row => row.redisUsername || '—', align: 'left' },
]
function displayNumber(value?: number | null) { return value == null ? 'Unknown' : value.toLocaleString() }
function ruleText(value?: string | null) { return value == null ? 'Unknown' : value || '(none)' }
function formatBytes(value?: number | null) {
  if (value == null) return 'Unknown'
  if (value < 1024) return value + ' B'
  if (value < 1048576) return (value / 1024).toFixed(1) + ' KiB'
  return (value / 1048576).toFixed(1) + ' MiB'
}
</script>

<style scoped>
.redis-observation, .redis-section-heading { display: flex; align-items: center; justify-content: space-between; gap: 12px; flex-wrap: wrap; }
.redis-observation { color: #657078; font-size: .9rem; }
.redis-server-details, .redis-rules { display: grid; grid-template-columns: 150px minmax(0, 1fr); gap: 10px 18px; margin: 0; }
dt { color: #657078; }
dd { margin: 0; overflow-wrap: anywhere; }
code { white-space: pre-wrap; overflow-wrap: anywhere; }
.redis-selector { padding: 12px; margin: 8px 0; background: rgba(128,128,128,.07); border-radius: 4px; }
@media (max-width: 600px) { .redis-server-details, .redis-rules { grid-template-columns: 112px minmax(0, 1fr); gap: 8px; } }
</style>
