<template>
  <div class="summary-section">
    <q-card flat bordered class="summary-card">
      <q-card-section class="row q-gutter-md">
        <div class="summary-item">
          <div class="summary-value">{{ summary?.totalAccounts ?? 0 }}</div>
          <div class="summary-label">Total Accounts</div>
        </div>
        <q-separator vertical />
        <div class="summary-item status-in-sync">
          <div class="summary-value">{{ summary?.inSyncCount ?? 0 }}</div>
          <div class="summary-label">In Sync</div>
        </div>
        <q-separator vertical />
        <div class="summary-item status-drift">
          <div class="summary-value">{{ summary?.driftCount ?? 0 }}</div>
          <div class="summary-label">Drift Detected</div>
        </div>
        <q-separator vertical />
        <div class="summary-item status-missing">
          <div class="summary-value">{{ summary?.missingPrincipalCount ?? 0 }}</div>
          <div class="summary-label">Missing Principal</div>
        </div>
        <q-separator vertical />
        <div class="summary-item status-error">
          <div class="summary-value">{{ summary?.errorCount ?? 0 }}</div>
          <div class="summary-label">Errors</div>
        </div>
      </q-card-section>
    </q-card>
  </div>
</template>

<script setup lang="ts">
interface Summary {
  totalAccounts?: number | null
  inSyncCount?: number | null
  driftCount?: number | null
  missingPrincipalCount?: number | null
  errorCount?: number | null
}

defineProps<{ summary?: Summary | null }>()
</script>

<style scoped>
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
</style>
