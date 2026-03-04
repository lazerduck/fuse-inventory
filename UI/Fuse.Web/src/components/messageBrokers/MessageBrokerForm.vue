<template>
  <q-card class="form-dialog">
    <q-card-section class="dialog-header">
      <div class="text-h6">{{ title }}</div>
      <q-btn flat round dense icon="close" @click="emit('cancel')" />
    </q-card-section>
    <q-separator />
    <q-form ref="formRef" @submit.prevent="handleSubmit" novalidate>
      <q-card-section>
        <div class="form-grid">
          <q-input
            v-model="form.name"
            label="Name*"
            dense
            outlined
            :rules="[val => !!val || 'Name is required']"
          />
          <q-select
            v-model="form.kind"
            label="Kind*"
            dense
            outlined
            use-input
            hide-dropdown-icon
            new-value-mode="add"
            clearable
            :options="kindOptions"
            :rules="[val => !!val || 'Kind is required']"
            @new-value="onNewKind"
          />
          <q-select
            v-model="form.environmentId"
            label="Environment*"
            dense
            outlined
            emit-value
            map-options
            :options="environmentOptions"
            :rules="[val => !!val || 'Environment is required']"
          />
          <q-input v-model="form.connectionUri" label="Connection URI" dense outlined />
          <TagSelect v-model="form.tagIds" />
          <q-input
            v-model="form.description"
            type="textarea"
            label="Description"
            dense
            outlined
            autogrow
            class="full-span"
          />
        </div>
      </q-card-section>

      <q-separator />

      <!-- Queues -->
      <q-card-section>
        <div class="section-header">
          <div class="text-subtitle1 text-weight-medium">Queues</div>
          <q-btn flat dense size="sm" icon="add" color="primary" label="Add Queue" @click="addQueue" />
        </div>
        <div v-if="form.queues.length === 0" class="text-grey-6 text-caption q-mt-sm">No queues defined.</div>
        <div v-for="(queue, index) in form.queues" :key="index" class="channel-row q-mt-sm">
          <q-input v-model="queue.name" label="Queue name*" dense outlined class="channel-name" :rules="[val => !!val || 'Required']" />
          <q-input v-model="queue.description" label="Description" dense outlined class="channel-desc" />
          <q-btn flat round dense icon="delete" color="negative" @click="removeQueue(index)" />
        </div>
      </q-card-section>

      <q-separator />

      <!-- Topics -->
      <q-card-section>
        <div class="section-header">
          <div class="text-subtitle1 text-weight-medium">Topics</div>
          <q-btn flat dense size="sm" icon="add" color="primary" label="Add Topic" @click="addTopic" />
        </div>
        <div v-if="form.topics.length === 0" class="text-grey-6 text-caption q-mt-sm">No topics defined.</div>
        <div v-for="(topic, index) in form.topics" :key="index" class="topic-row q-mt-sm">
          <div class="topic-main">
            <q-input v-model="topic.name" label="Topic name*" dense outlined class="channel-name" :rules="[val => !!val || 'Required']" />
            <q-input v-model="topic.description" label="Description" dense outlined class="channel-desc" />
            <q-btn flat round dense icon="delete" color="negative" @click="removeTopic(index)" />
          </div>
          <q-input
            v-model="topic.subscribersText"
            label="Subscribers (comma-separated)"
            dense
            outlined
            class="subscribers-input"
            hint="e.g. PaymentService, EmailService"
          />
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
import { computed, reactive, watch, onMounted, ref } from 'vue'
import { useEnvironments } from '../../composables/useEnvironments'
import type { MessageBroker } from '../../api/client'
import TagSelect from '../tags/TagSelect.vue'

type Mode = 'create' | 'edit'

interface QueueFormItem {
  name: string
  description: string
}

interface TopicFormItem {
  name: string
  description: string
  subscribersText: string
}

