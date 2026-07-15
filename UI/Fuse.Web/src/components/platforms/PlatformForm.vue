<template>
  <q-card class="form-dialog" :class="{ 'cluster-dialog': isCluster }">
    <q-card-section class="dialog-header">
      <div class="text-h6">{{ title }}</div>
      <q-btn flat round dense icon="close" @click="emit('cancel')" />
    </q-card-section>
    <q-separator />
    <q-form class="dialog-form" @submit.prevent="handleSubmit">
      <q-card-section class="dialog-body">
        <div class="form-grid">
          <q-input v-model="form.displayName" label="Name*" dense outlined :rules="[v => !!v || 'Display Name is required']" />
          <q-input v-model="form.dnsName" label="DNS Name" dense outlined />
          <q-input v-model="form.os" label="Operating System" dense outlined />
          <q-select
            v-model="form.kind"
            label="Kind"
            dense
            outlined
            emit-value
            map-options
            clearable
            :options="kindOptions"
          />
          <IpAddressListInput v-model="form.ipAddresses" dense outlined />
          <q-input v-model="form.notes" class="full-span" label="Notes" type="textarea" autogrow dense outlined />
          <TagSelect v-model="form.tagIds" class="full-span" />
          <div v-if="form.kind === PlatformKind.Cluster" class="cluster-nodes">
            <div class="row items-center justify-between q-mb-sm">
              <div class="text-subtitle2">Cluster Nodes</div>
              <q-btn flat dense icon="add" label="Add Node" color="primary" @click="addNode" />
            </div>
            <div v-if="form.nodes.length" class="node-table-header">
              <span>Name</span><span>DNS Name</span><span>Operating System</span><span>IP Addresses</span><span>Notes</span><span></span>
            </div>
            <div v-for="(node, index) in form.nodes" :key="node.id ?? index" class="node-row">
              <q-input v-model="node.displayName" aria-label="Node name" placeholder="Node name*" dense outlined hide-bottom-space :rules="[v => !!v || 'Node name is required']" />
              <q-input v-model="node.dnsName" aria-label="DNS name" placeholder="DNS name" dense outlined />
              <q-input v-model="node.os" aria-label="Operating system" placeholder="Operating system" dense outlined />
              <IpAddressListInput v-model="node.ipAddresses" aria-label="IP addresses" label="" placeholder="IP addresses" dense outlined />
              <q-input v-model="node.notes" aria-label="Node notes" placeholder="Notes" dense outlined />
              <q-btn flat round dense icon="delete" color="negative" aria-label="Delete node" @click="removeNode(index)" />
            </div>
            <div v-if="form.nodes.length === 0" class="text-grey-7 text-caption">Optional — leave empty for managed clusters.</div>
          </div>
        </div>
      </q-card-section>
      <q-separator />
      <q-card-actions align="right">
        <q-btn flat label="Cancel" @click="emit('cancel')" />
        <q-btn color="primary" type="submit" :label="submitLabel" :loading="loading" :disable="disabled" />
      </q-card-actions>
    </q-form>
  </q-card>
</template>

<script setup lang="ts">
import { computed, reactive, onMounted, watch } from 'vue'
import { PlatformKind, type Platform } from 'api/client'
import TagSelect from '../tags/TagSelect.vue'
import IpAddressListInput from './IpAddressListInput.vue'

type Mode = 'create' | 'edit'

export interface PlatformFormModel {
  displayName: string
  dnsName: string
  os: string | null
  kind: PlatformKind | null
  ipAddresses: string[]
  notes: string | null
  tagIds: string[]
  nodes: PlatformNodeFormModel[]
}

export interface PlatformNodeFormModel {
  id?: string
  displayName: string
  dnsName: string | null
  os: string | null
  ipAddresses: string[]
  notes: string | null
}

interface Props {
  mode?: Mode
  initialValue?: Partial<Platform> | null
  loading?: boolean
  disabled?: boolean
}

interface Emits {
  (e: 'submit', payload: PlatformFormModel): void
  (e: 'cancel'): void
}

