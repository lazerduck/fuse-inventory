<template>
  <q-card flat bordered class="graph-inspector" tag="aside" aria-label="Graph inspector">
    <div class="inspector-header row items-start no-wrap q-pa-md">
      <div class="col">
        <div class="text-overline text-grey-7">{{ edge ? 'Relationship' : node ? kindLabel(node.kind) : 'Inspector' }}</div>
        <h2 class="text-h6 q-ma-none inspector-title">{{ edge ? 'Dependency details' : node?.name ?? 'Explore your inventory' }}</h2>
        <div v-if="node?.environmentName && !edge" class="text-caption text-grey-7">{{ node.environmentName }}</div>
      </div>
      <q-btn v-if="node || edge" flat round dense icon="close" aria-label="Close inspector" @click="emit('close')" />
    </div>

    <div v-if="!node && !edge" class="q-pa-md q-pt-none text-body2 text-grey-7">
      <q-icon name="touch_app" size="2rem" color="primary" class="q-mb-sm" />
      <p>Select a resource or relationship in the graph to see its details.</p>
      <p class="q-mb-none">Use search to find a resource, then follow its dependencies or inspect what depends on it.</p>
    </div>

    <template v-else-if="edge">
      <q-separator />
      <q-card-section>
        <div class="text-caption text-grey-7">Source</div>
        <q-btn flat no-caps align="left" color="primary" class="resource-link" :label="sourceNode?.name ?? edge.source"
          :aria-label="`Inspect source ${sourceNode?.name ?? edge.source}`" @click="emit('select-node', edge.source)" />
        <div v-if="sourceNode?.environmentName" class="text-caption text-grey-7">{{ sourceNode.environmentName }}</div>
        <div class="direction-label text-caption q-my-sm"><q-icon name="south" color="primary" /> depends on</div>
        <q-btn flat no-caps align="left" color="primary" class="resource-link" :label="targetNode?.name ?? edge.target"
          :aria-label="`Inspect target ${targetNode?.name ?? edge.target}`" @click="emit('select-node', edge.target)" />
        <div v-if="targetNode" class="text-caption text-grey-7">{{ [kindLabel(targetNode.kind), targetNode.environmentName].filter(Boolean).join(' · ') }}</div>
      </q-card-section>
      <q-separator />
      <q-card-section>
        <dl class="detail-list">
          <dt>Port</dt><dd>{{ edge.dependency.port ?? 'Not specified' }}</dd>
          <dt>Authentication</dt><dd>{{ authLabel(edge.dependency.authKind) }}</dd>
          <template v-if="edge.dependency.authKind === 'Account'">
            <dt>Account</dt>
            <dd><router-link v-if="edge.dependency.accountId" :to="{ name: 'viewAccount', params: { id: edge.dependency.accountId } }">{{ accountLabel }}</router-link><span v-else>Not specified</span></dd>
          </template>
          <template v-if="edge.dependency.authKind === 'Identity'">
            <dt>Identity</dt>
            <dd><router-link v-if="edge.dependency.identityId" :to="{ name: 'viewIdentity', params: { id: edge.dependency.identityId } }">{{ identityLabel }}</router-link><span v-else>Not specified</span></dd>
          </template>
          <dt>Failure impact</dt>
          <dd><q-badge :color="edge.dependency.severity === 'Partial' ? 'warning' : 'negative'" :label="edge.dependency.severity === 'Partial' ? 'Partial' : 'Full'" /></dd>
        </dl>
        <p class="text-caption text-grey-7 q-mt-sm q-mb-none">{{ edge.dependency.severity === 'Partial' ? 'The source may be degraded if this dependency fails.' : 'The source cannot operate without this dependency.' }}</p>
      </q-card-section>
      <q-card-actions v-if="canEdit" vertical class="q-px-md q-pb-md">
        <q-btn outline color="primary" icon="edit" label="Edit relationship" @click="emit('edit-dependency')" />
        <q-btn flat color="negative" icon="link_off" label="Remove relationship" @click="emit('delete-dependency')" />
      </q-card-actions>
    </template>

    <template v-else-if="node">
      <q-card-section class="q-pt-none">
        <q-badge v-if="node.boundary" outline color="grey-7" class="q-mb-sm" label="Outside selected environments" />
        <p v-if="node.description" class="resource-description text-body2 text-grey-7 q-mb-md" :title="node.description">{{ node.description }}</p>
        <div class="row q-gutter-sm">
          <q-btn outline color="primary" icon="open_in_new" label="Open details" :to="detailsRoute(node)" />
          <q-btn flat color="primary" icon="crisis_alert" label="Show impact" @click="emit('show-impact')" />
        </div>
        <dl v-if="node.owner || node.version || node.platformName || health" class="detail-list q-mt-md">
          <template v-if="node.owner"><dt>Owner</dt><dd>{{ node.owner }}</dd></template>
          <template v-if="node.version"><dt>Version</dt><dd>{{ node.version }}</dd></template>
          <template v-if="node.platformName">
            <dt>Platform</dt><dd><router-link v-if="node.platformId" :to="{ name: 'viewPlatform', params: { id: node.platformId } }">{{ node.platformName }}</router-link><span v-else>{{ node.platformName }}</span></dd>
          </template>
          <template v-if="health && node.kind === 'Application'">
            <dt>Monitored health</dt><dd><q-badge :color="healthColor(health.status)" :label="health.status" /><div v-if="health.checkedAt" class="text-caption text-grey-7 q-mt-xs">Checked {{ formatTime(health.checkedAt) }}</div></dd>
          </template>
        </dl>
        <div class="column q-gutter-xs q-mt-sm">
          <q-btn v-if="node.kind === 'Application' && node.applicationId" flat dense no-caps align="left" color="primary" icon="apps" label="Open application"
            :to="{ name: 'viewApp', params: { id: node.applicationId } }" />
          <q-btn v-if="canEdit && node.kind === 'Application' && node.applicationId" flat dense no-caps align="left" color="primary" icon="edit" label="Edit instance"
            :to="{ name: 'instanceEdit', params: { applicationId: node.applicationId, instanceId: node.entityId } }" />
          <q-btn v-if="canEdit" flat dense no-caps align="left" color="primary" icon="add_link"
            :label="node.kind === 'Application' ? 'Add dependency' : 'Add dependent'" @click="emit('add-dependency')" />
        </div>
      </q-card-section>

      <q-separator />
      <q-tabs v-model="relationshipDirection" dense no-caps active-color="primary" indicator-color="primary" align="justify">
        <q-tab name="outgoing" :label="`Depends on (${outgoing.length})`" />
        <q-tab name="incoming" :label="`Dependants (${incoming.length})`" />
      </q-tabs>
      <div class="relationships-panel" role="region" :aria-label="relationshipDirection === 'outgoing' ? 'Dependencies' : 'Dependants'">
        <p v-if="!visibleRelationships.length" class="text-caption text-grey-7 q-pa-md q-mb-none">{{ relationshipDirection === 'outgoing' ? 'No outgoing dependencies documented.' : 'No dependants documented.' }}</p>
        <q-list v-else separator>
          <q-item v-for="relationship in visibleRelationships" :key="relationship.id" clickable class="relationship-item"
            :aria-label="`Inspect relationship ${relationshipName(relationship)}`" @click="emit('select-edge', relationship.id)">
            <q-item-section>
              <q-item-label class="text-weight-medium">{{ relationshipName(relationship) }}</q-item-label>
              <q-item-label caption>{{ relationshipSummary(relationship) }}</q-item-label>
            </q-item-section>
            <q-item-section side><q-icon name="chevron_right" /></q-item-section>
          </q-item>
        </q-list>
      </div>

      <q-separator />
      <q-card-section>
        <h3 class="text-subtitle2 q-mt-none q-mb-sm">Active risks<span v-if="!risksLoading && !risksUnavailable"> ({{ activeRisks.length }})</span></h3>
        <p v-if="risksUnavailable" class="text-caption text-grey-7 q-mb-none" role="status">Risk information is unavailable for this resource.</p>
        <p v-else-if="risksLoading" class="text-caption text-grey-7 q-mb-none" role="status">Loading risk information…</p>
        <template v-else>
          <p v-if="!activeRisks.length" class="text-caption text-grey-7 q-mb-none">No active risks recorded for this resource.</p>
          <div v-for="risk in activeRisks" :key="risk.id" class="risk-entry q-py-sm">
            <div class="row items-start no-wrap q-gutter-sm">
              <q-badge outline :color="riskColor(risk.impact)" :label="risk.impact ?? 'Unrated'" />
              <div class="col text-body2 text-weight-medium">{{ risk.title || 'Untitled risk' }}</div>
            </div>
            <div class="row items-center justify-between q-mt-xs">
              <span class="text-caption text-grey-7">{{ risk.targetType === 'Application' ? 'Application risk' : 'Resource risk' }} · {{ risk.status }}</span>
              <q-btn v-if="risk.id" flat dense no-caps size="sm" color="primary" label="View risk" :aria-label="`View risk ${risk.title || 'Untitled risk'}`" :to="{ name: 'viewRisk', params: { id: risk.id } }" />
            </div>
          </div>
        </template>
      </q-card-section>
    </template>
  </q-card>
