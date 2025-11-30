/**
 * Types for read-only view shell components.
 * Used by ReadOnlyShell and ReadOnlyContextList components.
 */

/**
 * Entity types supported in the read-only view.
 */
export type EntityType =
  | 'app'
  | 'instance'
  | 'dependency'
  | 'account'
  | 'identity'
  | 'datastore'
  | 'external'

/**
 * Configuration for entity type display.
 */
export interface EntityTypeConfig {
  label: string
  icon: string
}

/**
 * Configuration map for all entity types.
 */
export const entityTypeConfig: Record<EntityType, EntityTypeConfig> = {
  app: { label: 'Application', icon: 'apps' },
  instance: { label: 'Instance', icon: 'layers' },
  dependency: { label: 'Dependency', icon: 'link' },
  account: { label: 'Account', icon: 'vpn_key' },
  identity: { label: 'Identity', icon: 'badge' },
  datastore: { label: 'Data Store', icon: 'storage' },
  external: { label: 'External Resource', icon: 'hub' }
}

/**
 * Represents an item in the "higher" context list (upstream items).
 */
export interface HigherItem {
  id: string
  name: string
  type: EntityType
  route: string
  subtitle?: string
}

/**
 * Represents an item in the "lower" context list (downstream items).
 */
export interface LowerItem {
  id: string
  name: string
  type: EntityType
  route: string
  subtitle?: string
}

/**
 * Utility function to get icon for an entity type.
 */
export function getIconForEntityType(type: EntityType): string {
  return entityTypeConfig[type]?.icon ?? 'help'
}

/**
 * Utility function to get label for an entity type.
 */
export function getLabelForEntityType(type: EntityType): string {
  return entityTypeConfig[type]?.label ?? 'Unknown'
}
