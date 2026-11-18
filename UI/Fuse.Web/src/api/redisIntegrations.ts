import {
  CreateRedisIntegration, UpdateRedisIntegration, ValidateRedisConnection,
  type RedisIntegrationResponse, type RedisConnectionTestResult, type RedisDiscoveryResponse,
} from './client'
import { useFuseClient } from '../composables/useFuseClient'

export type { RedisIntegrationResponse, RedisConnectionTestResult, RedisDiscoveryResponse }

export interface RedisIntegrationFormModel {
  name: string
  dataStoreId: string
  endpoint: string
  username: string
  // Undefined retains the saved password on edit; empty explicitly clears it.
  password?: string
}

export const listRedisIntegrations = (signal?: AbortSignal) => useFuseClient().redisIntegrationAll(signal)
export const getRedisIntegration = (id: string, signal?: AbortSignal) => useFuseClient().redisIntegrationGET(id, signal)
export const createRedisIntegration = (value: RedisIntegrationFormModel) =>
  useFuseClient().redisIntegrationPOST(new CreateRedisIntegration(value))
export const updateRedisIntegration = (id: string, value: RedisIntegrationFormModel) =>
  useFuseClient().redisIntegrationPUT(id, new UpdateRedisIntegration({ id, ...value }))
export const deleteRedisIntegration = (id: string) => useFuseClient().redisIntegrationDELETE(id)
export const validateRedisConnection = (value: Pick<RedisIntegrationFormModel, 'endpoint' | 'username' | 'password'>, signal?: AbortSignal) =>
  useFuseClient().redisIntegrationValidate(new ValidateRedisConnection(value), signal)
export const validateSavedRedisConnection = (id: string, signal?: AbortSignal) => useFuseClient().redisIntegrationValidateSaved(id, signal)
export const discoverRedisAccess = (id: string, signal?: AbortSignal) => useFuseClient().redisIntegrationDiscovery(id, signal)
