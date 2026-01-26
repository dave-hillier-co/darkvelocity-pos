import type { Ticket, KdsView, TicketStatus } from '../types'

export type KdsAction =
  | { type: 'TICKETS_LOADED'; payload: { tickets: Ticket[] } }
  | { type: 'TICKET_RECEIVED'; payload: { ticket: Ticket } }
  | { type: 'LINE_ITEM_COMPLETED'; payload: { ticketId: string; itemId: string } }
  | { type: 'TICKET_COMPLETED'; payload: { ticketId: string } }
  | { type: 'TICKET_PRIORITIZED'; payload: { ticketId: string } }
  | { type: 'TICKET_RECALLED'; payload: { ticketId: string } }
  | { type: 'VIEW_CHANGED'; payload: { view: KdsView } }

export interface KdsState {
  tickets: Ticket[]
  currentView: KdsView
}

export const initialKdsState: KdsState = {
  tickets: [],
  currentView: 'open',
}

function updateTicketStatus(ticket: Ticket): TicketStatus {
  const allCompleted = ticket.items.every((item) => item.status === 'completed')
  const someCompleted = ticket.items.some((item) => item.status === 'completed')

  if (allCompleted) return 'completed'
  if (someCompleted) return 'in_progress'
  return 'pending'
}

export function kdsReducer(state: KdsState, action: KdsAction): KdsState {
  switch (action.type) {
    case 'TICKETS_LOADED': {
      return {
        ...state,
        tickets: action.payload.tickets,
      }
    }

    case 'TICKET_RECEIVED': {
      const existingIndex = state.tickets.findIndex(
        (t) => t.id === action.payload.ticket.id
      )
      if (existingIndex >= 0) {
        const newTickets = [...state.tickets]
        newTickets[existingIndex] = action.payload.ticket
        return { ...state, tickets: newTickets }
      }
      return {
        ...state,
        tickets: [...state.tickets, action.payload.ticket],
      }
    }

    case 'LINE_ITEM_COMPLETED': {
      const { ticketId, itemId } = action.payload
      const newTickets = state.tickets.map((ticket) => {
        if (ticket.id !== ticketId) return ticket

        const newItems = ticket.items.map((item) =>
          item.id === itemId
            ? { ...item, status: 'completed' as const, completedAt: new Date().toISOString() }
            : item
        )

        const updatedTicket = { ...ticket, items: newItems }
        const newStatus = updateTicketStatus(updatedTicket)

        return {
          ...updatedTicket,
          status: newStatus,
          completedAt: newStatus === 'completed' ? new Date().toISOString() : undefined,
        }
      })

      return { ...state, tickets: newTickets }
    }

    case 'TICKET_COMPLETED': {
      const { ticketId } = action.payload
      const newTickets = state.tickets.map((ticket) => {
        if (ticket.id !== ticketId) return ticket

        const newItems = ticket.items.map((item) =>
          item.status === 'pending'
            ? { ...item, status: 'completed' as const, completedAt: new Date().toISOString() }
            : item
        )

        return {
          ...ticket,
          items: newItems,
          status: 'completed' as const,
          completedAt: new Date().toISOString(),
        }
      })

      return { ...state, tickets: newTickets }
    }

    case 'TICKET_PRIORITIZED': {
      const { ticketId } = action.payload
      const newTickets = state.tickets.map((ticket) =>
        ticket.id === ticketId
          ? { ...ticket, isPrioritized: !ticket.isPrioritized }
          : ticket
      )

      return { ...state, tickets: newTickets }
    }

    case 'TICKET_RECALLED': {
      const { ticketId } = action.payload
      const newTickets = state.tickets.map((ticket) => {
        if (ticket.id !== ticketId) return ticket

        const newItems = ticket.items.map((item) => ({
          ...item,
          status: 'pending' as const,
          completedAt: undefined,
        }))

        return {
          ...ticket,
          items: newItems,
          status: 'pending' as const,
          completedAt: undefined,
        }
      })

      return { ...state, tickets: newTickets }
    }

    case 'VIEW_CHANGED': {
      return {
        ...state,
        currentView: action.payload.view,
      }
    }

    default:
      return state
  }
}
