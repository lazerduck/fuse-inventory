<template>
  <q-form @submit.prevent="onSubmit" class="q-gutter-md">
    <q-card flat bordered>
      <q-card-section class="text-subtitle1">Basic Info</q-card-section>
      <q-separator />
      <q-card-section>
        <!-- Basic Info -->
        <div class="row q-col-gutter-md">
          <div class="col-12 col-md-6">
            <q-input v-model="local.name" label="Name" dense outlined :rules="[rules.required]" autocomplete="off" />
          </div>
          <div class="col-12 col-md-3">
            <q-select v-model="local.type" :options="serviceTypeOptions" label="Type" dense outlined
              :rules="[rules.required]" />
          </div>
          <div class="col-12 col-md-3">
            <q-input v-model="local.version" label="Version" dense outlined />
          </div>
        </div>

        <div class="row q-col-gutter-md">
          <div class="col-12 col-md-4">
            <q-input v-model="local.author" label="Author" dense outlined />
          </div>
          <div class="col-12 col-md-4">
            <q-input v-model="local.framework" label="Framework" dense outlined />
          </div>
          <div class="col-12 col-md-4">
            <q-input v-model="local.repositoryUri" label="Repository URL" dense outlined type="url"
              :rules="[rules.urlOptional]" />
          </div>
        </div>
      <q-separator />

        <div class="row q-col-gutter-md">
          <div class="col-12 col-md-6">
            <q-input v-model="local.description" type="textarea" label="Description" autogrow outlined />
          </div>
          <div class="col-12 col-md-6">
            <q-input v-model="local.notes" type="textarea" label="Notes" autogrow outlined />
          </div>
        </div>
      </q-card-section>
    </q-card>

    <!-- Tags -->
    <q-card flat bordered>
      <q-card-section class="text-subtitle1">Tags</q-card-section>
      <q-separator />
      <q-card-section>
        <q-select v-model="tagsModel" label="Add or choose tags" dense outlined multiple use-input use-chips
          new-value-mode="add-unique" hide-dropdown-icon input-debounce="0" :options="[]"
          hint="Type a tag and press Enter" />
      </q-card-section>
    </q-card>

    <!-- Deployment Pipelines -->
    <q-card flat bordered>
      <q-card-section class="row items-center justify-between">
        <div class="text-subtitle1">Deployment Pipelines</div>
        <q-btn dense color="primary" class="q-pr-sm" icon="add" label="Add" @click="addPipeline" />
      </q-card-section>
      <q-separator />
      <q-card-section class="q-gutter-md">
        <div v-if="!pipelines.length" class="text-grey-7">No pipelines added.</div>
        <q-card v-for="(p, idx) in pipelines" :key="idx" flat bordered>
          <q-card-section class="row q-col-gutter-md items-start">
            <div class="col-12 col-md-4">
              <q-input v-model="p.name" label="Name" dense outlined hide-bottom-space :rules="[rules.required]" />
            </div>
            <div class="col-12 col-md-6">
              <q-input v-model="p.pipelineUri" label="URL" dense outlined hide-bottom-space type="url" :rules="[rules.urlOptional]" />
            </div>
            <div class="col-12 col-md-2">
              <q-btn class="self-start" flat color="negative" icon="delete" label="Remove" @click="removePipeline(idx)" />
            </div>
          </q-card-section>
        </q-card>
      </q-card-section>
    </q-card>

    <!-- Deployments -->
    <q-card flat bordered>
      <q-card-section class="row items-center justify-between">
        <div class="text-subtitle1">Deployments</div>
        <q-btn dense color="primary" class="q-pr-sm" icon="add" label="Add" @click="addDeployment" />
      </q-card-section>
      <q-separator />
      <q-card-section class="q-gutter-md">
        <div v-if="!deployments.length" class="text-grey-7">No deployments added.</div>
        <q-card v-for="(d, idx) in deployments" :key="idx" flat bordered>
          <q-card-section class="row q-col-gutter-md items-start">
            <div class="col-12 col-md-3">
              <q-input v-model="d.environmentName" label="Environment" dense outlined hide-bottom-space :rules="[rules.required]" />
            </div>
            <div class="col-12 col-md-3">
              <q-input v-model="d.deploymentUri" label="Base URL" dense outlined hide-bottom-space type="url" :rules="[rules.urlOptional]" />
            </div>
            <div class="col-12 col-md-3">
              <q-input v-model="d.swaggerUri" label="Swagger URL" dense outlined hide-bottom-space type="url"
                :rules="[rules.urlOptional]" />
            </div>
            <div class="col-12 col-md-3">
              <q-input v-model="d.healthUri" label="Health URL" dense outlined hide-bottom-space type="url"
                :rules="[rules.urlOptional]" />
            </div>
            <div class="col-12 col-md-3">
              <q-select v-model="d.status" :options="deploymentStatusOptions" label="Status" dense outlined hide-bottom-space />
            </div>
            <div class="col-12 col-md-2">
              <q-btn class="self-start" flat color="negative" icon="delete" label="Remove" @click="removeDeployment(idx)" />
            </div>
          </q-card-section>
        </q-card>
      </q-card-section>
    </q-card>

    <!-- Actions -->
    <div class="row items-center q-gutter-sm q-mt-md">
      <q-btn type="submit" color="primary" :label="submitText" :loading="loading" :disable="loading" />
      <q-btn flat color="primary" label="Cancel" v-if="showCancel" :disable="loading" @click="$emit('cancel')" />
    </div>
  </q-form>
