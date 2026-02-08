import { createContext, useContext, useState, useEffect, useCallback, type ReactNode } from 'react'
import { useDeviceAuth } from './DeviceAuthContext'

interface Station {
  id: string
  name: string
  orderTypes: string[]
}

interface StationState {
  stations: Station[]
  selectedStation: Station | null
  isLoading: boolean
  error: string | null
}

interface StationContextValue extends StationState {
  loadStations: () => Promise<void>
  selectStation: (station: Station) => Promise<void>
  clearStation: () => void
}

const StationContext = createContext<StationContextValue | null>(null)

const STATION_STORAGE_KEY = 'darkvelocity_kds_station'
const API_URL = import.meta.env.VITE_API_URL || 'http://localhost:5200'

export function StationProvider({ children }: { children: ReactNode }) {
  const { organizationId, siteId, deviceId, isDeviceAuthenticated } = useDeviceAuth()
  const [state, setState] = useState<StationState>({
    stations: [],
    selectedStation: null,
    isLoading: true,
    error: null,
  })

  // Load stored station on mount
  useEffect(() => {
    const stored = localStorage.getItem(STATION_STORAGE_KEY)
    if (stored) {
      try {
        const station = JSON.parse(stored)
        setState(prev => ({
          ...prev,
          selectedStation: station,
          isLoading: false,
        }))
      } catch {
        localStorage.removeItem(STATION_STORAGE_KEY)
        setState(prev => ({ ...prev, isLoading: false }))
      }
    } else {
      setState(prev => ({ ...prev, isLoading: false }))
    }
  }, [])

  const loadStations = useCallback(async () => {
    if (!organizationId || !siteId) {
      setState(prev => ({ ...prev, error: 'Device not authenticated' }))
      return
    }

    setState(prev => ({ ...prev, isLoading: true, error: null }))

    try {
      const response = await fetch(`${API_URL}/api/stations/${organizationId}/${siteId}`)
      if (!response.ok) {
        throw new Error('Failed to load stations')
      }
      const data = await response.json()
      setState(prev => ({
        ...prev,
        stations: data.items,
        isLoading: false,
      }))
    } catch (err) {
      setState(prev => ({
        ...prev,
        error: err instanceof Error ? err.message : 'Failed to load stations',
        isLoading: false,
      }))
    }
  }, [organizationId, siteId])

  // Load stations when device is authenticated
  useEffect(() => {
    if (isDeviceAuthenticated && organizationId && siteId) {
      loadStations()
    }
  }, [isDeviceAuthenticated, organizationId, siteId, loadStations])

  const selectStation = useCallback(async (station: Station) => {
    if (!organizationId || !siteId || !deviceId) {
      setState(prev => ({ ...prev, error: 'Device not authenticated' }))
      return
    }

    setState(prev => ({ ...prev, isLoading: true, error: null }))

    try {
      const response = await fetch(`${API_URL}/api/stations/${organizationId}/${siteId}/select`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          deviceId,
          stationId: station.id,
          stationName: station.name,
        }),
      })

      if (!response.ok) {
        throw new Error('Failed to select station')
      }

      localStorage.setItem(STATION_STORAGE_KEY, JSON.stringify(station))
      setState(prev => ({
        ...prev,
        selectedStation: station,
        isLoading: false,
      }))
    } catch (err) {
      setState(prev => ({
        ...prev,
        error: err instanceof Error ? err.message : 'Failed to select station',
        isLoading: false,
      }))
    }
  }, [organizationId, siteId, deviceId])

  const clearStation = useCallback(() => {
    localStorage.removeItem(STATION_STORAGE_KEY)
    setState(prev => ({
      ...prev,
      selectedStation: null,
    }))
  }, [])

  return (
    <StationContext.Provider
      value={{
        ...state,
        loadStations,
        selectStation,
        clearStation,
      }}
    >
      {children}
    </StationContext.Provider>
  )
}

export function useStation() {
  const context = useContext(StationContext)
  if (!context) {
    throw new Error('useStation must be used within a StationProvider')
  }
  return context
}
