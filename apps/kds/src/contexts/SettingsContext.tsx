import { createContext, useContext, useState, useEffect, type ReactNode } from 'react'
import type { StationSettings } from '../types'

const STORAGE_KEY = 'kds-settings'

const defaultSettings: StationSettings = {
  id: crypto.randomUUID(),
  name: 'Kitchen Station 1',
  type: 'prep',
  layout: 'tiled',
  yellowThresholdSeconds: 300,
  redThresholdSeconds: 600,
}

interface SettingsContextValue {
  settings: StationSettings
  updateSettings: (settings: Partial<StationSettings>) => void
}

const SettingsContext = createContext<SettingsContextValue | null>(null)

function loadSettings(): StationSettings {
  try {
    const stored = localStorage.getItem(STORAGE_KEY)
    if (stored) {
      return { ...defaultSettings, ...JSON.parse(stored) }
    }
  } catch {
    // Ignore parse errors
  }
  return defaultSettings
}

function saveSettings(settings: StationSettings): void {
  try {
    localStorage.setItem(STORAGE_KEY, JSON.stringify(settings))
  } catch {
    // Ignore storage errors
  }
}

export function SettingsProvider({ children }: { children: ReactNode }) {
  const [settings, setSettings] = useState<StationSettings>(loadSettings)

  useEffect(() => {
    saveSettings(settings)
  }, [settings])

  function updateSettings(partial: Partial<StationSettings>) {
    setSettings((prev) => ({ ...prev, ...partial }))
  }

  return (
    <SettingsContext.Provider value={{ settings, updateSettings }}>
      {children}
    </SettingsContext.Provider>
  )
}

export function useSettings() {
  const context = useContext(SettingsContext)
  if (!context) {
    throw new Error('useSettings must be used within a SettingsProvider')
  }
  return context
}
