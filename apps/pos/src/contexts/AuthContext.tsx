import { createContext, useContext, useReducer, useEffect, type ReactNode } from 'react'
import type { User, LoginResponse } from '../types'
import { apiClient } from '../api/client'
import { useDeviceAuth } from './DeviceAuthContext'

interface AuthState {
  user: User | null
  accessToken: string | null
  refreshToken: string | null
  sessionId: string | null
  isLoading: boolean
  error: string | null
}

type AuthAction =
  | { type: 'AUTH_STARTED' }
  | { type: 'AUTH_SUCCEEDED'; payload: { user: User; accessToken: string; refreshToken: string; sessionId?: string } }
  | { type: 'AUTH_FAILED'; payload: string }
  | { type: 'LOGGED_OUT' }

const initialState: AuthState = {
  user: null,
  accessToken: null,
  refreshToken: null,
  sessionId: null,
  isLoading: true,
  error: null,
}

function authReducer(state: AuthState, action: AuthAction): AuthState {
  switch (action.type) {
    case 'AUTH_STARTED':
      return { ...state, isLoading: true, error: null }
    case 'AUTH_SUCCEEDED':
      return {
        ...state,
        user: action.payload.user,
        accessToken: action.payload.accessToken,
        refreshToken: action.payload.refreshToken,
        sessionId: action.payload.sessionId ?? null,
        isLoading: false,
        error: null,
      }
    case 'AUTH_FAILED':
      return {
        ...state,
        user: null,
        accessToken: null,
        refreshToken: null,
        sessionId: null,
        isLoading: false,
        error: action.payload,
      }
    case 'LOGGED_OUT':
      return { ...initialState, isLoading: false }
    default:
      return state
  }
}

interface AuthContextValue extends AuthState {
  loginWithPin: (pin: string) => Promise<void>
  loginWithQr: (token: string) => Promise<void>
  logout: () => void
}

const AuthContext = createContext<AuthContextValue | null>(null)

const STORAGE_KEY = 'darkvelocity_auth'

interface PinLoginResponse {
  accessToken: string
  refreshToken: string
  expiresIn: number
  userId: string
  displayName: string
}

export function AuthProvider({ children }: { children: ReactNode }) {
  const [state, dispatch] = useReducer(authReducer, initialState)
  const { organizationId, siteId, deviceId, isDeviceAuthenticated } = useDeviceAuth()

  // Load stored auth on mount
  useEffect(() => {
    const stored = localStorage.getItem(STORAGE_KEY)
    if (stored) {
      try {
        const { accessToken, refreshToken, user, sessionId } = JSON.parse(stored)
        apiClient.setToken(accessToken)
        dispatch({
          type: 'AUTH_SUCCEEDED',
          payload: { accessToken, refreshToken, user, sessionId },
        })
      } catch {
        localStorage.removeItem(STORAGE_KEY)
        dispatch({ type: 'LOGGED_OUT' })
      }
    } else {
      dispatch({ type: 'LOGGED_OUT' })
    }
  }, [])

  // Persist auth to storage
  useEffect(() => {
    if (state.accessToken && state.user) {
      localStorage.setItem(
        STORAGE_KEY,
        JSON.stringify({
          accessToken: state.accessToken,
          refreshToken: state.refreshToken,
          user: state.user,
          sessionId: state.sessionId,
        })
      )
    }
  }, [state.accessToken, state.refreshToken, state.user, state.sessionId])

  // Set tenant context when device auth changes
  useEffect(() => {
    if (organizationId && siteId) {
      apiClient.setTenantContext({ orgId: organizationId, siteId })
    } else {
      apiClient.setTenantContext(null)
    }
  }, [organizationId, siteId])

  async function loginWithPin(pin: string) {
    if (!isDeviceAuthenticated || !organizationId || !siteId || !deviceId) {
      dispatch({ type: 'AUTH_FAILED', payload: 'Device not authenticated' })
      throw new Error('Device not authenticated')
    }

    dispatch({ type: 'AUTH_STARTED' })
    try {
      const response = await fetch(`${import.meta.env.VITE_API_URL || 'http://localhost:5000'}/api/auth/pin`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          pin,
          organizationId,
          siteId,
          deviceId,
        }),
      })

      if (!response.ok) {
        const error = await response.json().catch(() => ({ error_description: 'Login failed' }))
        throw new Error(error.error_description || 'Login failed')
      }

      const data: PinLoginResponse = await response.json()

      // Build user object from response
      const user: User = {
        id: data.userId,
        displayName: data.displayName,
      }

      apiClient.setToken(data.accessToken)
      dispatch({
        type: 'AUTH_SUCCEEDED',
        payload: {
          accessToken: data.accessToken,
          refreshToken: data.refreshToken,
          user,
        },
      })
    } catch (err) {
      const message = err instanceof Error ? err.message : 'Login failed'
      dispatch({ type: 'AUTH_FAILED', payload: message })
      throw err
    }
  }

  async function loginWithQr(token: string) {
    // QR login could work similarly to PIN login
    // For now, keep the legacy behavior
    dispatch({ type: 'AUTH_STARTED' })
    try {
      const response = await fetch(`${import.meta.env.VITE_API_URL || 'http://localhost:5000'}/api/auth/login/qr`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ token, locationId: siteId }),
      })

      if (!response.ok) {
        const error = await response.json().catch(() => ({ message: 'Login failed' }))
        throw new Error(error.message || 'Login failed')
      }

      const data: LoginResponse = await response.json()
      apiClient.setToken(data.accessToken)
      dispatch({
        type: 'AUTH_SUCCEEDED',
        payload: {
          accessToken: data.accessToken,
          refreshToken: data.refreshToken,
          user: data.user,
        },
      })
    } catch (err) {
      const message = err instanceof Error ? err.message : 'Login failed'
      dispatch({ type: 'AUTH_FAILED', payload: message })
      throw err
    }
  }

  function logout() {
    localStorage.removeItem(STORAGE_KEY)
    apiClient.setToken(null)
    dispatch({ type: 'LOGGED_OUT' })
  }

  return (
    <AuthContext.Provider
      value={{
        ...state,
        loginWithPin,
        loginWithQr,
        logout,
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
