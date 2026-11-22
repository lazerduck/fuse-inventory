<template>
  <div class="page-container">
    <div class="page-header">
      <div>
        <h1>Platforms</h1>
        <p class="subtitle">Catalogue infrastructure and link it to applications.</p>
      </div>
      <q-btn 
        color="primary" 
        label="Create Platform" 
        icon="add" 
        :disable="!fuseStore.hasPermission(Permission.PlatformsCreate)"
        @click="openCreateDialog" 
      />
    </div>

    <q-banner v-if="platformError" dense class="bg-red-1 text-negative q-mb-md">
      {{ platformError }}
    </q-banner>

    <q-banner v-if="!fuseStore.hasPermission(Permission.PlatformsRead)" dense class="bg-orange-1 text-orange-9 q-mb-md">
      You do not have permission to view platforms. Please log in with appropriate credentials.
    </q-banner>

    <q-card v-if="fuseStore.hasPermission(Permission.PlatformsRead)" class="content-card">
      <q-table
        flat
        bordered
        :rows="platforms"
        :columns="columns"
        row-key="id"
        :loading="isLoading"
        :pagination="pagination"
        @update:pagination="Object.assign(pagination, $event)"
        :filter="filter"
      >
        <template #top-right>
          <q-input
            v-model="filter"
            dense
            outlined
            debounce="300"
            placeholder="Search..."
          >
            <template #append>
              <q-icon name="search" />
            </template>
          </q-input>
        </template>
        <template #body-cell-environment="props">
          <q-td :props="props">
            {{ environmentLookup[props.row.environmentId ?? ''] ?? '—' }}
          </q-td>
        </template>
        <template #body-cell-kind="props">
          <q-td :props="props">
            {{ props.row.kind ?? '—' }}
          </q-td>
        </template>
        <template #body-cell-agent="props">
          <q-td :props="props">
            <q-btn v-if="hasAgentTargets(props.row)" flat dense no-caps color="primary"
              icon="sensors" :label="props.row.kind === PlatformKind.Cluster ? 'Node agents' : 'Agent'"
              :aria-label="`View agent reporting for ${props.row.displayName}`" @click="openAgent(props.row)" />
            <span v-else>—</span>
          </q-td>
        </template>
        <template #body-cell-tags="props">
          <q-td :props="props">
            <div v-if="props.row.tagIds?.length" class="tag-list">
              <TagChip
                v-for="tagId in props.row.tagIds"
                :key="tagId"
                :label="tagInfoLookup[tagId]?.name ?? tagId"
                :color="tagInfoLookup[tagId]?.color"
              />
            </div>
            <span v-else class="text-grey">—</span>
          </q-td>
        </template>
        <template #body-cell-actions="props">
          <q-td :props="props" class="text-right">
            <q-btn aria-label="Edit platform"
              flat 
              dense 
              round 
              icon="edit" 
              color="primary" 
              :disable="!fuseStore.hasPermission(Permission.PlatformsRead)"
              @click="openEditDialog(props.row)" 
            />
            <q-btn aria-label="Delete platform"
              flat
              dense
              round
              icon="delete"
              color="negative"
              class="q-ml-xs"
              :disable="!fuseStore.hasPermission(Permission.PlatformsDelete)"
              @click="confirmDelete(props.row)"
            />
          </q-td>
        </template>
        <template #no-data>
          <div class="q-pa-md text-grey-7">No platforms recorded.</div>
        </template>
      </q-table>
    </q-card>

    <q-dialog v-model="isDialogOpen" persistent>
      <PlatformForm
        :mode="dialogMode"
        :initial-value="selectedPlatform"
        :loading="isAnyPending"
        :disabled="dialogMode === 'edit' ? !fuseStore.hasPermission(Permission.PlatformsUpdate) : !fuseStore.hasPermission(Permission.PlatformsCreate)"
        @submit="handleSubmit"
        @cancel="closeDialog"
      />
    </q-dialog>

    <q-dialog v-if="fuseStore.hasPermission(Permission.PlatformsRead)" v-model="agentDialogOpen" @hide="agentPlatform = null">
      <q-card v-if="agentPlatform" class="agent-dialog">
        <q-card-section class="row items-center justify-between">
          <div>
            <h2 class="text-h6 q-my-none">Agent reporting</h2>
            <div class="text-subtitle2">{{ agentPlatform.displayName }}</div>
          </div>
          <q-btn v-close-popup flat round dense icon="close" aria-label="Close agent reporting" />
        </q-card-section>
        <q-separator />
        <q-card-section>
          <q-select v-if="agentPlatform.kind === PlatformKind.Cluster" v-model="agentNodeId"
            class="q-mb-md" outlined dense label="Cluster node" emit-value map-options
            :options="agentPlatform.nodes" option-value="id" option-label="displayName" />
          <PlatformAgentPanel v-if="agentPlatform.id && (agentPlatform.kind !== PlatformKind.Cluster || agentNodeId)"
            :key="agentNodeId || agentPlatform.id" :platform-id="agentPlatform.id" :node-id="agentNodeId" />
        </q-card-section>
      </q-card>
    </q-dialog>
  </div>
