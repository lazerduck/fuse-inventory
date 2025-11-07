<template>
  <q-card class="form-dialog">
    <q-card-section class="dialog-header">
      <div class="text-h6">{{ title }}</div>
      <q-btn v-if="!requireSetup" flat round dense icon="close" @click="emit('cancel')" />
    </q-card-section>
    <q-separator />
    <q-form @submit.prevent="handleSubmit">
      <q-card-section>
        <div class="form-grid">
          <q-input
            v-model="form.userName"
            label="Username"
            dense
            outlined
            required
            :rules="[val => !!val || 'Username is required']"
          />
          <q-select
            v-model="form.role"
            label="Role"
            dense
            outlined
            emit-value
            map-options
            :options="roleOptions"
            :rules="[val => !!val || 'Role is required']"
          />
          <q-input
            v-model="form.password"
            label="Password"
            type="password"
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
            type="password"
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
        </div>
        <q-banner v-if="requireSetup" class="bg-blue-1 text-info q-mt-md" dense rounded>
          <template #avatar>
            <q-icon name="info" color="info" />
          </template>
          Creating the first administrator account to secure this instance.
        </q-banner>
      </q-card-section>
      <q-separator />
      <q-card-actions align="right">
        <q-btn v-if="!requireSetup" flat label="Cancel" @click="emit('cancel')" />
        <q-btn color="primary" type="submit" :label="submitLabel" :loading="loading" />
      </q-card-actions>
    </q-form>
  </q-card>
</template>

<script setup lang="ts">
import { reactive, ref, computed } from 'vue'
import { SecurityRole } from '../../api/client'

interface SecurityAccountForm {
  userName: string
  password: string
  role: SecurityRole | null
  requestedBy: string
}

interface Props {
  requireSetup?: boolean
  loading?: boolean
}

interface Emits {
  (e: 'submit', form: SecurityAccountForm): void
  (e: 'cancel'): void
}

const props = withDefaults(defineProps<Props>(), {
  requireSetup: false,
  loading: false
})

const emit = defineEmits<Emits>()

const form = reactive<SecurityAccountForm>({
  userName: '',
  password: '',
  role: props.requireSetup ? SecurityRole.Admin : null,
  requestedBy: ''
})

const confirmPassword = ref('')

const roleOptions = [
  { label: 'Reader', value: SecurityRole.Reader },
  { label: 'Admin', value: SecurityRole.Admin }
]

const title = computed(() => props.requireSetup ? 'Create Administrator Account' : 'Create Security Account')
const submitLabel = computed(() => props.requireSetup ? 'Create Account & Continue' : 'Create Account')

function handleSubmit() {
  emit('submit', { ...form })
}
</script>

<style scoped>
@import '../../styles/pages.css';

.form-dialog {
  min-width: 520px;
}
</style>