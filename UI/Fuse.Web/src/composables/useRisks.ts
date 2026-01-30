import { computed, type Ref } from 'vue'
import { useMutation, useQuery, useQueryClient } from '@tanstack/vue-query'
import { useQuasar } from 'quasar'
import { useFuseClient } from './useFuseClient'
import { Risk, CreateRisk, UpdateRisk } from '../api/client'

export function useRisks() {
  const client = useFuseClient()
  
  const { data: risks = [], isLoading: risksLoading } = useQuery({
    queryKey: ['risks'],
    queryFn: () => client.riskAll()
  })

  return { risks, risksLoading }
}

export function useRisksByTarget(targetType: string | Ref<string>, targetId: string | Ref<string>) {
  const client = useFuseClient()
  const $q = useQuasar()
  const queryClient = useQueryClient()

  const targetTypeValue = computed(() => typeof targetType === 'string' ? targetType : targetType.value)
  const targetIdValue = computed(() => typeof targetId === 'string' ? targetId : targetId.value)

  const { data: risks = [], isLoading: risksLoading, refetch } = useQuery({
    queryKey: ['risks', targetTypeValue, targetIdValue],
    queryFn: () => client.target(targetTypeValue.value, targetIdValue.value),
    enabled: computed(() => !!targetTypeValue.value && !!targetIdValue.value)
  })

  const createRiskMutation = useMutation({
    mutationFn: (command: CreateRisk) => client.riskPOST(command),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['risks'] })
      queryClient.invalidateQueries({ queryKey: ['risks', targetType, targetId] })
      $q.notify({
        type: 'positive',
        message: 'Risk created successfully'
      })
    },
    onError: (error: any) => {
      $q.notify({
        type: 'negative',
        message: error?.response?.error || 'Failed to create risk'
      })
    }
  })

  const updateRiskMutation = useMutation({
    mutationFn: ({ id, command }: { id: string; command: UpdateRisk }) =>
      client.riskPUT(id, command),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['risks'] })
      queryClient.invalidateQueries({ queryKey: ['risks', targetType, targetId] })
      $q.notify({
        type: 'positive',
        message: 'Risk updated successfully'
      })
    },
    onError: (error: any) => {
      $q.notify({
        type: 'negative',
        message: error?.response?.error || 'Failed to update risk'
      })
    }
  })

  const deleteRiskMutation = useMutation({
    mutationFn: (id: string) => client.riskDELETE(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['risks'] })
      queryClient.invalidateQueries({ queryKey: ['risks', targetType, targetId] })
      $q.notify({
        type: 'positive',
        message: 'Risk deleted successfully'
      })
    },
    onError: (error: any) => {
      $q.notify({
        type: 'negative',
        message: error?.response?.error || 'Failed to delete risk'
      })
    }
  })

  async function saveRisk(risk: Partial<Risk>) {
    if (risk.id) {
      // Update existing risk
      const command = new UpdateRisk({
        id: risk.id,
        title: risk.title!,
        description: risk.description,
        impact: risk.impact!,
        likelihood: risk.likelihood!,
        status: risk.status!,
        ownerPositionId: risk.ownerPositionId!,
        approverPositionId: risk.approverPositionId,
        targetType: risk.targetType!,
        targetId: risk.targetId!,
        mitigation: risk.mitigation,
        reviewDate: risk.reviewDate,
        approvalDate: risk.approvalDate,
        tagIds: risk.tagIds,
        notes: risk.notes
      })
      await updateRiskMutation.mutateAsync({ id: risk.id, command })
    } else {
      // Create new risk
      const command = new CreateRisk({
        title: risk.title!,
        description: risk.description,
        impact: risk.impact!,
        likelihood: risk.likelihood!,
        status: risk.status!,
        ownerPositionId: risk.ownerPositionId!,
        approverPositionId: risk.approverPositionId,
        targetType: risk.targetType!,
        targetId: risk.targetId!,
        mitigation: risk.mitigation,
        reviewDate: risk.reviewDate,
        approvalDate: risk.approvalDate,
        tagIds: risk.tagIds,
        notes: risk.notes
      })
      await createRiskMutation.mutateAsync(command)
    }
  }

  async function deleteRisk(id: string) {
    await deleteRiskMutation.mutateAsync(id)
  }

  return {
    risks,
    risksLoading,
    saveRisk,
    deleteRisk,
    refetch,
    isSaving: createRiskMutation.isPending || updateRiskMutation.isPending,
    isDeleting: deleteRiskMutation.isPending
  }
}
