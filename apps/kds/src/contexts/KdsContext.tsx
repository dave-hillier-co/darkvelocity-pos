import { createContext, useContext, useReducer, useEffect, type ReactNode } from 'react'
import type { Ticket, KdsView } from '../types'
import { kdsReducer, initialKdsState, type KdsState } from '../reducers/kdsReducer'
import { sampleTickets } from '../data/sampleTickets'

interface KdsContextValue extends KdsState {
  openTickets: Ticket[]
  completedTickets: Ticket[]
  completeLineItem: (ticketId: string, itemId: string) => void
  completeTicket: (ticketId: string) => void
  prioritizeTicket: (ticketId: string) => void
  recallTicket: (ticketId: string) => void
  setView: (view: KdsView) => void
}

const KdsContext = createContext<KdsContextValue | null>(null)

export function KdsProvider({ children }: { children: ReactNode }) {
  const [state, dispatch] = useReducer(kdsReducer, initialKdsState)

  useEffect(() => {
    dispatch({ type: 'TICKETS_LOADED', payload: { tickets: sampleTickets } })
  }, [])

  const openTickets = state.tickets
    .filter((t) => t.status !== 'completed')
    .sort((a, b) => {
      if (a.isPrioritized && !b.isPrioritized) return -1
      if (!a.isPrioritized && b.isPrioritized) return 1
      return new Date(a.createdAt).getTime() - new Date(b.createdAt).getTime()
    })

  const completedTickets = state.tickets
    .filter((t) => t.status === 'completed')
    .sort((a, b) => {
      const aTime = a.completedAt ? new Date(a.completedAt).getTime() : 0
      const bTime = b.completedAt ? new Date(b.completedAt).getTime() : 0
      return bTime - aTime
    })

  function completeLineItem(ticketId: string, itemId: string) {
    dispatch({ type: 'LINE_ITEM_COMPLETED', payload: { ticketId, itemId } })
  }

  function completeTicket(ticketId: string) {
    dispatch({ type: 'TICKET_COMPLETED', payload: { ticketId } })
  }

  function prioritizeTicket(ticketId: string) {
    dispatch({ type: 'TICKET_PRIORITIZED', payload: { ticketId } })
  }

  function recallTicket(ticketId: string) {
    dispatch({ type: 'TICKET_RECALLED', payload: { ticketId } })
  }

  function setView(view: KdsView) {
    dispatch({ type: 'VIEW_CHANGED', payload: { view } })
  }

  return (
    <KdsContext.Provider
      value={{
        ...state,
        openTickets,
        completedTickets,
        completeLineItem,
        completeTicket,
        prioritizeTicket,
        recallTicket,
        setView,
      }}
    >
      {children}
    </KdsContext.Provider>
  )
}

export function useKds() {
  const context = useContext(KdsContext)
  if (!context) {
    throw new Error('useKds must be used within a KdsProvider')
  }
  return context
}
