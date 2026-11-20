<template>
  <q-card flat bordered class="summary-card">
    <q-card-section class="summary">
      <dl>
        <div><dt>Accounts</dt><dd>{{ summary?.totalAccounts ?? 0 }}</dd></div>
        <div><dt>In sync</dt><dd>{{ summary?.inSyncCount ?? 0 }}</dd></div>
        <div><dt>With drift</dt><dd>{{ summary?.driftCount ?? 0 }}</dd></div>
        <div><dt>Missing principal</dt><dd>{{ summary?.missingPrincipalCount ?? 0 }}</dd></div>
        <div v-if="unknownCount"><dt>Unknown</dt><dd>{{ unknownCount }}</dd></div>
        <div><dt>Errors</dt><dd>{{ summary?.errorCount ?? 0 }}</dd></div>
      </dl>
      <slot />
    </q-card-section>
  </q-card>
</template>

<script setup lang="ts">
interface Summary {
  totalAccounts?: number | null
  inSyncCount?: number | null
  driftCount?: number | null
  missingPrincipalCount?: number | null
  errorCount?: number | null
}

defineProps<{ summary?: Summary | null; unknownCount?: number }>()
</script>

<style scoped>
.summary { display: flex; flex-wrap: wrap; justify-content: space-between; align-items: center; gap: 1rem; }
dl { display: flex; flex-wrap: wrap; gap: .5rem 1.8rem; margin: 0; }
dl div { display: flex; flex-direction: column-reverse; }
dt { font-size: .78rem; color: var(--fuse-text-muted); }
dd { margin: 0; font-size: 1.45rem; font-weight: 650; line-height: 1.2; }
</style>
