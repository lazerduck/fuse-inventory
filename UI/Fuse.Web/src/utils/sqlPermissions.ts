import type { SqlPermissions } from '../api/client'

export function parseSqlPermissions(permissions?: SqlPermissions): string[] {
  if (!permissions) return []

  if (typeof permissions === 'string') {
    return permissions
      .split(',')
      .map((p) => p.trim())
      .filter(Boolean)
  }

  const perms: string[] = []
  const permValue = permissions as any

  if (typeof permValue === 'number') {
    if (permValue & 1) perms.push('Read')
    if (permValue & 2) perms.push('Write')
    if (permValue & 4) perms.push('Create')
  }

  return perms
}
