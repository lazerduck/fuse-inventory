export function getErrorMessage(error: unknown, fallback = 'Request failed'): string {
  if (typeof error === 'string') {
    return error
  }

  if (error && typeof error === 'object') {
    const maybeError = error as { message?: string; title?: string; detail?: string }
    if (maybeError.detail) {
      return maybeError.detail
    }
    if (maybeError.title) {
      return maybeError.title
    }
    if (maybeError.message) {
      return maybeError.message
    }
  }

  return fallback
}
