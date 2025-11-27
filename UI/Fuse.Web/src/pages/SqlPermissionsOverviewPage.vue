<template>
  <div class="page-container">
    <div class="page-header">
      <div>
        <div class="row items-center q-gutter-sm">
          <q-btn 
            flat 
            round 
            dense 
            icon="arrow_back" 
            @click="router.push({ name: 'sqlIntegrations' })"
          />
          <h1>{{ data?.integrationName ?? 'SQL Permissions Overview' }}</h1>
        </div>
        <p class="subtitle">Permission drift dashboard for accounts associated with this SQL integration.</p>
      </div>
      <q-btn 
        flat 
        dense 
        icon="refresh" 
        label="Refresh"
        :loading="isFetching"
        @click="() => refetch()"
      />
    </div>

    <!-- Loading State -->
    <div v-if="isLoading" class="q-pa-xl text-center">
      <q-spinner color="primary" size="3em" />
      <div class="text-grey-7 q-mt-md">Loading permissions overview...</div>
    </div>

    <!-- Error State -->
    <q-banner v-else-if="error" dense class="bg-red-1 text-negative q-mb-md">
      <template #avatar>
        <q-icon name="error" color="negative" />
      </template>
      Unable to load permissions overview. Please try again.
      <template #action>
        <q-btn flat label="Retry" @click="() => refetch()" />
      </template>
    </q-banner>

    <!-- Data Display -->
    <template v-else-if="data">
      <!-- Error Message from integration -->
      <q-banner v-if="data.errorMessage" dense class="bg-orange-1 text-orange-9 q-mb-md">
        <template #avatar>
          <q-icon name="warning" color="orange" />
        </template>
        {{ data.errorMessage }}
      </q-banner>

      <!-- Summary Section -->
      <div class="summary-section q-mb-md">
        <q-card flat bordered class="summary-card">
          <q-card-section class="row q-gutter-md">
            <div class="summary-item">
              <div class="summary-value">{{ data.summary?.totalAccounts ?? 0 }}</div>
              <div class="summary-label">Total Accounts</div>
            </div>
            <q-separator vertical />
            <div class="summary-item status-in-sync">
              <div class="summary-value">{{ data.summary?.inSyncCount ?? 0 }}</div>
              <div class="summary-label">In Sync</div>
            </div>
            <q-separator vertical />
            <div class="summary-item status-drift">
              <div class="summary-value">{{ data.summary?.driftCount ?? 0 }}</div>
              <div class="summary-label">Drift Detected</div>
            </div>
            <q-separator vertical />
            <div class="summary-item status-missing">
              <div class="summary-value">{{ data.summary?.missingPrincipalCount ?? 0 }}</div>
              <div class="summary-label">Missing Principal</div>
            </div>
            <q-separator vertical />
            <div class="summary-item status-error">
              <div class="summary-value">{{ data.summary?.errorCount ?? 0 }}</div>
              <div class="summary-label">Errors</div>
            </div>
          </q-card-section>
        </q-card>
      </div>

      <!-- Bulk Actions Section -->
      <div v-if="hasResolvableAccounts && canBulkResolve" class="q-mb-md">
        <q-card flat bordered>
          <q-card-section class="row items-center q-gutter-md">
            <div class="col">
              <div class="text-subtitle2">Bulk Actions</div>
              <div class="text-caption text-grey-7">
                Apply all pending changes at once. Creates missing accounts and resolves drift.
              </div>
            </div>
            <q-btn
              color="primary"
              icon="auto_fix_high"
              label="Bulk Resolve All"
              :loading="isBulkResolving"
              @click="openBulkResolveDialog"
            >
              <q-tooltip>
                Create {{ resolvableMissingCount }} missing account(s) and resolve {{ resolvableDriftCount }} drift(s)
              </q-tooltip>
            </q-btn>
          </q-card-section>
        </q-card>
      </div>

      <!-- Accounts Table -->
      <q-card class="content-card">
        <q-card-section>
          <div class="text-h6 q-mb-sm">Account Permissions</div>
          <div class="row q-gutter-sm q-mb-md">
            <q-btn-toggle
              v-model="statusFilter"
              flat
              dense
              toggle-color="primary"
              :options="filterOptions"
            />
          </div>
          <q-table
            flat
            bordered
            :rows="filteredAccounts"
            :columns="accountColumns"
            row-key="accountId"
            :pagination="{ rowsPerPage: 15 }"
          >
            <template #body-cell-accountName="props">
              <q-td :props="props">
                <router-link 
                  :to="{ name: 'accountEdit', params: { id: props.row.accountId } }"
                  class="account-link"
                >
                  {{ props.row.accountName ?? '—' }}
                </router-link>
              </q-td>
            </template>
            <template #body-cell-status="props">
              <q-td :props="props">
                <q-badge 
                  :color="getStatusColor(props.row.status)"
                  :label="getStatusLabel(props.row.status)"
                />
              </q-td>
            </template>
            <template #body-cell-permissions="props">
              <q-td :props="props">
                <div v-if="props.row.permissionComparisons?.length" class="permission-summary">
                  <template v-if="hasMissingPermissions(props.row)">
                    <q-icon name="remove_circle" color="negative" size="xs" />
                    <span class="text-negative text-caption q-ml-xs">
                      {{ countMissingPermissions(props.row) }} missing
                    </span>
                  </template>
                  <template v-if="hasExtraPermissions(props.row)">
                    <q-icon name="add_circle" color="orange" size="xs" class="q-ml-sm" />
                    <span class="text-orange text-caption q-ml-xs">
                      {{ countExtraPermissions(props.row) }} extra
                    </span>
                  </template>
                  <template v-if="!hasMissingPermissions(props.row) && !hasExtraPermissions(props.row) && props.row.status === 'InSync'">
                    <q-icon name="check_circle" color="positive" size="xs" />
                    <span class="text-positive text-caption q-ml-xs">All in sync</span>
                  </template>
                </div>
                <span v-else class="text-grey">—</span>
              </q-td>
            </template>
            <template #body-cell-actions="props">
              <q-td :props="props" class="text-right">
                <q-btn 
                  v-if="props.row.status === SyncStatus.MissingPrincipal && hasCreatePermission && canResolve"
                  flat 
                  dense 
                  size="sm"
                  color="positive"
                  label="Create Account"
                  :loading="isCreatingAccount === props.row.accountId"
                  @click="openCreateAccountDialog(props.row)"
                >
                  <q-tooltip>Create SQL login and user</q-tooltip>
                </q-btn>
                <q-btn 
                  v-if="props.row.status === SyncStatus.DriftDetected && hasWritePermission && canResolve"
                  flat 
                  dense 
                  size="sm"
                  color="primary"
                  label="Resolve"
                  :loading="isResolvingAccount === props.row.accountId"
                  @click="openResolveDialog(props.row)"
                >
                  <q-tooltip>Resolve permission drift</q-tooltip>
                </q-btn>
                <q-btn 
                  flat 
                  dense 
                  round 
                  icon="visibility" 
                  color="primary"
                  @click="router.push({ name: 'accountEdit', params: { id: props.row.accountId } })"
                >
                  <q-tooltip>View account details</q-tooltip>
                </q-btn>
              </q-td>
            </template>
            <template #no-data>
              <div class="q-pa-md text-grey-7">
                <template v-if="statusFilter !== 'all'">
                  No accounts match the selected filter.
                </template>
                <template v-else>
                  No accounts are associated with this SQL integration.
                </template>
              </div>
            </template>
          </q-table>
        </q-card-section>
      </q-card>

      <!-- Orphan Principals Table (if any) -->
      <q-card v-if="data.orphanPrincipals?.length" class="content-card q-mt-md">
        <q-card-section>
          <div class="text-h6 q-mb-sm">
            <q-icon name="help_outline" class="q-mr-sm" />
            Orphan Principals
          </div>
          <div class="text-caption text-grey-7 q-mb-md">
            SQL principals found that are not mapped to any Fuse account.
          </div>
          <q-table
            flat
            bordered
            :rows="data.orphanPrincipals"
            :columns="orphanColumns"
            row-key="principalName"
            :pagination="{ rowsPerPage: 10 }"
          >
            <template #body-cell-permissions="props">
              <q-td :props="props">
                <div v-if="props.row.actualPermissions?.length" class="tag-list">
                  <template v-for="grant in props.row.actualPermissions" :key="grant.database">
                    <q-badge
                      v-for="priv in grant.privileges"
                      :key="`${grant.database}-${priv}`"
                      outline
                      color="secondary"
                      :label="`${grant.database ?? 'default'}:${priv}`"
                    />
                  </template>
                </div>
                <span v-else class="text-grey">—</span>
              </q-td>
            </template>
            <template #no-data>
              <div class="q-pa-md text-grey-7">
                No orphan principals detected.
              </div>
            </template>
          </q-table>
        </q-card-section>
      </q-card>
    </template>

    <!-- Resolve Drift Confirmation Dialog -->
    <q-dialog v-model="showResolveDialog" persistent>
      <q-card style="min-width: 400px">
        <q-card-section class="row items-center">
          <q-icon name="sync" color="primary" size="2em" class="q-mr-sm" />
          <span class="text-h6">Resolve Permission Drift</span>
        </q-card-section>

        <q-card-section>
          <p v-if="selectedAccount">
            Apply changes to align 
            <strong>{{ selectedAccount.principalName }}</strong>'s 
            SQL permissions with Fuse configuration?
          </p>
          <div v-if="selectedAccount" class="q-mt-md">
            <div v-if="countMissingPermissions(selectedAccount) > 0" class="text-caption">
              <q-icon name="add_circle" color="positive" size="xs" />
              {{ countMissingPermissions(selectedAccount) }} permission(s) will be GRANTED
            </div>
            <div v-if="countExtraPermissions(selectedAccount) > 0" class="text-caption">
              <q-icon name="remove_circle" color="negative" size="xs" />
              {{ countExtraPermissions(selectedAccount) }} permission(s) will be REVOKED
            </div>
          </div>
        </q-card-section>

        <q-card-actions align="right">
          <q-btn flat label="Cancel" color="grey" v-close-popup :disable="isResolving" />
          <q-btn 
            flat 
            label="Resolve Drift" 
            color="primary" 
            :loading="isResolving"
            @click="handleResolveDrift" 
          />
        </q-card-actions>
      </q-card>
    </q-dialog>

    <!-- Result Dialog -->
    <q-dialog v-model="showResultDialog">
      <q-card style="min-width: 400px">
        <q-card-section class="row items-center">
          <q-icon 
            :name="resolveResult?.success ? 'check_circle' : 'error'" 
            :color="resolveResult?.success ? 'positive' : 'negative'" 
            size="2em" 
            class="q-mr-sm" 
          />
          <span class="text-h6">
            {{ resolveResult?.success ? 'Drift Resolved' : 'Resolution Failed' }}
          </span>
        </q-card-section>

        <q-card-section v-if="resolveResult">
          <div v-if="resolveResult.operations && resolveResult.operations.length > 0" class="q-mb-md">
            <div class="text-subtitle2 q-mb-sm">Operations performed:</div>
            <div 
              v-for="(op, index) in resolveResult.operations" 
              :key="index"
              class="text-caption q-mb-xs"
            >
              <q-icon 
                :name="op.success ? 'check' : 'close'" 
                :color="op.success ? 'positive' : 'negative'" 
                size="xs" 
              />
              {{ op.operationType }} {{ op.privilege }} 
              <span v-if="op.database">on {{ op.database }}</span>
              <span v-if="!op.success" class="text-negative"> - {{ op.errorMessage }}</span>
            </div>
          </div>
          <div v-if="resolveResult.errorMessage" class="text-negative">
            {{ resolveResult.errorMessage }}
          </div>
          <div v-if="resolveResult.updatedStatus" class="q-mt-md">
            <span class="text-subtitle2">Updated status: </span>
            <q-badge 
              :color="getStatusColor(resolveResult.updatedStatus.status)"
              :label="getStatusLabel(resolveResult.updatedStatus.status)"
            />
          </div>
        </q-card-section>

        <q-card-actions align="right">
          <q-btn flat label="Close" color="primary" v-close-popup />
        </q-card-actions>
      </q-card>
    </q-dialog>

    <!-- Create SQL Account Dialog -->
    <q-dialog v-model="showCreateAccountDialog" persistent>
      <q-card style="min-width: 450px">
        <q-card-section class="row items-center">
          <q-icon name="person_add" color="positive" size="2em" class="q-mr-sm" />
          <span class="text-h6">Create SQL Account</span>
        </q-card-section>

        <q-card-section>
          <p v-if="createAccountSelectedAccount">
            Create SQL login and user for 
            <strong>{{ createAccountSelectedAccount.principalName }}</strong>?
          </p>
          
          <div class="q-mt-md">
            <div class="text-subtitle2 q-mb-sm">Password Source</div>
            
            <q-option-group
              v-model="selectedPasswordSource"
              :options="[
                { 
                  label: 'Retrieve from Secret Provider', 
                  value: PasswordSource.SecretProvider,
                  disable: createAccountSelectedAccount && !hasSecretProvider(createAccountSelectedAccount.accountId!)
                },
                { label: 'Enter password manually', value: PasswordSource.Manual }
              ]"
              color="primary"
            />

            <q-banner 
              v-if="selectedPasswordSource === PasswordSource.SecretProvider && createAccountSelectedAccount && !hasSecretProvider(createAccountSelectedAccount.accountId!)"
              dense 
              class="bg-orange-1 text-orange-9 q-mt-md"
            >
              <template #avatar>
                <q-icon name="warning" color="orange" />
              </template>
              This account is not linked to a Secret Provider.
            </q-banner>

            <q-banner 
              v-if="selectedPasswordSource === PasswordSource.SecretProvider && createAccountSelectedAccount && hasSecretProvider(createAccountSelectedAccount.accountId!)"
              dense 
              class="bg-blue-1 text-blue-9 q-mt-md"
            >
              <template #avatar>
                <q-icon name="info" color="primary" />
              </template>
              Password will be retrieved from the linked Secret Provider.
            </q-banner>
            
            <q-input
              v-if="selectedPasswordSource === PasswordSource.Manual"
              v-model="manualPassword"
              type="password"
              label="Password"
              outlined
              dense
              class="q-mt-md"
              :rules="[val => !!val || 'Password is required']"
            >
              <template #hint>
                <span class="text-caption text-grey-7">
                  <q-icon name="security" size="xs" class="q-mr-xs" />
                  Fuse does not store passwords. This will only be used once to create the SQL account.
                </span>
              </template>
            </q-input>
          </div>
        </q-card-section>

        <q-card-actions align="right">
          <q-btn flat label="Cancel" color="grey" v-close-popup :disable="isCreating" />
          <q-btn 
            flat 
            label="Create Account" 
            color="positive" 
            :loading="isCreating"
            :disable="isCreateAccountDisabled"
            @click="handleCreateAccount" 
          />
        </q-card-actions>
      </q-card>
    </q-dialog>

    <!-- Create Account Result Dialog -->
    <q-dialog v-model="showCreateResultDialog">
      <q-card style="min-width: 400px">
        <q-card-section class="row items-center">
          <q-icon 
            :name="createAccountResult?.success ? 'check_circle' : 'error'" 
            :color="createAccountResult?.success ? 'positive' : 'negative'" 
            size="2em" 
            class="q-mr-sm" 
          />
          <span class="text-h6">
            {{ createAccountResult?.success ? 'Account Created' : 'Creation Failed' }}
          </span>
        </q-card-section>

        <q-card-section v-if="createAccountResult">
          <div v-if="createAccountResult.success" class="q-mb-md">
            <span class="text-subtitle2">Password source: </span>
            <q-badge 
              outline
              color="primary"
              :label="getPasswordSourceLabel(createAccountResult.passwordSource)"
            />
          </div>

          <div v-if="createAccountResult.operations && createAccountResult.operations.length > 0" class="q-mb-md">
            <div class="text-subtitle2 q-mb-sm">Operations performed:</div>
            <div 
              v-for="(op, index) in createAccountResult.operations" 
              :key="index"
              class="text-caption q-mb-xs"
            >
              <q-icon 
                :name="op.success ? 'check' : 'close'" 
                :color="op.success ? 'positive' : 'negative'" 
                size="xs" 
              />
              {{ op.operationType }} 
              <span v-if="op.database">in {{ op.database }}</span>
              <span v-if="!op.success" class="text-negative"> - {{ op.errorMessage }}</span>
            </div>
          </div>
          <div v-if="createAccountResult.errorMessage" class="text-negative">
            {{ createAccountResult.errorMessage }}
          </div>
          <div v-if="createAccountResult.updatedStatus" class="q-mt-md">
            <span class="text-subtitle2">Account status: </span>
            <q-badge 
              :color="getStatusColor(createAccountResult.updatedStatus.status)"
              :label="getStatusLabel(createAccountResult.updatedStatus.status)"
            />
            <div v-if="createAccountResult.success && createAccountResult.updatedStatus.status === SyncStatus.DriftDetected" class="text-caption text-orange q-mt-sm">
              <q-icon name="info" size="xs" />
              Account created but permissions need to be applied. Use "Resolve" to apply expected permissions.
            </div>
          </div>
        </q-card-section>

        <q-card-actions align="right">
          <q-btn flat label="Close" color="primary" v-close-popup />
        </q-card-actions>
      </q-card>
    </q-dialog>

    <!-- Bulk Resolve Confirmation Dialog -->
    <q-dialog v-model="showBulkResolveDialog" persistent>
      <q-card style="min-width: 500px">
        <q-card-section class="row items-center">
          <q-icon name="auto_fix_high" color="primary" size="2em" class="q-mr-sm" />
          <span class="text-h6">Bulk Resolve All</span>
        </q-card-section>

        <q-card-section>
          <p>
            This will apply all pending changes to align SQL permissions with Fuse configuration.
          </p>
          
          <div class="q-mt-md">
            <div v-if="resolvableMissingCount > 0" class="text-caption q-mb-sm">
              <q-icon name="person_add" color="positive" size="xs" />
              {{ resolvableMissingCount }} account(s) will be created
              <div class="text-grey-7 q-ml-lg">
                Only accounts linked to a Secret Provider will be processed.
              </div>
            </div>
            <div v-if="resolvableDriftCount > 0" class="text-caption q-mb-sm">
              <q-icon name="sync" color="primary" size="xs" />
              {{ resolvableDriftCount }} drift(s) will be resolved
            </div>
            <div v-if="skippedAccountsCount > 0" class="text-caption text-orange q-mb-sm">
              <q-icon name="warning" color="orange" size="xs" />
              {{ skippedAccountsCount }} account(s) will be skipped (no Secret Provider linked)
            </div>
          </div>

          <q-banner dense class="bg-blue-1 text-blue-9 q-mt-md">
            <template #avatar>
              <q-icon name="info" color="primary" />
            </template>
            Passwords will be retrieved from linked Secret Providers. 
            Accounts without a Secret Provider link will be skipped.
          </q-banner>
        </q-card-section>

        <q-card-actions align="right">
          <q-btn flat label="Cancel" color="grey" v-close-popup :disable="isBulkResolving" />
          <q-btn 
            flat 
            label="Bulk Resolve" 
            color="primary" 
            :loading="isBulkResolving"
            @click="handleBulkResolve" 
          />
        </q-card-actions>
      </q-card>
    </q-dialog>

    <!-- Bulk Resolve Result Dialog -->
    <q-dialog v-model="showBulkResolveResultDialog">
      <q-card style="min-width: 550px; max-width: 700px">
        <q-card-section class="row items-center">
          <q-icon 
            :name="bulkResolveResult?.success ? 'check_circle' : 'warning'" 
            :color="bulkResolveResult?.success ? 'positive' : 'orange'" 
            size="2em" 
            class="q-mr-sm" 
          />
          <span class="text-h6">
            {{ bulkResolveResult?.success ? 'Bulk Resolve Complete' : 'Bulk Resolve Completed with Issues' }}
          </span>
        </q-card-section>

        <q-card-section v-if="bulkResolveResult">
          <!-- Summary -->
          <div class="q-mb-md">
            <div class="text-subtitle2 q-mb-sm">Summary</div>
            <div class="row q-gutter-md">
              <div class="col-auto">
                <q-badge color="primary" :label="`${bulkResolveResult.summary?.totalProcessed ?? 0} Processed`" />
              </div>
              <div v-if="bulkResolveResult.summary?.accountsCreated" class="col-auto">
                <q-badge color="positive" :label="`${bulkResolveResult.summary.accountsCreated} Created`" />
              </div>
              <div v-if="bulkResolveResult.summary?.driftsResolved" class="col-auto">
                <q-badge color="info" :label="`${bulkResolveResult.summary.driftsResolved} Resolved`" />
              </div>
              <div v-if="bulkResolveResult.summary?.skipped" class="col-auto">
                <q-badge color="orange" :label="`${bulkResolveResult.summary.skipped} Skipped`" />
              </div>
              <div v-if="bulkResolveResult.summary?.failed" class="col-auto">
                <q-badge color="negative" :label="`${bulkResolveResult.summary.failed} Failed`" />
              </div>
            </div>
          </div>

          <!-- Results Table -->
          <div v-if="bulkResolveResult.results && bulkResolveResult.results.length > 0" class="q-mb-md">
            <div class="text-subtitle2 q-mb-sm">Details</div>
            <q-table
              flat
              bordered
              dense
              :rows="bulkResolveResult.results"
              :columns="bulkResultColumns"
              row-key="accountId"
              :pagination="{ rowsPerPage: 10 }"
            >
              <template #body-cell-status="props">
                <q-td :props="props">
                  <q-icon 
                    :name="props.row.success ? 'check_circle' : (props.row.errorMessage?.includes('skipped') ? 'warning' : 'error')" 
                    :color="props.row.success ? 'positive' : (props.row.errorMessage?.includes('skipped') ? 'orange' : 'negative')" 
                    size="sm" 
                  />
                </q-td>
              </template>
              <template #body-cell-message="props">
                <q-td :props="props">
                  <span v-if="props.row.success" class="text-positive">Success</span>
                  <span v-else :class="props.row.errorMessage?.includes('skipped') ? 'text-orange' : 'text-negative'">
                    {{ props.row.errorMessage ?? 'Failed' }}
                  </span>
                </q-td>
              </template>
            </q-table>
          </div>

          <div v-if="bulkResolveResult.errorMessage" class="text-negative">
            {{ bulkResolveResult.errorMessage }}
          </div>
        </q-card-section>

        <q-card-actions align="right">
          <q-btn flat label="Close" color="primary" v-close-popup />
        </q-card-actions>
      </q-card>
    </q-dialog>
  </div>
