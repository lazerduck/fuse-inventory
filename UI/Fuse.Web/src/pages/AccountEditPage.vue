<template>
  <div class="page-container">
    <AccountEditHeader
      :title="pageTitle"
      :subtitle="pageSubtitle"
      :primary-label="isEditMode ? 'Save Changes' : 'Create Account'"
      :is-saving="isSaving"
      :can-modify="canModify"
      :show-clone="isEditMode && account?.targetKind !== TargetKind.External"
      @cancel="handleCancel"
      @save="handleSave"
      @clone="openCloneDialog"
    />

    <q-banner v-if="loadError" dense class="bg-red-1 text-negative q-mb-md">
      {{ loadError }}
    </q-banner>

    <q-banner v-if="!canModify" dense class="bg-orange-1 text-orange-9 q-mb-md">
      You do not have permission to {{ isEditMode ? 'edit' : 'create' }} accounts.
    </q-banner>

    <q-card v-if="canModify && !loadError" class="content-card">
      <q-card-section>
        <div class="text-h6 q-mb-md">Account Details</div>
        <div v-if="isLoadingInitialData" class="row items-center justify-center q-pa-lg">
          <q-spinner color="primary" size="3em" />
        </div>
        <AccountForm
          v-else
          v-model="form"
          :target-kind-options="targetKindOptions"
          :target-options="targetOptions"
          :auth-kind-options="authKindOptions"
        />
      </q-card-section>

      <q-separator />

      <q-card-section>
        <div class="text-h6 q-mb-md">Grants & Permissions</div>
        <AccountGrantsSection
          :grants="isEditMode ? (account?.grants ?? []) : form.grants"
          :disable-actions="grantMutationPending"
          @add="handleGrantCreateRequest"
          @edit="handleGrantEditRequest"
          @delete="handleGrantDeleteRequest"
        />
      </q-card-section>

      <q-separator v-if="showSqlStatus" />

      <q-card-section v-if="showSqlStatus">
        <div class="text-h6 q-mb-md">SQL Status</div>
        <AccountSqlStatusSection :account-id="accountId!" />
      </q-card-section>

      <q-separator v-if="showSecretOperations" />

      <AccountSecretOperationsSection
        v-if="showSecretOperations"
        :can-rotate="canRotateSecret"
        :can-reveal="canRevealSecret"
        @rotate="openRotateDialog"
        @reveal="openRevealDialog"
      />
    </q-card>

    <AccountGrantsDialog
      v-model="isGrantDialogOpen"
      :initial-grant="editingGrant"
      :has-sql-integration="hasSqlIntegration"
      :database-options="databaseOptions"
      :is-databases-loading="isDatabasesLoading"
      :privilege-options="privilegeOptions"
      :loading="grantDialogLoading"
      @save="handleGrantSubmit"
    />

    <AccountSecretRotateDialog
      v-model="isRotateDialogOpen"
      v-model:new-secret-value="newSecretValue"
      :loading="rotateSecretLoading"
      @submit="handleRotateSecret"
      @cancel="closeRotateDialog"
    />

    <AccountSecretRevealDialog
      v-model="isRevealDialogOpen"
      :revealed-secret="revealedSecret"
      :show-secret="showRevealedValue"
      :loading="revealSecretLoading"
      @close="closeRevealDialog"
      @reveal="handleRevealSecret"
      @toggle-visibility="toggleRevealedValue"
      @copy="copySecretToClipboard"
    />

    <!-- Clone Dialog -->
    <q-dialog v-model="isCloneDialogOpen" persistent>
      <q-card style="min-width: 480px">
        <q-card-section style="display: flex; justify-content: space-between; align-items: center">
          <div class="text-h6">Clone across environments</div>
          <q-btn flat round dense icon="close" @click="isCloneDialogOpen = false" />
        </q-card-section>
        <q-separator />
        <q-card-section>
          <div v-if="isCloneTargetsLoading" class="row items-center justify-center q-pa-md">
            <q-spinner color="primary" size="2em" />
          </div>
          <div v-else-if="cloneTargets.length === 0" class="text-grey-6 q-pa-sm">
            No other environments found for this account's target.
          </div>
          <div v-else>
            <p class="q-mb-sm text-body2">Select the environments to clone this account into:</p>
            <q-list>
              <q-item v-for="target in cloneTargets" :key="target.id" tag="label" dense>
                <q-item-section side>
                  <q-checkbox v-model="selectedCloneTargetIds" :val="target.id!" />
                </q-item-section>
                <q-item-section>
                  <q-item-label>{{ target.label }}</q-item-label>
                  <q-item-label caption>{{ target.environmentName }}</q-item-label>
                </q-item-section>
              </q-item>
            </q-list>
          </div>
        </q-card-section>
        <q-separator />
        <q-card-actions align="right">
          <q-btn flat label="Cancel" @click="isCloneDialogOpen = false" />
          <q-btn
            color="primary"
            label="Clone"
            :loading="isCloning"
            :disable="selectedCloneTargetIds.length === 0"
            @click="handleClone"
          />
        </q-card-actions>
      </q-card>
    </q-dialog>
  </div>
</template>

