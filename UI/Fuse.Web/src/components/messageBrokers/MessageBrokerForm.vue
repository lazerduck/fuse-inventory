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

interface MessageBrokerFormModel {
  name: string
  kind: string
  environmentId: string | null
  connectionUri: string
  description: string
  tagIds: string[]
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
  tagIds: []
})

const isCreate = computed(() => props.mode === 'create')
const title = computed(() => (isCreate.value ? 'Create Message Broker' : 'Edit Message Broker'))
const submitLabel = computed(() => (isCreate.value ? 'Create' : 'Save'))
const loading = computed(() => props.loading)

function applyInitialValue(value?: Partial<MessageBroker> | null) {
  if (!value) {
    form.name = ''
    form.kind = ''
    form.environmentId = null
    form.connectionUri = ''
    form.description = ''
    form.tagIds = []
    return
  }
  form.name = value.name ?? ''
  form.kind = value.kind ?? ''
  form.environmentId = value.environmentId ?? null
  form.connectionUri = value.connectionUri ?? ''
  form.description = value.description ?? ''
  form.tagIds = [...(value.tagIds ?? [])]
}

onMounted(() => applyInitialValue(props.initialValue))
watch(() => props.initialValue, (v) => applyInitialValue(v))

function handleSubmit() {
  emit('submit', { ...form })
}
</script>

<style scoped>
@import '../../styles/pages.css';

.form-dialog { min-width: 480px; }
</style>
