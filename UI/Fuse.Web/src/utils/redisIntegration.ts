export function redisEndpointRule(value: string): true | string {
  try {
    const uri = new URL(value)
    if (!['redis:', 'rediss:'].includes(uri.protocol) || !uri.hostname
      || uri.username || uri.password || uri.search || uri.hash || !['', '/'].includes(uri.pathname)) {
      return 'Use redis://host:port or rediss://host:port, with credentials in the separate fields.'
    }
    if (uri.port && (Number(uri.port) < 1 || Number(uri.port) > 65535)) return 'Use a port between 1 and 65535.'
    return true
  } catch {
    return 'Use redis://host:port or rediss://host:port.'
  }
}

export function redisStatusLabel(value?: string | null): string {
  const labels: Record<string, string> = {
    complete: 'Complete', partial: 'Partial', denied: 'Permission denied', unavailable: 'Unavailable',
    timeout: 'Timed out', unsupported: 'Unsupported', connected: 'Connected',
    'authentication-failed': 'Authentication failed', 'permission-denied': 'Permission denied',
    matched: 'Matched', disabled: 'Disabled in Redis', missing: 'Missing in Redis',
    untracked: 'Not recorded in Fuse', unknown: 'Unknown',
  }
  return value ? labels[value] ?? value : 'Unknown'
}

export function redisStatusColor(value?: string | null): string {
  if (['complete', 'connected', 'matched'].includes(value ?? '')) return 'positive'
  if (['missing', 'authentication-failed', 'unavailable', 'timeout'].includes(value ?? '')) return 'negative'
  if (['partial', 'denied', 'permission-denied', 'untracked'].includes(value ?? '')) return 'orange-9'
  return 'grey-7'
}

// Missing users can only be asserted after a complete ACL inventory.
export function redisAccountStatus(status: string | undefined, aclComplete: boolean | undefined): string {
  return status === 'missing' && !aclComplete ? 'unknown' : status ?? 'unknown'
}
