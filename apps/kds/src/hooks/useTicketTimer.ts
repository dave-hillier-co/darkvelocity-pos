import { useState, useEffect } from 'react'
import type { TicketUrgency } from '../types'
import { useSettings } from '../contexts/SettingsContext'

interface TicketTimerResult {
  elapsedSeconds: number
  formattedTime: string
  urgency: TicketUrgency
}

export function useTicketTimer(createdAt: string): TicketTimerResult {
  const { settings } = useSettings()
  const [elapsedSeconds, setElapsedSeconds] = useState(() =>
    Math.floor((Date.now() - new Date(createdAt).getTime()) / 1000)
  )

  useEffect(() => {
    const interval = setInterval(() => {
      setElapsedSeconds(
        Math.floor((Date.now() - new Date(createdAt).getTime()) / 1000)
      )
    }, 1000)

    return () => clearInterval(interval)
  }, [createdAt])

  const minutes = Math.floor(elapsedSeconds / 60)
  const seconds = elapsedSeconds % 60
  const formattedTime = `${minutes}:${seconds.toString().padStart(2, '0')}`

  let urgency: TicketUrgency = 'normal'
  if (elapsedSeconds >= settings.redThresholdSeconds) {
    urgency = 'critical'
  } else if (elapsedSeconds >= settings.yellowThresholdSeconds) {
    urgency = 'warning'
  }

  return { elapsedSeconds, formattedTime, urgency }
}