</template>

<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import type { Account, Identity, Risk } from 'api/client'
import type { GraphDependencyEdge, GraphResourceNode } from '../../utils/inventoryGraph'

const props = defineProps<{
  node: GraphResourceNode | null
  edge: GraphDependencyEdge | null
  nodes: GraphResourceNode[]
  edges: GraphDependencyEdge[]
  risks: Risk[]
  risksLoading?: boolean
  risksUnavailable?: boolean
  accounts: Account[]
  identities: Identity[]
  canEdit: boolean
  health?: { status: string; checkedAt?: Date | string } | null
}>()

const emit = defineEmits<{
  (event: 'select-node', id: string): void
  (event: 'select-edge', id: string): void
  (event: 'close'): void
  (event: 'add-dependency'): void
  (event: 'edit-dependency'): void
  (event: 'delete-dependency'): void
  (event: 'show-impact'): void
}>()

const lookup = computed(() => new Map(props.nodes.map(node => [node.id, node])))
const sourceNode = computed(() => props.edge ? lookup.value.get(props.edge.source) : undefined)
const targetNode = computed(() => props.edge ? lookup.value.get(props.edge.target) : undefined)
const outgoing = computed(() => props.node ? props.edges.filter(edge => edge.source === props.node!.id) : [])
const incoming = computed(() => props.node ? props.edges.filter(edge => edge.target === props.node!.id) : [])
const relationshipDirection = ref('outgoing')
watch(() => props.node?.id, () => { relationshipDirection.value = props.node?.kind === 'Application' ? 'outgoing' : 'incoming' }, { immediate: true })
const visibleRelationships = computed(() => relationshipDirection.value === 'outgoing' ? outgoing.value : incoming.value)
const accountLabel = computed(() => props.accounts.find(account => account.id === props.edge?.dependency.accountId)?.userName || props.edge?.dependency.accountId || 'Not specified')
const identityLabel = computed(() => props.identities.find(identity => identity.id === props.edge?.dependency.identityId)?.name || props.edge?.dependency.identityId || 'Not specified')
const riskOrder: Record<string, number> = { Critical: 4, High: 3, Medium: 2, Low: 1 }
const activeRisks = computed(() => {
  const node = props.node
  if (!node) return []
  return props.risks.filter(risk => {
    if (risk.status === 'Mitigated' || risk.status === 'Closed') return false
    if (node.kind === 'Application') return (risk.targetType === 'ApplicationInstance' && risk.targetId === node.entityId) ||
      (risk.targetType === 'Application' && risk.targetId === node.applicationId)
    const riskType = node.kind === 'External' ? 'ExternalResource' : node.kind
    return risk.targetType === riskType && risk.targetId === node.entityId
  }).sort((a, b) => (riskOrder[b.impact ?? ''] ?? 0) - (riskOrder[a.impact ?? ''] ?? 0) || (a.title ?? '').localeCompare(b.title ?? ''))
})

