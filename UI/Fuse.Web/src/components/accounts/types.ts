import type { AuthKind, Grant, TargetKind } from '../../api/client'

export interface KeyValuePair {
  key: string
  value: string
}

export interface AccountSecretFields {
  providerId: string | null
  secretName: string | null
  plainReference: string
}

export interface AccountFormModel {
  targetKind: TargetKind
  targetId: string | null
  authKind: AuthKind
  userName: string
  secret: AccountSecretFields
  parameters: KeyValuePair[]
  tagIds: string[]
  grants: Grant[]
}

export interface SelectOption<T = string> {
  label: string
  value: T
}

export type TargetOption = SelectOption<string>
