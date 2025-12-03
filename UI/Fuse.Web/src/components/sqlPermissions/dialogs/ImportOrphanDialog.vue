<template>
  <q-dialog v-model="dialogModel" persistent>
    <q-card style="min-width: 450px">
      <q-card-section class="row items-center">
        <q-icon name="person_add" color="primary" size="2em" class="q-mr-sm" />
        <span class="text-h6">Import SQL Account</span>
      </q-card-section>

      <q-card-section>
        <p v-if="orphan">
          Create a Fuse account for SQL principal
          <strong>{{ orphan.principalName }}</strong>?
        </p>

        <div class="q-mt-md">
          <div class="text-subtitle2 q-mb-sm">Authentication Type</div>
          <q-select
            v-model="authKind"
            :options="authOptions"
            emit-value
            map-options
            outlined
            dense
          />
        </div>

        <div class="q-mt-md">
          <div class="text-subtitle2 q-mb-sm">Secret Binding</div>
          <q-select
            v-model="secretBindingKind"
            :options="secretBindingOptions"
            emit-value
            map-options
            outlined
            dense
          />

          <q-input
            v-if="secretBindingKind === SecretBindingKind.PlainReference"
            v-model="plainReference"
            label="Secret Reference"
            outlined
            dense
            class="q-mt-sm"
            hint="Reference to where the secret is stored"
          />
        </div>

        <div v-if="orphan?.actualPermissions?.length" class="q-mt-md">
          <div class="text-subtitle2 q-mb-sm">Permissions to import:</div>
          <div class="tag-list">
            <template v-for="grant in orphan.actualPermissions" :key="grant.database">
              <q-badge
                v-for="priv in grant.privileges"
                :key="`${grant.database}-${priv}`"
                outline
                color="secondary"
                :label="`${grant.database ?? 'default'}:${priv}`"
              />
            </template>
          </div>
        </div>
      </q-card-section>

      <q-card-actions align="right">
        <q-btn flat label="Cancel" color="grey" v-close-popup :disable="isImporting" />
        <q-btn
          flat
          label="Import Account"
          color="primary"
          :loading="isImporting"
          @click="submit"
        />
      </q-card-actions>
    </q-card>
  </q-dialog>
</template>

<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { AuthKind, SecretBindingKind, type SqlOrphanPrincipal } from '../../../api/client'

interface Props {
  modelValue: boolean
  orphan?: SqlOrphanPrincipal | null
  defaultAuthKind?: AuthKind
  defaultSecretBindingKind?: SecretBindingKind
  isImporting?: boolean
}

const props = withDefaults(defineProps<Props>(), {
  orphan: null,
  defaultAuthKind: AuthKind.UserPassword,
  defaultSecretBindingKind: SecretBindingKind.None,
  isImporting: false
})

const emit = defineEmits<{
  (e: 'update:modelValue', value: boolean): void
  (e: 'submit', payload: { authKind: AuthKind; secretBindingKind: SecretBindingKind; plainReference?: string }): void
}>()

const dialogModel = computed({
  get: () => props.modelValue,
  set: (value: boolean) => emit('update:modelValue', value)
})

const authKind = ref(props.defaultAuthKind)
const secretBindingKind = ref(props.defaultSecretBindingKind)
const plainReference = ref('')

watch(dialogModel, (isOpen) => {
  if (!isOpen) {
    authKind.value = props.defaultAuthKind
    secretBindingKind.value = props.defaultSecretBindingKind
    plainReference.value = ''
  }
})

const authOptions = [
  { label: 'User/Password', value: AuthKind.UserPassword },
  { label: 'API Key', value: AuthKind.ApiKey },
  { label: 'Certificate', value: AuthKind.Certificate },
  { label: 'No Authentication', value: AuthKind.None }
]

const secretBindingOptions = [
  { label: 'None', value: SecretBindingKind.None },
  { label: 'Plain Reference', value: SecretBindingKind.PlainReference }
]

function submit() {
  emit('submit', {
    authKind: authKind.value,
    secretBindingKind: secretBindingKind.value,
    plainReference: secretBindingKind.value === SecretBindingKind.PlainReference ? plainReference.value : undefined
  })
}
</script>

<style scoped>
.tag-list {
  display: flex;
  flex-wrap: wrap;
  gap: 0.25rem;
}
</style>