function kindLabel(kind: GraphResourceNode['kind']) {
  return { Application: 'Application instance', DataStore: 'Data store', External: 'External resource', MessageBroker: 'Message broker' }[kind]
}
function detailsRoute(node: GraphResourceNode) {
  return { name: { Application: 'viewInstance', DataStore: 'viewDatastore', External: 'viewExternal', MessageBroker: 'viewMessageBroker' }[node.kind], params: { id: node.entityId } }
}
function authLabel(kind?: string) { return kind === 'ApiKey' ? "Target's API key" : kind ?? 'None' }
function relationshipNode(edge: GraphDependencyEdge) { return lookup.value.get(relationshipDirection.value === 'outgoing' ? edge.target : edge.source) }
function relationshipName(edge: GraphDependencyEdge) { return relationshipNode(edge)?.name ?? (relationshipDirection.value === 'outgoing' ? edge.target : edge.source) }
function relationshipSummary(edge: GraphDependencyEdge) {
  const related = relationshipNode(edge)
  return [related?.environmentName, edge.dependency.port != null ? `Port ${edge.dependency.port}` : null, `${edge.dependency.severity ?? 'Full'} impact`].filter(Boolean).join(' · ')
}
function riskColor(impact?: string) {
  const colors: Record<string, string> = { Critical: 'negative', High: 'deep-orange', Medium: 'orange-9', Low: 'positive' }
  return colors[impact ?? ''] ?? 'grey-7'
}
function healthColor(status: string) {
  switch (status.toLowerCase()) {
    case 'healthy': case 'up': return 'positive'
    case 'degraded': return 'warning'
    case 'unhealthy': case 'down': return 'negative'
    default: return 'grey-7'
  }
}
function formatTime(value: Date | string) {
  const date = value instanceof Date ? value : new Date(value)
  return Number.isNaN(date.getTime()) ? 'at an unknown time' : date.toLocaleString()
}
</script>

