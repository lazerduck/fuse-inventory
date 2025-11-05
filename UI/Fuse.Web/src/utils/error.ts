import { ApiException } from '../api/client'

type ProblemDetails = {
  error?: string
  errors?: Record<string, string[] | string>
  message?: string
  title?: string
  detail?: string
}

function fromProblemDetails(details: ProblemDetails | undefined): string | undefined {
  if (!details) {
    return undefined
  }

  if (details.error && details.error.trim().length) {
    return details.error
  }

  if (details.detail && details.detail.trim().length) {
    return details.detail
  }

  if (details.title && details.title.trim().length) {
    return details.title
  }

  if (details.message && details.message.trim().length) {
    return details.message
  }

  if (details.errors) {
    for (const value of Object.values(details.errors)) {
      if (Array.isArray(value) && value.length) {
        const candidate = value.find((item) => !!item?.trim?.())
        if (candidate) {
          return candidate
        }
      } else if (typeof value === 'string' && value.trim().length) {
        return value
      }
    }
  }

  return undefined
}

function parseResponsePayload(payload: string | undefined): ProblemDetails | undefined {
  if (!payload) {
    return undefined
  }

  try {
    const parsed = JSON.parse(payload)
    if (parsed && typeof parsed === 'object') {
      return parsed as ProblemDetails
    }
  } catch (err) {
    // ignore JSON parse errors
  }

  return undefined
}

export function getErrorMessage(error: unknown, fallback = 'Request failed'): string {
  if (typeof error === 'string') {
    return error
  }

  if (error instanceof ApiException) {
    const parsed = parseResponsePayload(error.response)
    const message = fromProblemDetails(parsed)
    if (message) {
      return message
    }
    if (error.message?.trim().length) {
      return error.message
    }
  }

  if (error && typeof error === 'object') {
    const maybeProblem = fromProblemDetails(error as ProblemDetails)
    if (maybeProblem) {
      return maybeProblem
    }

    if (error instanceof Error && error.message.trim().length) {
      return error.message
    }
  }

  return fallback
}
