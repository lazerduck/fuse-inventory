<template>
  <q-card class="form-dialog">
    <q-card-section class="dialog-header">
      <div class="text-h6">Reset Password</div>
      <q-btn flat round dense icon="close" @click="emit('cancel')" />
    </q-card-section>
    <q-separator />
    <q-form @submit.prevent="handleSubmit">
      <q-card-section>
        <div class="form-grid">
          <q-input
            :model-value="props.userName"
            label="Username"
            dense
            outlined
            readonly
            disable
            class="full-span"
          />
            dense
            outlined
            readonly
            disable
            class="full-span"
          />
          <q-input
            v-if="isSelfReset"
            v-model="form.currentPassword"
            label="Current Password"
            dense
            outlined
            type="password"
            class="full-span"
            :rules="[v => !!v || 'Current password is required']"
          />
          <q-input
            v-model="form.newPassword"
            label="New Password"
            dense
            outlined
            type="password"
            class="full-span"
            :rules="[v => !!v || 'New password is required', v => v.length >= 8 || 'Password must be at least 8 characters']"
          />
          <q-input
            v-model="confirmPassword"
            label="Confirm New Password"
            dense
            outlined
            type="password"
            class="full-span"
            :rules="[v => !!v || 'Please confirm your new password', v => v === form.newPassword || 'Passwords do not match']"
          />
        </div>
      </q-card-section>
      <q-card-actions align="right" class="q-pa-md">
        <q-btn flat label="Cancel" color="grey" @click="emit('cancel')" />
        <q-btn color="primary" type="submit" label="Reset Password" :loading="loading" />
      </q-card-actions>
    </q-form>
  </q-card>
</template>

<script setup lang="ts">
import { reactive, ref } from 'vue'

interface ResetPasswordForm {
  newPassword: string
  currentPassword?: string
}

interface Props {
  userName: string
  isSelfReset: boolean
  loading?: boolean
}

interface Emits {
  (e: 'submit', form: ResetPasswordForm): void
  (e: 'cancel'): void
}

const props = withDefaults(defineProps<Props>(), {
  loading: false
})

const emit = defineEmits<Emits>()

const form = reactive<ResetPasswordForm>({
  newPassword: '',
  currentPassword: ''
})

const confirmPassword = ref('')

function handleSubmit() {
  emit('submit', { ...form })
}
</script>

<style scoped>
@import '../../styles/pages.css';

.form-dialog {
  min-width: 480px;
}
</style>