</template>

<script setup lang="ts">
import { computed, reactive, toRaw, watch } from 'vue'
import { CreateServiceCommandType, DeploymentsStatus, Tags, type ICreateServiceCommand, type ServiceManifest } from '../httpClients/client.gen'

// Props & Emits
interface Props {
  modelValue?: ICreateServiceCommand | ServiceManifest | null
  mode?: 'create' | 'edit'
  loading?: boolean
  submitLabel?: string
  showCancel?: boolean
}
const props = withDefaults(defineProps<Props>(), {
  modelValue: null,
  mode: 'create',
  loading: false,
  submitLabel: '',
  showCancel: true
})

const emit = defineEmits<{
  (e: 'update:modelValue', v: ICreateServiceCommand | ServiceManifest): void
  (e: 'submit', v: ICreateServiceCommand | ServiceManifest): void
  (e: 'cancel'): void
}>()

// Options for selects
const serviceTypeOptions = Object.values(CreateServiceCommandType)
const deploymentStatusOptions = Object.values(DeploymentsStatus)

// Validation rules
const rules = {
  required: (v: any) => (v != null && String(v).trim().length > 0) || 'Required',
  urlOptional: (v: any) => {
    if (!v) return true
    try {
      // Basic URL validation
      new URL(String(v))
      return true
    } catch {
      return 'Invalid URL'
    }
  }
}

// Local reactive copy (avoid mutating the prop directly)
function makeEmpty(): ICreateServiceCommand {
  return {
    name: '',
    version: '',
    description: '',
    notes: '',
    author: '',
    framework: '',
    repositoryUri: '',
    type: CreateServiceCommandType.WebApi,
    deploymentPipelines: [],
    deployments: [],
    tags: []
  }
}

function deepClone<T>(obj: T): T {
  return obj ? (JSON.parse(JSON.stringify(obj)) as T) : (obj as T)
}

const local = reactive<ICreateServiceCommand | ServiceManifest>(
  props.modelValue ? deepClone(props.modelValue) : makeEmpty()
)

// Keep local in sync if parent updates the v-model
watch(
  () => props.modelValue,
  (val) => {
    if (val) {
      const cloned = deepClone(val)
      Object.assign(local, cloned)
    } else {
      Object.assign(local, makeEmpty())
    }
  }
)

// Note: We avoid emitting update:modelValue on each local change to prevent reactive loops.

const submitText = computed(() => props.submitLabel || (props.mode === 'edit' ? 'Save' : 'Create'))

// Safe arrays for template to avoid optional chain issues with union type
const pipelines = computed<any[]>(() => ((local as any).deploymentPipelines ?? []) as any[])
const deployments = computed<any[]>(() => ((local as any).deployments ?? []) as any[])

function addPipeline() {
  ; (local as any).deploymentPipelines.push({ name: '', pipelineUri: '' })
}
function removePipeline(idx: number) {
  ; (local as any).deploymentPipelines.splice(idx, 1)
}

function addDeployment() {
  ; (local as any).deployments.push({
    environmentName: '',
    deploymentUri: '',
    swaggerUri: '',
    healthUri: '',
    status: DeploymentsStatus.Unknown
  })
}
function removeDeployment(idx: number) {
  ; (local as any).deployments.splice(idx, 1)
}

// Tags adapter: UI uses string[] chips, API expects Tags[] objects
const tagsModel = computed<string[]>({
  get() {
    const list = (local as any).tags as any[] | undefined
    if (!list) return []
    return list.map((t: any) => (typeof t === 'string' ? t : t?.name ?? '')).filter(Boolean)
  },
  set(val: string[]) {
    ;(local as any).tags = val.map((name) => {
      const t = new Tags()
      t.name = name
      return t
    })
  }
})

async function onSubmit() {
  // Ensure tags are Tags[] based on current chips
  tagsModel.value = tagsModel.value
  emit('submit', deepClone(toRaw(local)))
}
</script>

<style scoped></style>
