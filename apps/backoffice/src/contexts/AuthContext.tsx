import { createContext, useContext, useState, useEffect, useCallback, type ReactNode } from 'react'
import { apiClient } from '../api/client'

interface User {
  id: string
  displayName: string
  email?: string
  organizationId: string
  siteId?: string
  roles: string[]
}

interface AuthState {
  user: User | null
  accessToken: string | null
  refreshToken: string | null
  isLoading: boolean
  error: string | null
}

interface AuthContextValue extends AuthState {
  loginWithGoogle: () => void
  loginWithMicrosoft: () => void
  logout: () => void
  isAuthenticated: boolean
}

const AuthContext = createContext<AuthContextValue | null>(null)

const STORAGE_KEY = 'darkvelocity_backoffice_auth'
const API_URL = import.meta.env.VITE_API_URL || 'http://localhost:5000'

export function AuthProvider({ children }: { children: ReactNode }) {
  const [state, setState] = useState<AuthState>({
    user: null,
    accessToken: null,
    refreshToken: null,
    isLoading: true,
    error: null,
  })

  // Handle OAuth callback from URL fragment
  useEffect(() => {
    const hash = window.location.hash
    if (hash && hash.includes('access_token=')) {
      const params = new URLSearchParams(hash.substring(1))
      const accessToken = params.get('access_token')
      const refreshToken = params.get('refresh_token')
      const userId = params.get('user_id')
      const displayName = params.get('display_name')

      if (accessToken && userId && displayName) {
        const user: User = {
          id: userId,
          displayName: decodeURIComponent(displayName),
          organizationId: '00000000-0000-0000-0000-000000000001', // From token in production
          roles: ['admin', 'backoffice'],
        }

        localStorage.setItem(STORAGE_KEY, JSON.stringify({
          accessToken,
          refreshToken,
          user,
        }))

        setState({
          user,
          accessToken,
          refreshToken,
          isLoading: false,
          error: null,
        })

        // Clear the hash from URL
        window.history.replaceState(null, '', window.location.pathname)
      }
    }
  }, [])

  // Check for stored auth on mount
  useEffect(() => {
    const stored = localStorage.getItem(STORAGE_KEY)
    if (stored) {
      try {
        const data = JSON.parse(stored)
        setState({
          user: data.user,
          accessToken: data.accessToken,
          refreshToken: data.refreshToken,
          isLoading: false,
          error: null,
        })
      } catch {
        localStorage.removeItem(STORAGE_KEY)
        setState(prev => ({ ...prev, isLoading: false }))
      }
    } else {
      setState(prev => ({ ...prev, isLoading: false }))
    }
  }, [])

  // Handle error from OAuth callback
  useEffect(() => {
    const params = new URLSearchParams(window.location.search)
    const error = params.get('error')
    if (error) {
      setState(prev => ({
        ...prev,
        isLoading: false,
        error: error === 'auth_failed' ? 'Authentication failed' : 'An error occurred',
      }))
      // Clear the error from URL
      window.history.replaceState(null, '', window.location.pathname)
    }
  }, [])

  // Set API client token and tenant context when auth state changes
  useEffect(() => {
    if (state.accessToken) {
      apiClient.setToken(state.accessToken)
    } else {
      apiClient.setToken(null)
    }

    if (state.user?.organizationId) {
      // For backoffice, use the org's default site or a selected site
      // The siteId can come from user selection or URL params
      const siteId = state.user.siteId || '00000000-0000-0000-0000-000000000001'
      apiClient.setTenantContext({
        orgId: state.user.organizationId,
        siteId,
      })
    } else {
      apiClient.setTenantContext(null)
    }
  }, [state.accessToken, state.user])

  const loginWithGoogle = useCallback(() => {
    const returnUrl = encodeURIComponent(window.location.origin)
    window.location.href = `${API_URL}/api/oauth/login/google?returnUrl=${returnUrl}`
  }, [])

  const loginWithMicrosoft = useCallback(() => {
    const returnUrl = encodeURIComponent(window.location.origin)
    window.location.href = `${API_URL}/api/oauth/login/microsoft?returnUrl=${returnUrl}`
  }, [])

  const logout = useCallback(() => {
    localStorage.removeItem(STORAGE_KEY)
    setState({
      user: null,
      accessToken: null,
      refreshToken: null,
      isLoading: false,
      error: null,
    })
  }, [])

  return (
    <AuthContext.Provider
      value={{
        ...state,
        loginWithGoogle,
        loginWithMicrosoft,
        logout,
        isAuthenticated: !!state.user && !!state.accessToken,
      }}
    >
      {children}
    </AuthContext.Provider>
  )
}

export function useAuth() {
  const context = useContext(AuthContext)
  if (!context) {
    throw new Error('useAuth must be used within an AuthProvider')
  }
  return context
}
