<template>
  <div class="page-container">
    <div class="page-header">
      <div>
        <h1>Message Brokers</h1>
        <p class="subtitle">Track RabbitMQ, Azure Service Bus, Kafka, and other messaging infrastructure.</p>
      </div>
      <q-btn
        color="primary"
        label="Create Broker"
        icon="add"
        :disable="!fuseStore.hasPermission(Permission.MessageBrokersCreate)"
        @click="openCreateDialog"
      />
    </div>

    <q-banner v-if="brokerError" dense class="bg-red-1 text-negative q-mb-md">
      {{ brokerError }}
    </q-banner>

    <q-banner v-if="!fuseStore.hasPermission(Permission.MessageBrokersRead)" dense class="bg-orange-1 text-orange-9 q-mb-md">
      You do not have permission to view message brokers. Please log in with appropriate credentials.
    </q-banner>

    <q-card v-if="fuseStore.hasPermission(Permission.MessageBrokersRead)" class="content-card">
      <q-table
        flat
        bordered
        :rows="brokers"
        :columns="columns"
        row-key="id"
        :loading="isLoading"
        :pagination="pagination"
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
            <span>{{ environmentLookup[props.row.environmentId] ?? props.row.environmentId ?? '—' }}</span>
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
            <q-btn
              flat
              dense
              round
              icon="edit"
              color="primary"
              :disable="!fuseStore.hasPermission(Permission.MessageBrokersUpdate)"
              @click="openEditDialog(props.row)"
            />
            <q-btn
              flat
              dense
              round
              icon="delete"
              color="negative"
              class="q-ml-xs"
              :disable="!fuseStore.hasPermission(Permission.MessageBrokersDelete)"
              @click="confirmDelete(props.row)"
            />
          </q-td>
        </template>
        <template #no-data>
          <div class="q-pa-md text-grey-7">No message brokers yet.</div>
        </template>
      </q-table>
    </q-card>

    <q-dialog v-model="isDialogOpen" persistent>
      <MessageBrokerForm
        :mode="dialogMode"
        :initial-value="selectedBroker"
        :loading="isAnyPending"
        :disabled="dialogMode === 'edit' ? !fuseStore.hasPermission(Permission.MessageBrokersUpdate) : !fuseStore.hasPermission(Permission.MessageBrokersCreate)"
        @submit="handleSubmit"
        @cancel="closeDialog"
      />
    </q-dialog>
  </div>
</template>

<script setup lang="ts">
import { computed, ref } from 'vue'
import { useQuery, useMutation, useQueryClient } from '@tanstack/vue-query'
import { Notify, Dialog } from 'quasar'
import type { QTableColumn } from 'quasar'
import { MessageBroker, CreateMessageBroker, UpdateMessageBroker, BrokerQueueInput, BrokerTopicInput, Permission } from '../api/client'
import { useFuseClient } from '../composables/useFuseClient'
import { useFuseStore } from '../stores/FuseStore'
import { useTags } from '../composables/useTags'
import { useEnvironments } from '../composables/useEnvironments'
import { getErrorMessage } from '../utils/error'
import MessageBrokerForm, { type MessageBrokerFormModel } from '../components/messageBrokers/MessageBrokerForm.vue'
import TagChip from '../components/tags/TagChip.vue'

const client = useFuseClient()
const queryClient = useQueryClient()
const fuseStore = useFuseStore()
const tagsStore = useTags()
const environmentsStore = useEnvironments()

const pagination = { rowsPerPage: 10 }
const filter = ref('')

const { data, isLoading, error } = useQuery({
  queryKey: ['messageBrokers'],
  queryFn: () => client.messageBrokerAll()
})

const brokers = computed(() => data.value ?? [])
const brokerError = computed(() => (error.value ? getErrorMessage(error.value) : null))

const tagInfoLookup = tagsStore.tagInfoLookup
const environmentLookup = environmentsStore.lookup

const columns: QTableColumn<MessageBroker>[] = [
  { name: 'name', label: 'Name', field: 'name', align: 'left', sortable: true },
  { name: 'kind', label: 'Kind', field: 'kind', align: 'left', sortable: true },
  { name: 'environment', label: 'Environment', field: 'environmentId', align: 'left', sortable: true },
  { name: 'connectionUri', label: 'Connection URI', field: 'connectionUri', align: 'left' },
  { name: 'description', label: 'Description', field: 'description', align: 'left' },
  { name: 'tags', label: 'Tags', field: 'tagIds', align: 'left' },
  { name: 'actions', label: '', field: (row) => row.id, align: 'right' }
]

