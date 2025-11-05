import type { AuthKind, TargetKind } from '../../api/client'

export interface KeyValuePair {
  key: string
  value: string
}

export interface AccountFormModel {
  targetKind: TargetKind
  targetId: string | null
  authKind: AuthKind
  userName: string
  secretRef: string
  parameters: KeyValuePair[]
  tagIds: string[]
}

export interface SelectOption<T = string> {
  label: string
  value: T
}

export type TargetOption = SelectOption<string>
