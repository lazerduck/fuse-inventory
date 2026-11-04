import { SecurityPosture } from 'api/client'

export const securityLevelOptions = [
  { value: SecurityPosture.FullyRestricted, label: 'Fully Restricted', recommended: true, description: 'Sign-in is required to read or edit inventory. Roles control permissions.' },
  { value: SecurityPosture.RestrictedEditing, label: 'Restricted Editing', description: 'Anyone can read inventory. Editing requires sign-in and the appropriate permissions.' },
  { value: SecurityPosture.Unrestricted, label: 'Unrestricted', description: 'Anyone can read and edit inventory without signing in.' }
]

export function securityLevelOption(value?: SecurityPosture | null) {
  return securityLevelOptions.find(option => option.value === value)
}