const props = withDefaults(defineProps<Props>(), {
  mode: 'create',
  initialValue: null,
  loading: false,
  disabled: false
})
const emit = defineEmits<Emits>()

const kindOptions = computed(() => Object.values(PlatformKind)
  .map(value => ({ label: value, value: value as PlatformKind, disable: form.nodes.length > 0 && value !== PlatformKind.Cluster })))

const form = reactive<PlatformFormModel>({
  displayName: '',
  dnsName: '',
  os: null,
  kind: null,
  ipAddresses: [],
  notes: null,
  tagIds: [],
  nodes: []
})

const isCreate = computed(() => props.mode === 'create')
const isCluster = computed(() => form.kind === PlatformKind.Cluster)
const title = computed(() => `${isCreate.value ? 'Create' : 'Edit'} ${isCluster.value ? 'Cluster' : 'Platform'}`)
const submitLabel = computed(() => (isCreate.value ? 'Create' : 'Save'))
const loading = computed(() => props.loading)

function applyInitialValue(value?: Partial<Platform> | null) {
  if (!value) {
    form.displayName = ''
    form.dnsName = ''
    form.os = null
    form.tagIds = []
    form.kind = null
    form.ipAddresses = []
    form.notes = null
    form.nodes = []
    return
  }
  form.displayName = value.displayName ?? ''
  form.dnsName = value.dnsName ?? ''
  form.os = value.os ?? null
  form.kind = value.kind ?? null
  form.ipAddresses = [...(value.ipAddresses ?? [])]
  form.notes = value.notes ?? null
  form.tagIds = [...(value.tagIds ?? [])]
  form.nodes = (value.nodes ?? []).map(node => ({
    id: node.id,
    displayName: node.displayName ?? '',
    dnsName: node.dnsName ?? null,
    os: node.os ?? null,
    ipAddresses: [...(node.ipAddresses ?? [])],
    notes: node.notes ?? null
  }))
}

onMounted(() => applyInitialValue(props.initialValue))
watch(() => props.initialValue, (v) => applyInitialValue(v))

function handleSubmit() {
  emit('submit', { ...form })
}

function addNode() {
  form.nodes.push({ displayName: '', dnsName: null, os: null, ipAddresses: [], notes: null })
}

function removeNode(index: number) {
  form.nodes.splice(index, 1)
}
</script>

<style scoped>
@import '../../styles/pages.css';

.form-dialog {
  min-width: 520px;
  max-height: 90vh;
  display: flex;
  flex-direction: column;
}

.cluster-dialog {
  width: min(1400px, calc(100vw - 48px));
  max-width: 1400px;
}

.dialog-form {
  flex: 1;
  min-height: 0;
  overflow: hidden;
  display: flex;
  flex-direction: column;
}

.dialog-body { overflow-y: auto; }
.cluster-nodes { grid-column: 1 / -1; }

.node-table-header,
.node-row {
  display: grid;
  grid-template-columns: minmax(150px, 1.1fr) minmax(180px, 1.3fr) minmax(140px, 0.9fr) minmax(190px, 1.3fr) minmax(160px, 1fr) 40px;
  gap: 8px;
  align-items: start;
}

.node-table-header {
  padding: 0 4px 6px;
  color: var(--fuse-text-muted);
  font-size: 0.75rem;
  font-weight: 500;
}

.node-row {
  padding: 8px;
  border-top: 1px solid var(--fuse-border);
}

.node-row:last-of-type { border-bottom: 1px solid var(--fuse-border); }

@media (max-width: 1000px) {
  .form-dialog,
  .cluster-dialog {
    width: calc(100vw - 32px);
    min-width: 0;
  }

  .node-table-header { display: none; }
  .node-row { grid-template-columns: 1fr 1fr 40px; }
  .node-row > :nth-child(5) { grid-column: 1 / 3; }
  .node-row > :last-child { grid-column: 3; grid-row: 1; }
}

@media (max-width: 600px) {
  .node-row { grid-template-columns: 1fr 40px; }
  .node-row > :not(:last-child) { grid-column: 1; }
  .node-row > :last-child { grid-column: 2; grid-row: 1; }
}
</style>
