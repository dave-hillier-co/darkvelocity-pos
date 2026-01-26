import type { Ticket as TicketType } from '../types'
import { Ticket } from './Ticket'

interface TicketGridProps {
  tickets: TicketType[]
  showRecall?: boolean
  emptyMessage?: string
}

export function TicketGrid({ tickets, showRecall, emptyMessage = 'No tickets' }: TicketGridProps) {
  if (tickets.length === 0) {
    return (
      <section className="ticket-grid empty">
        <p className="empty-message">{emptyMessage}</p>
      </section>
    )
  }

  return (
    <section className="ticket-grid">
      {tickets.map((ticket) => (
        <Ticket key={ticket.id} ticket={ticket} showRecall={showRecall} />
      ))}
    </section>
  )
}
