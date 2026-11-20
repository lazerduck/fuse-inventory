<template>
  <section aria-labelledby="sql-status-title">
    <div class="section-header">
      <div>
        <h2 id="sql-status-title" class="text-h6 q-ma-none">SQL permissions</h2>
        <p v-if="status?.sqlIntegrationName" class="muted q-mb-none">{{ status.sqlIntegrationName }}</p>
      </div>
      <q-btn flat dense no-caps icon="refresh" label="Refresh" :loading="isFetching" @click="() => refetch()" />
    </div>
    <div v-if="isLoading" class="q-py-md" role="status"><q-spinner class="q-mr-sm" />Loading SQL permissions…</div>
    <p v-else-if="error" role="alert" class="q-mt-md">Unable to load SQL permissions.</p>
    <template v-else-if="status">
      <p v-if="status.errorMessage && status.errorMessage !== status.coverageWarning" class="q-mt-md" role="alert">{{ status.errorMessage }}</p>
      <div v-if="showCoverage" class="q-mt-lg">
        <h3 class="text-subtitle2 q-mb-sm">Observed access</h3>
        <SqlAccessCoverage :coverage="status.coverage" :warning="status.coverageWarning" />
      </div>
      <div class="q-mt-lg">
        <div class="row items-center q-gutter-x-sm">
          <h3 class="text-subtitle2 q-ma-none">Whole-database grants</h3>
          <StatusBadge :status="status.status" />
        </div>
        <p v-if="status.statusSummary" class="muted q-mt-xs">{{ status.statusSummary }}</p>
        <q-markup-table v-if="status.permissionComparisons?.length" flat bordered dense wrap-cells>
          <thead><tr><th>Database</th><th>Configured</th><th>Observed</th><th>Difference</th></tr></thead>
          <tbody><tr v-for="comparison in status.permissionComparisons" :key="comparison.database">
            <td>{{ comparison.database }}</td>
            <td>{{ comparison.configuredPrivileges?.join(', ') || '—' }}</td>
            <td>{{ comparison.actualPrivileges?.join(', ') || '—' }}</td>
            <td>
              <div v-if="comparison.missingPrivileges?.length">Missing {{ comparison.missingPrivileges.join(', ') }}</div>
              <div v-if="comparison.extraPrivileges?.length">Extra {{ comparison.extraPrivileges.join(', ') }}</div>
              <div v-if="comparison.blockedPrivileges?.length" class="muted">Manual review · {{ comparison.blockedPrivileges.join(', ') }}</div>
              <span v-if="!comparison.missingPrivileges?.length && !comparison.extraPrivileges?.length">—</span>
            </td>
          </tr></tbody>
        </q-markup-table>
      </div>
    </template>
  </section>
</template>

<script setup lang="ts">
import { computed } from 'vue'
import { SyncStatus } from 'api/client'
import SqlAccessCoverage from '../sqlPermissions/SqlAccessCoverage.vue'
import StatusBadge from '../sqlPermissions/StatusBadge.vue'
import { useAccountSqlStatus } from '../../composables/useAccountSqlStatus'

const props = defineProps<{ accountId: string }>()
const { data, isLoading, isFetching, error, refetch } = useAccountSqlStatus(computed(() => props.accountId))
const status = computed(() => data.value?.status)
const showCoverage = computed(() => status.value?.coverage?.length || status.value?.coverageWarning ||
  status.value?.status === SyncStatus.InSync || status.value?.status === SyncStatus.DriftDetected)
</script>

<style scoped>
.section-header { display: flex; justify-content: space-between; align-items: flex-start; gap: .5rem; }
.muted { color: var(--fuse-text-muted); }
th, td { text-align: left; }
</style>
