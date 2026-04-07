<template>
  <section v-if="risks.value.length > 0" class="detail-section">
    <h3 class="section-subtitle">
      <q-icon name="warning" size="20px" />
      Risks
      <q-badge v-if="aggregation.value.total > 0" :label="aggregation.value.total" color="grey-6" class="risk-count-badge" />
    </h3>
    <div v-if="aggregation.value.total > 0" class="risk-summary">
      <span v-if="aggregation.value.critical > 0" class="risk-stat critical">
        {{ aggregation.value.critical }} Critical
      </span>
      <span v-if="aggregation.value.high > 0" class="risk-stat high">
        {{ aggregation.value.high }} High
      </span>
      <span v-if="aggregation.value.medium > 0" class="risk-stat medium">
        {{ aggregation.value.medium }} Medium
      </span>
      <span v-if="aggregation.value.low > 0" class="risk-stat low">
        {{ aggregation.value.low }} Low
      </span>
    </div>
    <div class="risk-list">
      <router-link
        v-for="risk in risks.value"
        :key="risk.id"
        :to="`/view/risk/${risk.id}`"
        class="risk-item"
      >
        <q-icon name="warning" size="18px" :color="getRiskImpactColor(risk.impact)" />
        <span class="risk-title">{{ risk.title }}</span>
        <q-badge :label="risk.impact" :color="getRiskImpactColor(risk.impact)" outline class="risk-badge" />
        <q-icon name="chevron_right" size="16px" />
      </router-link>
    </div>
  </section>
</template>

<script setup lang="ts">
import { type Ref } from 'vue'
import type { Risk } from 'api/client'
import type { RiskAggregation } from '../../composables/useRiskAggregation'

interface Props {
  risks: Ref<Risk[]>
  aggregation: Ref<RiskAggregation>
}

defineProps<Props>()

// Map risk impact to color
function getRiskImpactColor(impact: string | undefined): string {
  switch (impact) {
    case 'Critical':
      return 'negative'
    case 'High':
      return 'orange'
    case 'Medium':
      return 'warning'
    case 'Low':
      return 'positive'
    default:
      return 'grey'
  }
}
</script>

<style scoped>
.detail-section {
  padding-bottom: 1rem;
  border-bottom: 1px solid var(--fuse-panel-border);
}

.section-subtitle {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  margin: 0 0 0.75rem 0;
  font-size: 1rem;
  font-weight: 600;
  color: var(--fuse-text-muted);
}

.risk-count-badge {
  margin-left: 0.5rem;
  font-size: 0.75rem;
}

.risk-summary {
  display: flex;
  gap: 1rem;
  flex-wrap: wrap;
  margin-bottom: 1rem;
  padding: 0.75rem;
  background: var(--fuse-panel-bg);
  border-radius: 6px;
}

.risk-stat {
  font-size: 0.9rem;
  font-weight: 500;
  padding: 0.25rem 0.5rem;
  border-radius: 4px;
}

.risk-stat.critical {
  background: rgba(244, 67, 54, 0.1);
  color: #f44336;
}

.risk-stat.high {
  background: rgba(255, 152, 0, 0.1);
  color: #ff9800;
}

.risk-stat.medium {
  background: rgba(255, 193, 7, 0.1);
  color: #ffc107;
}

.risk-stat.low {
  background: rgba(76, 175, 80, 0.1);
  color: #4caf50;
}

.risk-list {
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
}

.risk-item {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  padding: 0.75rem;
  background: var(--fuse-panel-bg);
  border-radius: 6px;
  text-decoration: none;
  color: inherit;
  transition: background 0.2s;
}

.risk-item:hover {
  background: var(--fuse-hover-bg);
}

.risk-title {
  flex: 1;
  font-weight: 500;
}

.risk-badge {
  font-size: 0.75rem;
}
</style>
