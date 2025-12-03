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
          @update:model-value="onDataStoreChange"
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

        <!-- Authentication Mode Toggle -->
        <div class="q-mt-md">
          <div class="text-subtitle2 q-mb-sm">Authentication Method</div>
          <q-btn-toggle
            v-model="authMode"
            spread
            no-caps
            :disable="loading || testLoading"
            :options="[
              { label: 'Connection String', value: 'connectionString' },
              { label: 'Use Account', value: 'account' }
            ]"
            toggle-color="primary"
          />
        </div>

        <!-- Connection String Mode -->
        <q-input 
          v-if="authMode === 'connectionString'"
          v-model="form.connectionString" 
          label="Connection String" 
          :disable="loading || testLoading" 
          type="textarea"
          rows="3"
          required
          hint="SQL Server connection string (e.g., Server=myserver;Database=mydb;...)"
          class="q-mt-md"
        />

        <!-- Account Mode -->
        <template v-if="authMode === 'account'">
          <q-select 
            v-model="form.accountId" 
            :options="accountOptions" 
            label="Account" 
            :disable="loading || testLoading || !form.dataStoreId" 
            required
            hint="Select an account that targets this data store"
            option-label="label"
            option-value="value"
            emit-value
            map-options
            class="q-mt-md"
          >
            <template #option="scope">
              <q-item :clickable="true" @click="scope.toggleOption(scope.opt)">
                <q-item-section>
                  <q-item-label>{{ scope.opt.label }}</q-item-label>
                  <q-item-label v-if="scope.opt.description" caption>
                    {{ scope.opt.description }}
                  </q-item-label>
                </q-item-section>
                <q-item-section side v-if="scope.opt.hasSecretProvider">
                  <q-badge color="green" label="Has Secret Provider" />
                </q-item-section>
              </q-item>
            </template>

            <template #selected-item="scope">
              <div class="row items-center">
                <span>{{ scope.opt.label }}</span>
                <q-badge v-if="scope.opt.hasSecretProvider" color="green" label="Has Secret Provider" class="q-ml-sm" />
              </div>
            </template>

            <template #no-option>
              <q-item>
                <q-item-section class="text-grey">
                  <template v-if="!form.dataStoreId">
                    Select a data store first
                  </template>
                  <template v-else>
                    No accounts found for this data store
                  </template>
                </q-item-section>
              </q-item>
            </template>
          </q-select>

          <!-- Password Section for Account Mode -->
          <div v-if="form.accountId && selectedAccount" class="q-mt-md">
            <template v-if="selectedAccount.hasSecretProvider">
              <q-banner dense class="bg-blue-1 text-blue-9">
                <template #avatar>
                  <q-icon name="vpn_key" color="primary" />
                </template>
                Password will be retrieved from the linked Secret Provider.
              </q-banner>
            </template>
            <template v-else>
              <q-banner dense class="bg-orange-1 text-orange-9 q-mb-sm">
                <template #avatar>
                  <q-icon name="warning" color="orange" />
                </template>
                This account doesn't have a Secret Provider linked. Please enter the password manually.
              </q-banner>
              <q-input 
                v-model="form.manualPassword" 
                label="Password" 
                type="password"
                :disable="loading || testLoading" 
                required
                hint="Password for the SQL account (used for connection only, not stored)"
              />
            </template>
          </div>
        </template>

        <!-- Connection Test Results -->
        <div v-if="testResult" class="q-mt-md q-pa-md" :class="testResultClass">
          <div class="row items-center q-gutter-sm">
            <q-icon :name="testResult.isSuccessful ? 'check_circle' : 'error'" size="sm" />
            <div class="text-weight-medium">
              <template v-if="testResult.isSuccessful && isAccountTestPending">
                Ready to Save
              </template>
              <template v-else>
                {{ testResult.isSuccessful ? 'Connection Successful' : 'Connection Failed' }}
              </template>
            </div>
          </div>
          <div v-if="isAccountTestPending" class="q-mt-sm text-caption text-grey-7">
            Account-based connections are validated when saving. The server will build and test the connection using the selected account's credentials.
          </div>
          <div v-if="testResult.isSuccessful && testResult.permissions && !isAccountTestPending" class="q-mt-sm">
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
import { useAccounts } from '../../composables/useAccounts'
import { useFuseClient } from '../../composables/useFuseClient'
import { TestSqlConnection, SqlConnectionTestResult, SecretBindingKind, TargetKind } from '../../api/client'
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
const accountsStore = useAccounts()

type AuthMode = 'connectionString' | 'account'

const authMode = ref<AuthMode>('connectionString')

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