const isDialogOpen = ref(false)
const dialogMode = ref<'create' | 'edit'>('create')
const selectedBroker = ref<MessageBroker | null>(null)

function openCreateDialog() {
  selectedBroker.value = null
  dialogMode.value = 'create'
  isDialogOpen.value = true
}

function openEditDialog(broker: MessageBroker) {
  if (!broker.id) return
  selectedBroker.value = broker
  dialogMode.value = 'edit'
  isDialogOpen.value = true
}

function closeDialog() {
  selectedBroker.value = null
  isDialogOpen.value = false
}

const createMutation = useMutation({
  mutationFn: (payload: CreateMessageBroker) => client.messageBrokerPOST(payload),
  onSuccess: () => {
    queryClient.invalidateQueries({ queryKey: ['messageBrokers'] })
    Notify.create({ type: 'positive', message: 'Message broker created' })
    closeDialog()
  },
  onError: (err) => {
    Notify.create({ type: 'negative', message: getErrorMessage(err, 'Unable to create message broker') })
  }
})

const updateMutation = useMutation({
  mutationFn: ({ id, payload }: { id: string; payload: UpdateMessageBroker }) =>
    client.messageBrokerPUT(id, payload),
  onSuccess: () => {
    queryClient.invalidateQueries({ queryKey: ['messageBrokers'] })
    Notify.create({ type: 'positive', message: 'Message broker updated' })
    closeDialog()
  },
  onError: (err) => {
    Notify.create({ type: 'negative', message: getErrorMessage(err, 'Unable to update message broker') })
  }
})

const deleteMutation = useMutation({
  mutationFn: (id: string) => client.messageBrokerDELETE(id),
  onSuccess: () => {
    queryClient.invalidateQueries({ queryKey: ['messageBrokers'] })
    Notify.create({ type: 'positive', message: 'Message broker deleted' })
  },
  onError: (err) => {
    Notify.create({ type: 'negative', message: getErrorMessage(err, 'Unable to delete message broker') })
  }
})

const isAnyPending = computed(() => createMutation.isPending.value || updateMutation.isPending.value)

function handleSubmit(model: MessageBrokerFormModel) {
  const queues = model.queues
    .filter(q => q.name.trim())
    .map(q => Object.assign(new BrokerQueueInput(), { name: q.name, description: q.description || undefined }))
  const topics = model.topics
    .filter(t => t.name.trim())
    .map(t => Object.assign(new BrokerTopicInput(), {
      name: t.name,
      description: t.description || undefined,
      subscribers: t.subscribersText
        ? t.subscribersText.split(',').map(s => s.trim()).filter(Boolean)
        : undefined
    }))

  if (dialogMode.value === 'create') {
    const payload = Object.assign(new CreateMessageBroker(), {
      name: model.name || undefined,
      kind: model.kind || undefined,
      environmentId: model.environmentId || undefined,
      connectionUri: model.connectionUri || undefined,
      description: model.description || undefined,
      queues: queues.length ? queues : undefined,
      topics: topics.length ? topics : undefined,
      tagIds: model.tagIds.length ? [...model.tagIds] : undefined
    })
    createMutation.mutate(payload)
  } else if (dialogMode.value === 'edit' && selectedBroker.value?.id) {
    const payload = Object.assign(new UpdateMessageBroker(), {
      name: model.name || undefined,
      kind: model.kind || undefined,
      environmentId: model.environmentId || undefined,
      connectionUri: model.connectionUri || undefined,
      description: model.description || undefined,
      queues: queues.length ? queues : undefined,
      topics: topics.length ? topics : undefined,
      tagIds: model.tagIds.length ? [...model.tagIds] : undefined
    })
    updateMutation.mutate({ id: selectedBroker.value.id, payload })
  }
}

function confirmDelete(broker: MessageBroker) {
  if (!broker.id) return
  Dialog.create({
    title: 'Delete message broker',
    message: `Delete "${broker.name ?? 'this broker'}"?`,
    cancel: true,
    persistent: true
  }).onOk(() => deleteMutation.mutate(broker.id!))
}
</script>

<style scoped>
@import '../styles/pages.css';
</style>