<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { Notify, Dialog } from 'quasar'
import { Grant, CloneTarget, CloneAccountRequest, TargetKind } from '../api/client'
import AccountForm from '../components/accounts/AccountForm.vue'
import AccountGrantsSection from '../components/accounts/AccountGrantsSection.vue'
import AccountSqlStatusSection from '../components/accounts/AccountSqlStatusSection.vue'
import AccountEditHeader from './account-edit/components/AccountEditHeader.vue'
import AccountGrantsDialog from './account-edit/components/AccountGrantsDialog.vue'
import AccountSecretOperationsSection from './account-edit/components/AccountSecretOperationsSection.vue'
import AccountSecretRotateDialog from './account-edit/components/AccountSecretRotateDialog.vue'
import AccountSecretRevealDialog from './account-edit/components/AccountSecretRevealDialog.vue'
import { useAccountEdit } from './account-edit/composables/useAccountEdit'
import { useAccountGrants, type GrantFormInput } from './account-edit/composables/useAccountGrants'
import { useAccountSecretOperations } from './account-edit/composables/useAccountSecretOperations'
import { useFuseClient } from '../composables/useFuseClient'
import { useMutation, useQueryClient } from '@tanstack/vue-query'
import { getErrorMessage } from '../utils/error'

const {
  accountId,
  isEditMode,
  pageTitle,
  pageSubtitle,
  account,
  loadError,
  form,
  isLoadingInitialData,
  targetKindOptions,
  targetOptions,
  authKindOptions,
  privilegeOptions,
  hasSqlIntegration,
  databaseOptions,
  isDatabasesLoading,
  showSqlStatus,
  canModify,
  isSaving,
  handleSave,
  handleCancel,
  sqlIntegrationsQuery,
  currentSqlIntegrationId,
  selectedProvider
} = useAccountEdit()

const secretFields = computed(() => form.value.secret)

const {
  showSecretOperations,
  canRotateSecret,
  canRevealSecret,
  isRotateDialogOpen,
  newSecretValue,
  rotateSecretLoading,
  openRotateDialog,
  closeRotateDialog,
  handleRotateSecret,
  isRevealDialogOpen,
  revealedSecret,
  showRevealedValue,
  revealSecretLoading,
  openRevealDialog,
  closeRevealDialog,
  handleRevealSecret,
  toggleRevealedValue,
  copySecretToClipboard
} = useAccountSecretOperations({
  secret: secretFields,
  isEditMode,
  selectedProvider
})

const {
  grantMutationPending,
  grantDialogLoading,
  createGrant,
  updateGrant,
  deleteGrant
} = useAccountGrants({
  accountId,
  account,
  currentSqlIntegrationId,
  sqlIntegrations: sqlIntegrationsQuery.data
})

const isGrantDialogOpen = ref(false)
const editingGrant = ref<Grant | null>(null)

watch(isGrantDialogOpen, (isOpen) => {
  if (!isOpen) {
    editingGrant.value = null
  }
})

function closeGrantDialog() {
  isGrantDialogOpen.value = false
  editingGrant.value = null
}

function handleGrantSubmit(payload: GrantFormInput) {
  if (!accountId.value) return
  if (editingGrant.value?.id) {
    updateGrant(editingGrant.value.id, payload, closeGrantDialog)
  } else {
    createGrant(payload, closeGrantDialog)
  }
}

function handleGrantCreateRequest() {
  if (!isEditMode.value) {
    Notify.create({ type: 'warning', message: 'Save the account first before adding grants' })
    return
  }
  if (!accountId.value) {
    Notify.create({ type: 'warning', message: 'Account ID is required' })
    return
  }
  editingGrant.value = null
  isGrantDialogOpen.value = true
}

function handleGrantEditRequest({ grant }: { grant: Grant }) {
  editingGrant.value = grant
  isGrantDialogOpen.value = true
}

function handleGrantDeleteRequest({ grant }: { grant: Grant }) {
  if (!accountId.value || !grant.id) return
  Dialog.create({
    title: 'Delete grant',
    message: `Delete grant for "${grant.database ?? 'this database'}"?`,
    cancel: true,
    persistent: true
  }).onOk(() => deleteGrant(grant.id!))
}

// Clone across environments
const client2 = useFuseClient()
const queryClient2 = useQueryClient()
const isCloneDialogOpen = ref(false)
const selectedCloneTargetIds = ref<string[]>([])
const cloneTargets = ref<CloneTarget[]>([])
const isCloneTargetsLoading = ref(false)

const cloneMutation = useMutation({
  mutationFn: ({ id, payload }: { id: string; payload: CloneAccountRequest }) =>
    client2.accountClone(id, payload),
  onSuccess: (created) => {
    queryClient2.invalidateQueries({ queryKey: ['accounts'] })
    isCloneDialogOpen.value = false
    Notify.create({ type: 'positive', message: `Cloned to ${created.length} environment${created.length !== 1 ? 's' : ''}` })
  },
  onError: (err) => {
    Notify.create({ type: 'negative', message: getErrorMessage(err, 'Unable to clone account') })
  }
})

const isCloning = computed(() => cloneMutation.isPending.value)

async function openCloneDialog() {
  if (!accountId.value) return
  isCloneDialogOpen.value = true
  selectedCloneTargetIds.value = []
  isCloneTargetsLoading.value = true
  try {
    cloneTargets.value = await client2.accountCloneTargets(accountId.value)
  } catch {
    cloneTargets.value = []
  } finally {
    isCloneTargetsLoading.value = false
  }
}

function handleClone() {
  if (!accountId.value || selectedCloneTargetIds.value.length === 0) return
  const payload = Object.assign(new CloneAccountRequest(), {
    targetIds: [...selectedCloneTargetIds.value]
  })
  cloneMutation.mutate({ id: accountId.value, payload })
}
</script>

<style scoped>
@import '../styles/pages.css';

.form-grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(220px, 1fr));
  gap: 1rem;
}
</style>
