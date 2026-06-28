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
    <q-card class="content-card q-mb-md">
      <q-card-section>
        <div class="settings-section">
          <h2>License Settings</h2>
          <div class="settings-item">
            <q-toggle v-model="localLicenseValidationOnly" label="Validate licenses locally only" :disable="!canEdit" />
            <div class="text-caption text-grey-7">For isolated or internet-restricted deployments. Fuse validates the license signature and expiry locally without contacting the licensing service.</div>
          </div>
          <div class="settings-item q-mt-md">
            <q-toggle v-model="hideValidLicenseChip" label="Hide the license chip while licensed" :disable="!canEdit || !fuseStore.licenseStatus?.isValid" />
          </div>
        </div>
      </q-card-section>
    </q-card>
    <q-card class="content-card q-mb-md">
      <q-card-section>
        <div class="settings-section">
          <h2>Data Retention</h2>
          <div class="settings-item">
            <q-input
              v-model.number="versionHistoryKeepCount"
              label="Version history limit per entity"
              type="number"
              min="0"
              :disable="!canEdit"
              :rules="[val => val === 0 || (val > 0 && val <= 10000) || 'Must be between 1 and 10,000 (0 = unlimited)']"
            >
              <template v-slot:append>
                <q-badge>0 = unlimited</q-badge>
              </template>
            </q-input>
            <div class="text-caption text-grey-7">Maximum number of version history entries to keep per entity. Set to 0 to keep all versions forever.</div>
          </div>
          <div class="settings-item q-mt-md">
            <q-input
              v-model.number="auditLogDaysToKeep"
              label="Audit log retention (days)"
              type="number"
              :disable="!canEdit"
              :rules="[val => val === null || val === 0 || (val > 0 && val <= 36500) || 'Must be between 1 and 100 years (0 or blank = unlimited)']"
            >
              <template v-slot:append>
                <q-badge>0 or blank = unlimited</q-badge>
              </template>
            </q-input>
            <div class="text-caption text-grey-7">Number of days to keep audit log entries. Set to 0 or leave blank to keep all logs forever. Older entries will be automatically purged.</div>
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

const localLicenseValidationOnly = computed({
  get: () => fuseStore.appSettings?.localLicenseValidationOnly ?? false,
  set: (value: boolean) => fuseStore.updateAppSettings({ localLicenseValidationOnly: value })
});

const hideValidLicenseChip = computed({
  get: () => fuseStore.appSettings?.hideValidLicenseChip ?? false,
  set: (value: boolean) => fuseStore.updateAppSettings({ hideValidLicenseChip: value })
});

const versionHistoryKeepCount = computed({
  get: () => fuseStore.appSettings?.versionHistoryKeepCount ?? 0,
  set: (value: number) => fuseStore.updateAppSettings({ versionHistoryKeepCount: value })
});

const auditLogDaysToKeep = computed({
  get: () => fuseStore.appSettings?.auditLogDaysToKeep ?? null,
  set: (value: number | null) => fuseStore.updateAppSettings({ auditLogDaysToKeep: value })
});

const canEdit = computed(() => {
  return fuseStore.hasPermission("appsettings:update");
});

</script>

<style scoped>
@import '../styles/pages.css';
</style>
