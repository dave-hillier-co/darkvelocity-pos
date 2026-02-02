import { createContext, useContext, useState, useEffect, useCallback, type ReactNode } from 'react'
import { apiClient } from '../api/client'

interface DeviceAuthState {
  isDeviceAuthenticated: boolean
  deviceId: string | null
  organizationId: string | null
  siteId: string | null
  deviceToken: string | null
  deviceName: string | null
  isLoading: boolean
  error: string | null
}

interface DeviceCodeResponse {
  deviceCode: string
  userCode: string
  verificationUri: string
  verificationUriComplete: string
  expiresIn: number
  interval: number
}

interface DeviceAuthContextValue extends DeviceAuthState {
  requestDeviceCode: () => Promise<DeviceCodeResponse>
  pollForToken: (deviceCode: string, userCode: string, interval: number) => void
  stopPolling: () => void
  clearDeviceAuth: () => void
}

const DeviceAuthContext = createContext<DeviceAuthContextValue | null>(null)

const DEVICE_STORAGE_KEY = 'darkvelocity_device'

export function DeviceAuthProvider({ children }: { children: ReactNode }) {
  const [state, setState] = useState<DeviceAuthState>({
    isDeviceAuthenticated: false,
    deviceId: null,
    organizationId: null,
    siteId: null,
    deviceToken: null,
    deviceName: null,
    isLoading: true,
    error: null,
  })
  const [pollingInterval, setPollingInterval] = useState<number | null>(null)

  // Check for stored device auth on mount
  useEffect(() => {
    const stored = localStorage.getItem(DEVICE_STORAGE_KEY)
    if (stored) {
      try {
        const data = JSON.parse(stored)
        setState({
          isDeviceAuthenticated: true,
          deviceToken: data.deviceToken,
          deviceId: data.deviceId,
          organizationId: data.organizationId,
          siteId: data.siteId,
          deviceName: data.deviceName,
          isLoading: false,
          error: null,
        })
      } catch {
        localStorage.removeItem(DEVICE_STORAGE_KEY)
        setState(prev => ({ ...prev, isLoading: false }))
      }
    } else {
      setState(prev => ({ ...prev, isLoading: false }))
    }
  }, [])

  // Clean up polling on unmount
  useEffect(() => {
    return () => {
      if (pollingInterval) {
        clearInterval(pollingInterval)
      }
    }
  }, [pollingInterval])

  const requestDeviceCode = useCallback(async (): Promise<DeviceCodeResponse> => {
    setState(prev => ({ ...prev, error: null }))

    const response = await apiClient.post<DeviceCodeResponse>('/api/device/code', {
      clientId: 'pos-app',
      scope: 'device pos',
      deviceFingerprint: await getDeviceFingerprint(),
    })

    return response
  }, [])

  const pollForToken = useCallback((deviceCode: string, userCode: string, interval: number) => {
    // Clear any existing polling
    if (pollingInterval) {
      clearInterval(pollingInterval)
    }

    const poll = async () => {
      try {
        const response = await fetch(`${import.meta.env.VITE_API_URL || 'http://localhost:5000'}/api/device/token`, {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({ userCode, deviceCode }),
        })

        const data = await response.json()

        if (response.ok && data.accessToken) {
          // Success - device authorized
          const authData = {
            deviceToken: data.accessToken,
            deviceId: data.deviceId,
            organizationId: data.organizationId,
            siteId: data.siteId,
            deviceName: null, // Will be set after authorization
          }

          localStorage.setItem(DEVICE_STORAGE_KEY, JSON.stringify(authData))

          setState({
            isDeviceAuthenticated: true,
            deviceToken: data.accessToken,
            deviceId: data.deviceId,
            organizationId: data.organizationId,
            siteId: data.siteId,
            deviceName: null,
            isLoading: false,
            error: null,
          })

          // Stop polling
          if (pollingInterval) {
            clearInterval(pollingInterval)
            setPollingInterval(null)
          }
        } else if (data.error === 'authorization_pending') {
          // Keep polling - do nothing
        } else if (data.error === 'expired_token') {
          setState(prev => ({ ...prev, error: 'Device code expired. Please try again.' }))
          if (pollingInterval) {
            clearInterval(pollingInterval)
            setPollingInterval(null)
          }
        } else if (data.error === 'access_denied') {
          setState(prev => ({ ...prev, error: 'Authorization denied.' }))
          if (pollingInterval) {
            clearInterval(pollingInterval)
            setPollingInterval(null)
          }
        }
      } catch {
        // Network error - continue polling
      }
    }

    // Start polling
    const intervalId = setInterval(poll, interval * 1000) as unknown as number
    setPollingInterval(intervalId)

    // Also poll immediately
    poll()
  }, [pollingInterval])

  const stopPolling = useCallback(() => {
    if (pollingInterval) {
      clearInterval(pollingInterval)
      setPollingInterval(null)
    }
  }, [pollingInterval])

  const clearDeviceAuth = useCallback(() => {
    localStorage.removeItem(DEVICE_STORAGE_KEY)
    setState({
      isDeviceAuthenticated: false,
      deviceId: null,
      organizationId: null,
      siteId: null,
      deviceToken: null,
      deviceName: null,
      isLoading: false,
      error: null,
    })
  }, [])

  return (
    <DeviceAuthContext.Provider
      value={{
        ...state,
        requestDeviceCode,
        pollForToken,
        stopPolling,
        clearDeviceAuth,
      }}
    >
      {children}
    </DeviceAuthContext.Provider>
  )
}

export function useDeviceAuth() {
  const context = useContext(DeviceAuthContext)
  if (!context) {
    throw new Error('useDeviceAuth must be used within a DeviceAuthProvider')
  }
  return context
}

async function getDeviceFingerprint(): Promise<string> {
  // Generate a fingerprint from available device info
  const data = [
    navigator.userAgent,
    navigator.language,
    screen.width + 'x' + screen.height,
    new Date().getTimezoneOffset().toString(),
  ].join('|')

  const encoder = new TextEncoder()
  const hashBuffer = await crypto.subtle.digest('SHA-256', encoder.encode(data))
  return Array.from(new Uint8Array(hashBuffer))
    .map(b => b.toString(16).padStart(2, '0'))
    .join('')
}
