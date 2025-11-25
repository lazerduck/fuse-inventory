<template>
  <q-card class="form-card">
    <q-card-section>
      <div class="text-h6">{{ mode === 'edit' ? 'Edit SQL Integration' : 'Add SQL Integration' }}</div>
    </q-card-section>
    <q-card-section>
      <q-form @submit.prevent="submitForm">
        <q-input 
          v-model="form.name" 
          label="Name" 
          :disable="loading || testLoading" 
          required
          hint="A friendly name for this SQL integration"
        />
        
        <q-select 
          v-model="form.dataStoreId" 
          :options="dataStoreOptions" 
          label="Data Store" 
          :disable="loading || testLoading" 
          required
          hint="The data store this integration connects to"
          option-label="label"
          option-value="value"
          emit-value
          map-options
        >
          <template #option="scope">
            <q-item :clickable="true" @click="scope.toggleOption(scope.opt)">
              <q-item-section>
                <q-item-label>{{ scope.opt.label }}</q-item-label>
                <q-item-label v-if="scope.opt.description" caption>
                  {{ scope.opt.description }}
                </q-item-label>
              </q-item-section>
            </q-item>
          </template>

          <template #selected-item="scope">
            <q-item>
              <q-item-section>
                <q-item-label>{{ scope.opt.label }}</q-item-label>
                <q-item-label v-if="scope.opt.description" caption>
                  {{ scope.opt.description }}
                </q-item-label>
              </q-item-section>
            </q-item>
          </template>
        </q-select>
        
        <q-input 
          v-model="form.connectionString" 
          label="Connection String" 
          :disable="loading || testLoading" 
          type="textarea"
          rows="3"
          required
          hint="SQL Server connection string (e.g., Server=myserver;Database=mydb;...)"
        />

        <!-- Connection Test Results -->
        <div v-if="testResult" class="q-mt-md q-pa-md" :class="testResultClass">
          <div class="row items-center q-gutter-sm">
            <q-icon :name="testResult.isSuccessful ? 'check_circle' : 'error'" size="sm" />
            <div class="text-weight-medium">
              {{ testResult.isSuccessful ? 'Connection Successful' : 'Connection Failed' }}
            </div>
          </div>
          <div v-if="testResult.isSuccessful && testResult.permissions" class="q-mt-sm">
            <div class="text-caption text-grey-7 q-mb-xs">Detected Permissions:</div>
            <div class="q-gutter-xs">
              <q-badge 
                v-for="perm in parsePermissions(testResult.permissions)" 
                :key="perm" 
                outline 
                color="primary"
                :label="perm"
              />
            </div>
          </div>
          <div v-if="testResult.errorMessage" class="q-mt-sm text-caption">
            {{ testResult.errorMessage }}
          </div>
        </div>

        <div class="q-mt-md flex justify-between">
          <q-btn 
            label="Test Connection" 
            icon="cable" 
            color="secondary" 
            outline
            @click="testConnection" 
            :loading="testLoading" 
            :disable="!canTestConnection || loading"
          />
          <div class="q-gutter-sm">
            <q-btn label="Cancel" flat @click="$emit('cancel')" :disable="loading || testLoading" />
            <q-btn 
              label="Save" 
              color="primary" 
              type="submit" 
              :loading="loading" 
              :disable="testLoading || !hasSuccessfulTest"
            />
          </div>
        </div>
      </q-form>
    </q-card-section>
  </q-card>
</template>

<script setup lang="ts">
import { ref, watch, computed } from 'vue'
import { useMutation } from '@tanstack/vue-query'
import { Notify } from 'quasar'
import { useDataStores } from '../../composables/useDataStores'
import { useFuseClient } from '../../composables/useFuseClient'
import { TestSqlConnection, SqlConnectionTestResult } from '../../api/client'
import { getErrorMessage } from '../../utils/error'
import { parseSqlPermissions } from '../../utils/sqlPermissions'

const props = defineProps<{ 
  mode: 'create' | 'edit'
  initialValue?: any
  loading: boolean 
}>()
const emit = defineEmits(['submit', 'cancel'])

const client = useFuseClient()
const dataStoresStore = useDataStores()

const dataStoreOptions = computed(() => {
  const stores = dataStoresStore.data.value ?? []

  const options = stores.map(ds => {
    const kind = (ds.kind ?? '').toString()
    return {
      label: ds.name ?? ds.id!,
      value: ds.id,
      description: kind,
      kindKey: kind.toLowerCase()
    }
  })

  const sqlKinds = ['sql', 'sql server', 'mssql', 'postgresql', 'postgres', 'mysql', 'sqlite']

  options.sort((a, b) => {
    const aIsSql = sqlKinds.includes(a.kindKey)
    const bIsSql = sqlKinds.includes(b.kindKey)
    if (aIsSql && !bIsSql) return -1
    if (!aIsSql && bIsSql) return 1
    return a.label.localeCompare(b.label)
  })

  return options
})

const form = ref<{
  name: string
  dataStoreId: string | null
  connectionString: string
}>({
  name: '',
  dataStoreId: null,
  connectionString: ''
})

const testResult = ref<SqlConnectionTestResult | null>(null)
const testLoading = ref(false)

watch(() => props.initialValue, (val) => {
  if (val) {
    form.value = {
      name: val.name ?? '',
      dataStoreId: val.dataStoreId ?? null,
      connectionString: '' // Don't show stored connection string for security
    }
    testResult.value = null // Reset test result on edit
  } else {
    form.value = {
      name: '',
      dataStoreId: null,
      connectionString: ''
    }
    testResult.value = null
  }
}, { immediate: true })

const canTestConnection = computed(() => 
  !!form.value.connectionString && form.value.connectionString.length > 0
)

const hasSuccessfulTest = computed(() => 
  testResult.value?.isSuccessful === true || props.mode === 'edit'
)

const testResultClass = computed(() => ({
  'bg-green-1': testResult.value?.isSuccessful,
  'bg-red-1': !testResult.value?.isSuccessful,
  'text-positive': testResult.value?.isSuccessful,
  'text-negative': !testResult.value?.isSuccessful,
  'rounded-borders': true
}))

const testConnectionMutation = useMutation({
  mutationFn: (connectionString: string) => {
    const command = Object.assign(new TestSqlConnection(), { connectionString })
    return client.testConnection2(command)
  },
  onSuccess: (result) => {
    testResult.value = result
    if (result.isSuccessful) {
      Notify.create({ type: 'positive', message: 'Connection test successful' })
    } else {
      Notify.create({ type: 'warning', message: 'Connection test failed' })
    }
  },
  onError: (err) => {
    Notify.create({ type: 'negative', message: getErrorMessage(err, 'Failed to test connection') })
    testResult.value = null
  }
})

function testConnection() {
  if (!canTestConnection.value) return
  testLoading.value = true
  testConnectionMutation.mutate(form.value.connectionString, {
    onSettled: () => {
      testLoading.value = false
    }
  })
}

const parsePermissions = parseSqlPermissions

function submitForm() {
  const dataStoreId = form.value.dataStoreId

  if (!dataStoreId) {
    Notify.create({ type: 'negative', message: 'Please select a data store.' })
    return
  }

  emit('submit', {
    name: form.value.name,
    dataStoreId,
    connectionString: form.value.connectionString
  })
}
</script>

<style scoped>
.form-card {
  min-width: 500px;
  max-width: 600px;
}
</style>
