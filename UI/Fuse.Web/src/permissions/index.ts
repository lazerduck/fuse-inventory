import { InventoryPermissions } from './inventoryPermissions'
import { SecurityPermissions } from './securityPermissions'
import { IntegrationPermissions } from './integrationPermissions'
import { OperationsPermissions } from './operationsPermissions'

export const Permission = {
  ...InventoryPermissions,
  ...SecurityPermissions,
  ...IntegrationPermissions,
  ...OperationsPermissions,
} as const

export type Permission = (typeof Permission)[keyof typeof Permission]
