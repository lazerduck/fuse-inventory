import type { SecretProviderResponse } from '../api/client'

export type SecretCapability = 'Check' | 'Create' | 'Rotate' | 'Read'

const CAPABILITY_FLAGS: Record<SecretCapability, number> = {
  Check: 1,
  Create: 2,
  Rotate: 4,
  Read: 8
}

const capabilityNames = Object.keys(CAPABILITY_FLAGS) as SecretCapability[]

export function parseCapabilities(value: SecretProviderResponse['capabilities']): SecretCapability[] {
  if (value === undefined || value === null) {
    return []
  }

  if (typeof value === 'string') {
    return value
      .split(',')
      .map((cap) => cap.trim())
      .filter((cap): cap is SecretCapability => capabilityNames.includes(cap as SecretCapability))
  }

  if (typeof value === 'number') {
    return capabilityNames.filter((cap) => (value & CAPABILITY_FLAGS[cap]) === CAPABILITY_FLAGS[cap])
  }

  return []
}

export function hasCapability(
  provider: SecretProviderResponse | null | undefined,
  capability: SecretCapability
): boolean {
  if (!provider) return false
  return parseCapabilities(provider.capabilities).includes(capability)
}