<style scoped>
.graph-inspector { height: 100%; overflow-y: auto; min-width: 0; }
.inspector-header { gap: 0.5rem; }
.inspector-title { overflow-wrap: anywhere; line-height: 1.35; }
.resource-description { display: -webkit-box; -webkit-line-clamp: 3; -webkit-box-orient: vertical; overflow: hidden; overflow-wrap: anywhere; }
.resource-link { padding-left: 0; padding-right: 0; max-width: 100%; overflow-wrap: anywhere; }
.direction-label { display: flex; align-items: center; gap: 0.4rem; }
.detail-list { display: grid; grid-template-columns: minmax(5.5rem, auto) minmax(0, 1fr); gap: 0.6rem 0.8rem; margin-bottom: 0; font-size: 0.875rem; }
.detail-list dt { color: var(--q-primary); }
.detail-list dd { margin: 0; overflow-wrap: anywhere; }
.detail-list a { color: var(--q-primary); text-decoration: underline; text-underline-offset: 2px; }
.relationship-item { width: 100%; border: 0; background: transparent; color: inherit; text-align: left; }
.relationship-item:focus-visible { outline: 2px solid var(--q-primary); outline-offset: -2px; }
.risk-entry + .risk-entry { border-top: 1px solid rgba(128, 128, 128, 0.2); }
</style>
