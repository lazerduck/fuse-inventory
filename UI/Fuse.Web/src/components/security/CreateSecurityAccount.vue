<template>
  <q-card class="form-dialog">
    <q-card-section class="dialog-header">
      <div class="text-h6">{{ title }}</div>
      <q-btn aria-label="Close dialog" v-if="!requireSetup" flat round dense icon="close" @click="draft.discard(() => emit('cancel'))" />
    </q-card-section>
    <q-separator />
    <q-form @submit.prevent="handleSubmit">
      <q-card-section>
        <div class="form-grid" :class="{ 'setup-fields': requireSetup }">
          <q-input
            v-model="form.userName"
            label="Username"
            autocomplete="username"
            autofocus
            dense
            outlined
            required
            :rules="[val => !!val || 'Username is required']"
          />
          <q-select
            v-if="!requireSetup"
            v-model="form.isAdmin"
            label="Administrator"
            dense
            outlined
            :options="isAdminOptions"
            emit-value
            map-options
            hint="Admins have full access. You can still assign additional roles below."
          />
          <q-input
            v-model="form.password"
            label="Password"
            hint="At least 8 characters"
            :type="showPassword ? 'text' : 'password'"
            autocomplete="new-password"
            dense
            outlined
            required
            :rules="[
              val => !!val || 'Password is required',
              val => val.length >= 8 || 'Password must be at least 8 characters'
            ]"
          />
          <q-input
            v-model="confirmPassword"
            label="Confirm Password"
            :type="showPassword ? 'text' : 'password'"
            autocomplete="new-password"
            dense
            outlined
            required
            :rules="[
              val => !!val || 'Please confirm password',
              val => val === form.password || 'Passwords do not match'
            ]"
          />
          <q-input
            v-if="!requireSetup"
            v-model="form.requestedBy"
            label="Requested By"
            dense
            outlined
            class="full-span"
          />
          <div v-if="!requireSetup" class="full-span q-mt-md">
            <div class="text-subtitle2 q-mb-sm">Role Assignments</div>
            <q-select
              v-model="form.roleIds"
              label="Assigned Roles"
              dense
              outlined
              multiple
              emit-value
              map-options
              use-chips
              :options="availableRoles"
              option-label="name"
              option-value="id"
              hint="Select one or more roles to assign permissions"
            >
              <template #option="scope">
                <q-item v-bind="scope.itemProps">
                  <q-item-section>
                    <q-item-label>{{ scope.opt.name }}</q-item-label>
                    <q-item-label caption>{{ scope.opt.description }}</q-item-label>
                  </q-item-section>
                  <q-item-section side>
                    <q-badge :label="scope.opt.permissions?.length || 0" color="primary" />
                  </q-item-section>
                </q-item>
              </template>
            </q-select>
          </div>
        </div>
        <q-checkbox v-model="showPassword" label="Show passwords" />
        <q-banner v-if="requireSetup" class="q-mt-md" :class="$q.dark.isActive ? 'bg-blue-10 text-blue-1' : 'bg-blue-1 text-primary'" dense rounded>
          <template #avatar>
            <q-icon name="info" color="info" />
          </template>
          You will be signed in as the administrator. Fuse starts {{ selectedAccess.label }}:
          {{ selectedAccess.description }} You can change access later in Security settings.
        </q-banner>
        <q-expansion-item v-if="requireSetup" label="Change access level" :caption="selectedAccess.label" class="q-mt-sm">
          <div v-for="option in accessOptions" :key="option.value" class="q-pb-sm">
            <q-radio v-model="form.initialSecurityPosture" :val="option.value" :label="option.recommended ? `${option.label} — recommended` : option.label" />
            <div class="text-body2 q-ml-xl q-mr-md">{{ option.description }}</div>
          </div>
        </q-expansion-item>
      </q-card-section>
      <q-separator />
      <q-card-actions align="right">
        <q-btn v-if="!requireSetup" flat label="Cancel" @click="draft.discard(() => emit('cancel'))" />
        <q-btn color="primary" type="submit" :label="submitLabel" :loading="loading" />
      </q-card-actions>
    </q-form>
  </q-card>
</template>

<script setup lang="ts">
import { useUnsavedChanges } from '../../composables/useUnsavedChanges'
import { reactive, ref, computed } from 'vue'
import { RoleInfo, SecurityPosture } from 'api/client'

interface SecurityAccountForm {
  userName: string
  password: string
  isAdmin: boolean
  requestedBy: string
  roleIds: string[]
  initialSecurityPosture?: SecurityPosture
}

interface Props {
  requireSetup?: boolean
  availableRoles?: RoleInfo[]
  loading?: boolean
}

interface Emits {
  (e: 'submit', form: SecurityAccountForm): void
  (e: 'cancel'): void
}

const props = withDefaults(defineProps<Props>(), {
  requireSetup: false,
  availableRoles: () => [],
  loading: false
})

const emit = defineEmits<Emits>()

const form = reactive<SecurityAccountForm>({
  userName: '',
  password: '',
  isAdmin: props.requireSetup,
  requestedBy: '',
  roleIds: [],
  initialSecurityPosture: props.requireSetup ? SecurityPosture.FullyRestricted : undefined
})

const confirmPassword = ref('')
const showPassword = ref(false)

const accessOptions = [
  { value: SecurityPosture.FullyRestricted, label: 'Fully Restricted', recommended: true, description: 'Sign-in is required to read or edit inventory. Roles control permissions.' },
  { value: SecurityPosture.RestrictedEditing, label: 'Restricted Editing', description: 'Anyone can read inventory. Editing requires sign-in and the appropriate permissions.' },
  { value: SecurityPosture.Unrestricted, label: 'Unrestricted', description: 'Anyone can read and edit inventory without signing in.' }
]
const selectedAccess = computed(() => accessOptions.find(option => option.value === form.initialSecurityPosture) ?? accessOptions[0]!)

const isAdminOptions = [
  { label: 'No', value: false },
  { label: 'Yes', value: true }
]

const title = computed(() => props.requireSetup ? 'Create Administrator Account' : 'Create Security Account')
const submitLabel = computed(() => props.requireSetup ? 'Create Account & Continue' : 'Create Account')

function handleSubmit() {
  if (props.loading) return
  emit('submit', { ...form })
}
const draft = useUnsavedChanges(() => ({ ...form, confirmPassword: confirmPassword.value }))
</script>

<style scoped>
@import '../../styles/pages.css';

.form-dialog {
  width: min(560px, 100%);
  min-width: 0;
}
.setup-fields {
  grid-template-columns: 1fr;
}
</style>