</template>

<script setup lang="ts">
import { computed, reactive, ref } from 'vue'
import { useQuery, useMutation, useQueryClient } from '@tanstack/vue-query'
import { Notify, Dialog } from 'quasar'
import type { QTableColumn } from 'quasar'
import { Platform, CreatePlatform, UpdatePlatform, PlatformNodeInput, PlatformKind } from 'api/client'
import { Permission } from 'permissions'
import { useFuseClient } from '../composables/useFuseClient'
import { useFuseStore } from '../stores/FuseStore'
import { useEnvironments } from '../composables/useEnvironments'
import { useTags } from '../composables/useTags'
import { usePersistedTableState } from '../composables/usePersistedTableState'
import { getErrorMessage } from '../utils/error'
import PlatformForm, { type PlatformFormModel } from '../components/platforms/PlatformForm.vue'
import PlatformAgentPanel from '../components/platforms/PlatformAgentPanel.vue'
import TagChip from '../components/tags/TagChip.vue'

const client = useFuseClient()
const queryClient = useQueryClient()
const fuseStore = useFuseStore()
const environmentsStore = useEnvironments()
const tagsStore = useTags()

// sessionStorage persistence for filter and pagination state
const STORAGE_KEY_FILTER = 'PlatformsPage_filter'
const STORAGE_KEY_PAGE = 'PlatformsPage_page'

const pagination = reactive({ rowsPerPage: 10, page: 1 })
const filter = ref('')

usePersistedTableState({
  filterStorageKey: STORAGE_KEY_FILTER,
  pageStorageKey: STORAGE_KEY_PAGE,
  filter,
  pagination
})


const { data, isLoading, error } = useQuery({
  queryKey: ['platforms'],
  queryFn: () => client.platformAll()
})

const platforms = computed(() => data.value ?? [])
const platformError = computed(() => (error.value ? getErrorMessage(error.value) : null))

const environmentLookup = environmentsStore.lookup
const tagInfoLookup = tagsStore.tagInfoLookup

const columns: QTableColumn<Platform>[] = [
  { name: 'name', label: 'Name', field: 'displayName', align: 'left', sortable: true },
  { name: 'dnsName', label: 'DNS Name', field: 'dnsName', align: 'left' },
  { name: 'os', label: 'Operating System', field: 'os', align: 'left' },
  { name: 'kind', label: 'Kind', field: 'kind', align: 'left' },
  { name: 'ipAddresses', label: 'IPs', field: row => row.ipAddresses?.join(', ') ?? '', align: 'left' },
  { name: 'nodes', label: 'Nodes', field: row => row.nodes?.length ?? 0, align: 'left', sortable: true },
  { name: 'agent', label: 'Reporting', field: 'id', align: 'left' },
  { name: 'tags', label: 'Tags', field: 'tagIds', align: 'left' },
  { name: 'actions', label: '', field: (row) => row.id, align: 'right' }
]

const isDialogOpen = ref(false)
const dialogMode = ref<'create' | 'edit'>('create')
const selectedPlatform = ref<Platform | null>(null)
const agentPlatform = ref<Platform | null>(null)
const agentDialogOpen = ref(false)
const agentNodeId = ref<string>()

function hasAgentTargets(platform: Platform) {
  return platform.kind === PlatformKind.Server || platform.kind === PlatformKind.ContainerHost ||
    (platform.kind === PlatformKind.Cluster && !!platform.nodes?.length)
}

