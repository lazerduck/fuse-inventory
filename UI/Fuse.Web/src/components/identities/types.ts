import type { IdentityKind, IdentityAssignment, TargetKind } from '../../api/client'

export interface IdentityFormModel {
  name: string
  kind: IdentityKind
  notes: string
  ownerInstanceId: string | null
  assignments: IdentityAssignment[]
  tagIds: string[]
}

export interface SelectOption<T = string> {
  label: string
  value: T
}

export interface TargetOption extends SelectOption<string> {
  targetKind?: TargetKind
}

export interface AssignmentForm {
  targetKind: TargetKind
  targetId: string | null
  role: string
  notes: string
}
