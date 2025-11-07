/**
 * Composable for managing authentication token storage
 */

const TOKEN_KEY = 'fuse_auth_token'
const TOKEN_EXPIRY_KEY = 'fuse_auth_expiry'

export function useAuthToken() {
  function getToken(): string | null {
    try {
      const token = localStorage.getItem(TOKEN_KEY)
      const expiry = localStorage.getItem(TOKEN_EXPIRY_KEY)
      
      if (!token || !expiry) {
        return null
      }
      
      // Check if token is expired
      const expiryDate = new Date(expiry)
      if (expiryDate <= new Date()) {
        clearToken()
        return null
      }
      
      return token
    } catch {
      return null
    }
  }

  function setToken(token: string, expiresAt: Date | string): void {
    try {
      const expiryDate = typeof expiresAt === 'string' ? new Date(expiresAt) : expiresAt
      localStorage.setItem(TOKEN_KEY, token)
      localStorage.setItem(TOKEN_EXPIRY_KEY, expiryDate.toISOString())
    } catch (error) {
      console.error('Failed to store auth token:', error)
    }
  }

  function clearToken(): void {
    try {
      localStorage.removeItem(TOKEN_KEY)
      localStorage.removeItem(TOKEN_EXPIRY_KEY)
    } catch (error) {
      console.error('Failed to clear auth token:', error)
    }
  }

  return {
    getToken,
    setToken,
    clearToken
  }
}