function openAgent(platform: Platform) {
  agentPlatform.value = platform
  agentNodeId.value = platform.kind === PlatformKind.Cluster ? platform.nodes?.[0]?.id : undefined
  agentDialogOpen.value = true
}

function openCreateDialog() {
  selectedPlatform.value = null
  dialogMode.value = 'create'
  isDialogOpen.value = true
}

function openEditDialog(platform: Platform) {
  if (!platform.id) return
  selectedPlatform.value = platform
  dialogMode.value = 'edit'
  isDialogOpen.value = true
}

function closeDialog() {
  selectedPlatform.value = null
  isDialogOpen.value = false
}

const createMutation = useMutation({
  mutationFn: (payload: CreatePlatform) => client.platformPOST(payload),
  onSuccess: () => {
    queryClient.invalidateQueries({ queryKey: ['platforms'] })
    Notify.create({ type: 'positive', message: 'Platform created' })
    closeDialog()
  },
  onError: (err) => {
    Notify.create({ type: 'negative', message: getErrorMessage(err, 'Unable to create platform') })
  }
})

const updateMutation = useMutation({
  mutationFn: ({ id, payload }: { id: string; payload: UpdatePlatform }) => client.platformPUT(id, payload),
  onSuccess: () => {
    queryClient.invalidateQueries({ queryKey: ['platforms'] })
    Notify.create({ type: 'positive', message: 'Platform updated' })
    closeDialog()
  },
  onError: (err) => {
    Notify.create({ type: 'negative', message: getErrorMessage(err, 'Unable to update platform') })
  }
})

const deleteMutation = useMutation({
  mutationFn: (id: string) => client.platformDELETE(id),
  onSuccess: () => {
    queryClient.invalidateQueries({ queryKey: ['platforms'] })
    Notify.create({ type: 'positive', message: 'Platform deleted' })
  },
  onError: (err) => {
    Notify.create({ type: 'negative', message: getErrorMessage(err, 'Unable to delete platform') })
  }
})

const isAnyPending = computed(() => createMutation.isPending.value || updateMutation.isPending.value)

function toNodeInput(node: PlatformFormModel['nodes'][number]): PlatformNodeInput {
  return new PlatformNodeInput({
    id: node.id,
    displayName: node.displayName || undefined,
    dnsName: node.dnsName || undefined,
    os: node.os || undefined,
    ipAddresses: node.ipAddresses.length ? [...node.ipAddresses] : undefined,
    notes: node.notes || undefined
  })
}

function handleSubmit(model: PlatformFormModel) {
  if (dialogMode.value === 'create') {
    const payload = Object.assign(new CreatePlatform(), {
      displayName: model.displayName || undefined,
      dnsName: model.dnsName || undefined,
      os: model.os || undefined,
      kind: model.kind || undefined,
      ipAddresses: model.ipAddresses.length ? [...model.ipAddresses] : undefined,
      notes: model.notes || undefined,
      tagIds: model.tagIds.length ? [...model.tagIds] : undefined,
      nodes: model.kind === 'Cluster' ? model.nodes.map(toNodeInput) : undefined
    })
    createMutation.mutate(payload)
  } else if (dialogMode.value === 'edit' && selectedPlatform.value?.id) {
    const payload = Object.assign(new UpdatePlatform(), {
      displayName: model.displayName || undefined,
      dnsName: model.dnsName || undefined,
      os: model.os || undefined,
      kind: model.kind || undefined,
      ipAddresses: model.ipAddresses.length ? [...model.ipAddresses] : undefined,
      notes: model.notes || undefined,
      tagIds: model.tagIds.length ? [...model.tagIds] : undefined,
      nodes: model.kind === 'Cluster' ? model.nodes.map(toNodeInput) : undefined
    })
    updateMutation.mutate({ id: selectedPlatform.value.id, payload })
  }
}

function confirmDelete(platform: Platform) {
  if (!platform.id) return
  Dialog.create({
    title: 'Delete platform',
    message: `Delete "${platform.displayName ?? platform.dnsName ?? 'this platform'}"?`,
    cancel: true,
    persistent: true
  }).onOk(() => deleteMutation.mutate(platform.id!))
}
</script>

<style scoped>
@import '../styles/pages.css';

.agent-dialog {
  width: 760px;
  max-width: calc(100vw - 32px);
}
</style>