</template>

<script setup lang="ts">
import { computed, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import type { QTableColumn } from 'quasar'
import { useSqlPermissionsOverview } from '../composables/useSqlPermissionsOverview'
import { useResolveDrift } from '../composables/useResolveDrift'
import { useCreateSqlAccount } from '../composables/useCreateSqlAccount'
import { useBulkResolve } from '../composables/useBulkResolve'
import { useSqlIntegrations } from '../composables/useSqlIntegrations'
import { useAccounts } from '../composables/useAccounts'
import { useFuseStore } from '../stores/FuseStore'
import { SyncStatus, SecurityLevel, ResolveDriftResponse, CreateSqlAccountResponse, BulkResolveResponse, PasswordSource, PasswordSourceUsed, BulkPasswordSource, SecretBindingKind, type SqlAccountPermissionsStatus, type SqlOrphanPrincipal, type BulkResolveAccountResult } from '../api/client'

const route = useRoute()
const router = useRouter()
const fuseStore = useFuseStore()

const integrationId = computed(() => route.params.id as string)
const { data, isLoading, isFetching, error, refetch } = useSqlPermissionsOverview(integrationId)
const { data: sqlIntegrations } = useSqlIntegrations()
const { data: accounts } = useAccounts()
const { mutateAsync: resolveDrift, isPending: isResolving } = useResolveDrift()
const { mutateAsync: createSqlAccount, isPending: isCreating } = useCreateSqlAccount()
const { mutateAsync: bulkResolve, isPending: isBulkResolving } = useBulkResolve()

// Resolve drift dialog state
const showResolveDialog = ref(false)
const showResultDialog = ref(false)
const selectedAccount = ref<SqlAccountPermissionsStatus | null>(null)
const resolveResult = ref<ResolveDriftResponse | null>(null)
const isResolvingAccount = ref<string | null>(null)

// Create account dialog state
const showCreateAccountDialog = ref(false)
const showCreateResultDialog = ref(false)
const createAccountSelectedAccount = ref<SqlAccountPermissionsStatus | null>(null)
const createAccountResult = ref<CreateSqlAccountResponse | null>(null)
const isCreatingAccount = ref<string | null>(null)
const selectedPasswordSource = ref<PasswordSource>(PasswordSource.Manual)
const manualPassword = ref('')

// Bulk resolve dialog state
const showBulkResolveDialog = ref(false)
const showBulkResolveResultDialog = ref(false)
const bulkResolveResult = ref<BulkResolveResponse | null>(null)

// Check if integration has write permission
// SqlPermissions is a flags enum serialized as comma-separated string (e.g., "Read, Write")
const hasWritePermission = computed(() => {
  const integration = sqlIntegrations.value?.find(i => i.id === integrationId.value)
  if (!integration?.permissions) return false
  const permStr = String(integration.permissions)
  return permStr.includes('Write')
})

// Check if integration has create permission
const hasCreatePermission = computed(() => {
  const integration = sqlIntegrations.value?.find(i => i.id === integrationId.value)
  if (!integration?.permissions) return false
  const permStr = String(integration.permissions)
  return permStr.includes('Create')
})

// Check if user can resolve drift based on security level
// Level None: anyone can resolve
// Level above None: only logged-in admins can resolve
const canResolve = computed(() => {
  if (fuseStore.securityLevel === SecurityLevel.None) {
    return true
  }
  // For any security level above None, require admin role
  return fuseStore.canModify
})

// Check if user can bulk resolve (needs both write and create permissions)
const canBulkResolve = computed(() => {
  return canResolve.value && hasWritePermission.value && hasCreatePermission.value
})

// Check if there are any accounts that can be bulk resolved
const hasResolvableAccounts = computed(() => {
  return resolvableMissingCount.value > 0 || resolvableDriftCount.value > 0
})

// Count of accounts with missing principals that can be resolved (have Secret Provider)
const resolvableMissingCount = computed(() => {
  if (!data.value?.accounts) return 0
  return data.value.accounts.filter(a => 
    a.status === SyncStatus.MissingPrincipal && 
    a.accountId && 
    hasSecretProvider(a.accountId)
  ).length
})

// Count of accounts with drift detected
const resolvableDriftCount = computed(() => {
  return data.value?.summary?.driftCount ?? 0
})

// Count of accounts that will be skipped (missing principals without Secret Provider)
const skippedAccountsCount = computed(() => {
  if (!data.value?.accounts) return 0
  return data.value.accounts.filter(a => 
    a.status === SyncStatus.MissingPrincipal && 
    a.accountId && 
    !hasSecretProvider(a.accountId)
  ).length
})

// Check if create account button should be disabled
const isCreateAccountDisabled = computed(() => {
  // Manual password is required when using manual source
  if (selectedPasswordSource.value !== PasswordSource.SecretProvider && !manualPassword.value) {
    return true
  }
  // Secret provider must be linked when using secret provider source
  if (selectedPasswordSource.value === PasswordSource.SecretProvider && 
      createAccountSelectedAccount.value && 
      !hasSecretProvider(createAccountSelectedAccount.value.accountId!)) {
    return true
  }
  return false
})

const statusFilter = ref<string>('all')

const filterOptions = [
  { label: 'All', value: 'all' },
  { label: 'In Sync', value: 'InSync' },
  { label: 'Drift', value: 'DriftDetected' },
  { label: 'Missing', value: 'MissingPrincipal' },
  { label: 'Error', value: 'Error' }
]

const filteredAccounts = computed(() => {
  const accounts = data.value?.accounts ?? []
  if (statusFilter.value === 'all') {
    return accounts
  }
  return accounts.filter(a => a.status === statusFilter.value)
})

const accountColumns: QTableColumn<SqlAccountPermissionsStatus>[] = [
  { name: 'accountName', label: 'Account', field: 'accountName', align: 'left', sortable: true },
  { name: 'principalName', label: 'SQL Principal', field: 'principalName', align: 'left', sortable: true },
  { name: 'status', label: 'Status', field: 'status', align: 'left', sortable: true },
  { name: 'permissions', label: 'Permission Summary', field: (row) => row.accountId, align: 'left' },
  { name: 'actions', label: '', field: (row) => row.accountId, align: 'right' }
]

const orphanColumns: QTableColumn<SqlOrphanPrincipal>[] = [
  { name: 'principalName', label: 'Principal Name', field: 'principalName', align: 'left', sortable: true },
  { name: 'permissions', label: 'Actual Permissions', field: (row) => row.principalName, align: 'left' }
]

function getStatusColor(status?: SyncStatus): string {
  switch (status) {
    case SyncStatus.InSync:
      return 'positive'
    case SyncStatus.DriftDetected:
      return 'warning'
    case SyncStatus.MissingPrincipal:
      return 'orange'
    case SyncStatus.Error:
      return 'negative'
    case SyncStatus.NotApplicable:
      return 'grey'
    default:
      return 'grey'
  }
}

function getStatusLabel(status?: SyncStatus): string {
  switch (status) {
    case SyncStatus.InSync:
      return 'In Sync'
    case SyncStatus.DriftDetected:
      return 'Drift'
    case SyncStatus.MissingPrincipal:
      return 'Missing Principal'
    case SyncStatus.Error:
      return 'Error'
    case SyncStatus.NotApplicable:
      return 'N/A'
    default:
      return 'Unknown'
  }
}

function hasMissingPermissions(account: SqlAccountPermissionsStatus): boolean {
  return account.permissionComparisons?.some(c => (c.missingPrivileges?.length ?? 0) > 0) ?? false
}

function hasExtraPermissions(account: SqlAccountPermissionsStatus): boolean {
  return account.permissionComparisons?.some(c => (c.extraPrivileges?.length ?? 0) > 0) ?? false
}

function countMissingPermissions(account: SqlAccountPermissionsStatus): number {
  return account.permissionComparisons?.reduce((sum, c) => sum + (c.missingPrivileges?.length ?? 0), 0) ?? 0
}

function countExtraPermissions(account: SqlAccountPermissionsStatus): number {
  return account.permissionComparisons?.reduce((sum, c) => sum + (c.extraPrivileges?.length ?? 0), 0) ?? 0
}

function openResolveDialog(account: SqlAccountPermissionsStatus) {
  selectedAccount.value = account
  showResolveDialog.value = true
}

async function handleResolveDrift() {
  if (!selectedAccount.value?.accountId) return
  
  isResolvingAccount.value = selectedAccount.value.accountId
  
  try {
    const result = await resolveDrift({
      integrationId: integrationId.value,
      accountId: selectedAccount.value.accountId
    })
    
    resolveResult.value = result
    showResolveDialog.value = false
    showResultDialog.value = true
  } catch (err: any) {
    // Create an error response object
    const errorResponse = new ResolveDriftResponse()
    errorResponse.accountId = selectedAccount.value.accountId
    errorResponse.principalName = selectedAccount.value.principalName
    errorResponse.success = false
    errorResponse.operations = []
    
    // Check for 401 Unauthorized error
    if (err?.status === 401) {
      errorResponse.errorMessage = 'Authentication required. Please log in as an admin to resolve permission drift.'
    } else {
      errorResponse.errorMessage = err?.message || 'An error occurred while resolving drift.'
    }
    
    resolveResult.value = errorResponse
    showResolveDialog.value = false
    showResultDialog.value = true
  } finally {
    isResolvingAccount.value = null
  }
}

// Helper to get account secret binding info
function getAccountSecretBinding(accountId: string) {
  const account = accounts.value?.find(a => a.id === accountId)
  return account?.secretBinding
}

// Check if account has a secret provider linked
function hasSecretProvider(accountId: string): boolean {
  const binding = getAccountSecretBinding(accountId)
  return binding?.kind === SecretBindingKind.AzureKeyVault && !!binding.azureKeyVault
}

function openCreateAccountDialog(account: SqlAccountPermissionsStatus) {
  createAccountSelectedAccount.value = account
  manualPassword.value = ''
  
  // Default to SecretProvider if available, otherwise Manual
  if (account.accountId && hasSecretProvider(account.accountId)) {
    selectedPasswordSource.value = PasswordSource.SecretProvider
  } else {
    selectedPasswordSource.value = PasswordSource.Manual
  }
  
  showCreateAccountDialog.value = true
}

async function handleCreateAccount() {
  if (!createAccountSelectedAccount.value?.accountId) return
  
  isCreatingAccount.value = createAccountSelectedAccount.value.accountId
  
  try {
    const result = await createSqlAccount({
      integrationId: integrationId.value,
      accountId: createAccountSelectedAccount.value.accountId,
      passwordSource: selectedPasswordSource.value,
      password: selectedPasswordSource.value === PasswordSource.SecretProvider ? undefined : manualPassword.value
    })
    
    createAccountResult.value = result
    showCreateAccountDialog.value = false
    showCreateResultDialog.value = true
    
    // Clear the password immediately after use
    manualPassword.value = ''
    
    // Refetch the permissions overview
    await refetch()
  } catch (err: any) {
    // Create an error response object
    const errorResponse = new CreateSqlAccountResponse()
    errorResponse.accountId = createAccountSelectedAccount.value.accountId
    errorResponse.principalName = createAccountSelectedAccount.value.principalName
    errorResponse.success = false
    errorResponse.operations = []
    
    // Check for 401 Unauthorized error
    if (err?.status === 401) {
      errorResponse.errorMessage = 'Authentication required. Please log in as an admin to create SQL accounts.'
    } else {
      errorResponse.errorMessage = err?.message || 'An error occurred while creating the account.'
    }
    
    createAccountResult.value = errorResponse
    showCreateAccountDialog.value = false
    showCreateResultDialog.value = true
    manualPassword.value = ''
  } finally {
    isCreatingAccount.value = null
  }
}

// Helper to get password source label
function getPasswordSourceLabel(source?: PasswordSourceUsed): string {
  switch (source) {
    case PasswordSourceUsed.SecretProvider:
      return 'Secret Provider'
    case PasswordSourceUsed.Manual:
      return 'Manual Entry'
    case PasswordSourceUsed.NewSecret:
      return 'New Secret Created'
    default:
      return 'Unknown'
  }
}

// Bulk resolve table columns
const bulkResultColumns: QTableColumn<BulkResolveAccountResult>[] = [
  { name: 'status', label: '', field: (row) => row.accountId, align: 'center', style: 'width: 40px' },
  { name: 'accountName', label: 'Account', field: 'accountName', align: 'left' },
  { name: 'principalName', label: 'Principal', field: 'principalName', align: 'left' },
  { name: 'operationType', label: 'Operation', field: 'operationType', align: 'left' },
  { name: 'message', label: 'Result', field: (row) => row.accountId, align: 'left' }
]

function openBulkResolveDialog() {
  showBulkResolveDialog.value = true
}

async function handleBulkResolve() {
  try {
    const result = await bulkResolve({
      integrationId: integrationId.value,
      passwordSource: BulkPasswordSource.SecretProvider
    })
    
    bulkResolveResult.value = result
    showBulkResolveDialog.value = false
    showBulkResolveResultDialog.value = true
    
    // Refetch the permissions overview
    await refetch()
  } catch (err: any) {
    // Create an error response object using constructor pattern
    const errorMessage = err?.status === 401 
      ? 'Authentication required. Please log in as an admin to perform bulk resolve.'
      : err?.message || 'An error occurred during bulk resolve.'
    
    bulkResolveResult.value = new BulkResolveResponse({
      integrationId: integrationId.value,
      success: false,
      results: [],
      errorMessage
    })
    
    showBulkResolveDialog.value = false
    showBulkResolveResultDialog.value = true
  }
}
</script>

<style scoped>
@import '../styles/pages.css';

.summary-section {
  display: flex;
  gap: 1rem;
}

.summary-card {
  flex: 1;
}

.summary-item {
  text-align: center;
  padding: 0 1rem;
}

.summary-value {
  font-size: 2rem;
  font-weight: bold;
  line-height: 1.2;
}

.summary-label {
  font-size: 0.85rem;
  color: var(--fuse-text-muted);
}

.summary-item.status-in-sync .summary-value {
  color: var(--q-positive);
}

.summary-item.status-drift .summary-value {
  color: var(--q-warning);
}

.summary-item.status-missing .summary-value {
  color: var(--q-orange);
}

.summary-item.status-error .summary-value {
  color: var(--q-negative);
}

.account-link {
  color: var(--q-primary);
  text-decoration: none;
}

.account-link:hover {
  text-decoration: underline;
}

.permission-summary {
  display: flex;
  align-items: center;
  flex-wrap: wrap;
  gap: 0.25rem;
}

.tag-list {
  display: flex;
  flex-wrap: wrap;
  gap: 0.25rem;
}
</style>