// Get accounts that target the selected data store
const accountOptions = computed(() => {
  if (!form.value.dataStoreId) return []
  
  const accounts = accountsStore.data.value ?? []
  return accounts
    .filter(acc => 
      acc.targetKind === TargetKind.DataStore && 
      acc.targetId === form.value.dataStoreId
    )
    .map(acc => ({
      label: acc.userName ?? acc.id!,
      value: acc.id,
      description: acc.authKind?.toString() ?? '',
      hasSecretProvider: acc.secretBinding?.kind === SecretBindingKind.AzureKeyVault && 
                         acc.secretBinding?.azureKeyVault != null
    }))
})

// Get the currently selected account for UI display
const selectedAccount = computed(() => {
  if (!form.value.accountId) return null
  return accountOptions.value.find(opt => opt.value === form.value.accountId) ?? null
})

const form = ref<{
  name: string
  dataStoreId: string | null
  connectionString: string
  accountId: string | null
  manualPassword: string
}>({
  name: '',
  dataStoreId: null,
  connectionString: '',
  accountId: null,
  manualPassword: ''
})

const testResult = ref<SqlConnectionTestResult | null>(null)
const testLoading = ref(false)

watch(() => props.initialValue, (val) => {
  if (val) {
    form.value = {
      name: val.name ?? '',
      dataStoreId: val.dataStoreId ?? null,
      connectionString: '', // Don't show stored connection string for security
      accountId: val.accountId ?? null,
      manualPassword: ''
    }
    // Set auth mode based on whether account was used
    authMode.value = val.accountId ? 'account' : 'connectionString'
    testResult.value = null // Reset test result on edit
  } else {
    form.value = {
      name: '',
      dataStoreId: null,
      connectionString: '',
      accountId: null,
      manualPassword: ''
    }
    authMode.value = 'connectionString'
    testResult.value = null
  }
}, { immediate: true })

function onDataStoreChange() {
  // Clear account selection when data store changes
  form.value.accountId = null
  form.value.manualPassword = ''
  testResult.value = null
}

const canTestConnection = computed(() => {
  if (authMode.value === 'connectionString') {
    return !!form.value.connectionString && form.value.connectionString.length > 0
  } else {
    // Account mode - can test if account is selected and either has secret provider or manual password
    if (!form.value.accountId) return false
    if (selectedAccount.value?.hasSecretProvider) return true
    return !!form.value.manualPassword && form.value.manualPassword.length > 0
  }
})

const hasSuccessfulTest = computed(() => 
  testResult.value?.isSuccessful === true || props.mode === 'edit'
)

// Track if we're using account-based auth and awaiting server-side validation
const isAccountTestPending = computed(() => 
  authMode.value === 'account' && testResult.value?.isSuccessful === true && !testResult.value?.permissions
)

const testResultClass = computed(() => ({
  'bg-green-1': testResult.value?.isSuccessful,
  'bg-red-1': !testResult.value?.isSuccessful,
  'text-positive': testResult.value?.isSuccessful,
  'text-negative': !testResult.value?.isSuccessful,
  'rounded-borders': true,
  'bg-blue-1': isAccountTestPending.value,
  'text-blue-9': isAccountTestPending.value
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
  
  if (authMode.value === 'connectionString') {
    testConnectionMutation.mutate(form.value.connectionString, {
      onSettled: () => {
        testLoading.value = false
      }
    })
  } else {
    // For account mode, the connection is validated server-side when saving
    // Mark as ready to proceed - the test result box will explain this to users
    testResult.value = {
      isSuccessful: true,
      permissions: undefined,
      errorMessage: undefined
    } as SqlConnectionTestResult
    testLoading.value = false
  }
}

const parsePermissions = parseSqlPermissions

function submitForm() {
  const dataStoreId = form.value.dataStoreId

  if (!dataStoreId) {
    Notify.create({ type: 'negative', message: 'Please select a data store.' })
    return
  }

  if (authMode.value === 'connectionString' && !form.value.connectionString) {
    Notify.create({ type: 'negative', message: 'Please enter a connection string.' })
    return
  }

  if (authMode.value === 'account' && !form.value.accountId) {
    Notify.create({ type: 'negative', message: 'Please select an account.' })
    return
  }

  emit('submit', {
    name: form.value.name,
    dataStoreId,
    connectionString: authMode.value === 'connectionString' ? form.value.connectionString : undefined,
    accountId: authMode.value === 'account' ? form.value.accountId : undefined,
    manualPassword: authMode.value === 'account' && !selectedAccount.value?.hasSecretProvider 
      ? form.value.manualPassword 
      : undefined
  })
}
</script>

<style scoped>
.form-card {
  min-width: 500px;
  max-width: 650px;
}
</style>
