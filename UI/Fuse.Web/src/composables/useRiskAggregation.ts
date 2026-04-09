import { computed, type Ref } from 'vue'
import { useRisks } from './useRisks'
import type { Risk } from 'api/client'

export interface RiskAggregation {
  total: number
  critical: number
  high: number
  medium: number
  low: number
  byStatus: {
    identified: number
    mitigated: number
    accepted: number
    closed: number
  }
}

export function useRiskAggregation(targetType: string | Ref<string>, targetId: string | Ref<string>) {
  const { risks } = useRisks()

  const targetTypeValue = computed(() => typeof targetType === 'string' ? targetType : targetType.value)
  const targetIdValue = computed(() => typeof targetId === 'string' ? targetId : targetId.value)

  const filteredRisks = computed<Risk[]>(() => {
    if (!risks.value || !Array.isArray(risks.value)) return []
    return risks.value.filter(
      (risk: any) =>
        risk.targetType === targetTypeValue.value && risk.targetId === targetIdValue.value
    )
  })

  const aggregation = computed<RiskAggregation>(() => {
    const agg: RiskAggregation = {
      total: filteredRisks.value.length,
      critical: 0,
      high: 0,
      medium: 0,
      low: 0,
      byStatus: {
        identified: 0,
        mitigated: 0,
        accepted: 0,
        closed: 0
      }
    }

    for (const risk of filteredRisks.value) {
      // Count by impact
      switch (risk.impact) {
        case 'Critical':
          agg.critical++
          break
        case 'High':
          agg.high++
          break
        case 'Medium':
          agg.medium++
          break
        case 'Low':
          agg.low++
          break
      }

      // Count by status
      switch (risk.status) {
        case 'Identified':
          agg.byStatus.identified++
          break
        case 'Mitigated':
          agg.byStatus.mitigated++
          break
        case 'Accepted':
          agg.byStatus.accepted++
          break
        case 'Closed':
          agg.byStatus.closed++
          break
      }
    }

    return agg
  })

  return {
    risks: filteredRisks,
    aggregation
  }
}
