<template>
  <div class="page-container">
    <div class="page-header">
      <div>
        <h1>App Settings</h1>
        <p class="subtitle">Here you can configure your application settings.</p>
      </div>
    </div>
    <q-card class="content-card q-mb-md">
      <q-card-section>
        <div class="settings-section">
          <h2>General Settings</h2>
          <div class="settings-item">
            <q-toggle
              v-model="incompleteDataWarningEnabled"
              label="Enable Incomplete Data Warning"
              :disable="!canEdit"
            />
          </div>
        </div>
      </q-card-section>
    </q-card>
  </div>
</template>

<script setup lang="ts">
import { computed } from 'vue'
import { useFuseStore } from "../stores/FuseStore";

const fuseStore = useFuseStore();
fuseStore.fetchStatus();

const incompleteDataWarningEnabled = computed({
  get: () => fuseStore.appSettings?.incompleteDataWarningEnabled ?? false,
  async set(value: boolean) {
    if (!fuseStore.appSettings) {
      return;
    }

    await fuseStore.updateAppSettings({ incompleteDataWarningEnabled: value });
  }
});

const canEdit = computed(() => {
  return fuseStore.hasPermission("appsettings:update");
});

</script>

<style scoped>
@import '../styles/pages.css';
</style>