export interface MessageBrokerFormModel {
  name: string
  kind: string
  environmentId: string | null
  connectionUri: string
  description: string
  tagIds: string[]
  queues: QueueFormItem[]
  topics: TopicFormItem[]
}

interface Props {
  mode?: Mode
  initialValue?: Partial<MessageBroker> | null
  loading?: boolean
  disabled?: boolean
}

interface Emits {
  (e: 'submit', payload: MessageBrokerFormModel): void
  (e: 'cancel'): void
}

const props = withDefaults(defineProps<Props>(), {
  mode: 'create',
  initialValue: null,
  loading: false,
  disabled: false
})
const emit = defineEmits<Emits>()

const environmentsStore = useEnvironments()
const environmentOptions = environmentsStore.options

const kindOptions = ref([
  'RabbitMQ',
  'Azure Service Bus',
  'Azure Event Hub',
  'Apache Kafka',
  'AWS SQS',
  'AWS SNS',
  'Google Pub/Sub',
  'NATS',
  'Redis Streams',
  'Other'
])

function onNewKind(val: string, done: (val: string) => void) {
  if (val && !kindOptions.value.includes(val)) {
    kindOptions.value.push(val)
  }
  done(val)
}

const formRef = ref()

const form = reactive<MessageBrokerFormModel>({
  name: '',
  kind: '',
  environmentId: null,
  connectionUri: '',
  description: '',
  tagIds: [],
  queues: [],
  topics: []
})

const isCreate = computed(() => props.mode === 'create')
const title = computed(() => (isCreate.value ? 'Create Message Broker' : 'Edit Message Broker'))
const submitLabel = computed(() => (isCreate.value ? 'Create' : 'Save'))
const loading = computed(() => props.loading)

function addQueue() {
  form.queues.push({ name: '', description: '' })
}

function removeQueue(index: number) {
  form.queues.splice(index, 1)
}

function addTopic() {
  form.topics.push({ name: '', description: '', subscribersText: '' })
}

function removeTopic(index: number) {
  form.topics.splice(index, 1)
}

function applyInitialValue(value?: Partial<MessageBroker> | null) {
  if (!value) {
    form.name = ''
    form.kind = ''
    form.environmentId = null
    form.connectionUri = ''
    form.description = ''
    form.tagIds = []
    form.queues = []
    form.topics = []
    return
  }
  form.name = value.name ?? ''
  form.kind = value.kind ?? ''
  form.environmentId = value.environmentId ?? null
  form.connectionUri = value.connectionUri ?? ''
  form.description = value.description ?? ''
  form.tagIds = [...(value.tagIds ?? [])]
  form.queues = (value.queues ?? []).map(q => ({ name: q.name ?? '', description: q.description ?? '' }))
  form.topics = (value.topics ?? []).map(t => ({
    name: t.name ?? '',
    description: t.description ?? '',
    subscribersText: (t.subscribers ?? []).join(', ')
  }))
}

onMounted(() => applyInitialValue(props.initialValue))
watch(() => props.initialValue, (v) => applyInitialValue(v))

function handleSubmit() {
  emit('submit', { ...form })
}
</script>

<style scoped>
@import '../../styles/pages.css';

.form-dialog { min-width: 560px; max-width: 720px; max-height: 85vh; overflow-y: auto; }

.section-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-bottom: 0.25rem;
}

.channel-row {
  display: flex;
  align-items: flex-start;
  gap: 0.5rem;
}

.channel-name { flex: 1 1 0; min-width: 0; }
.channel-desc { flex: 2 1 0; min-width: 0; }

.topic-row {
  display: flex;
  flex-direction: column;
  gap: 0.4rem;
  padding: 0.5rem 0.75rem;
  border: 1px solid var(--fuse-panel-border, #e0e0e0);
  border-radius: 6px;
}

.topic-main {
  display: flex;
  align-items: flex-start;
  gap: 0.5rem;
}

.subscribers-input { width: 100%; }
</style>

