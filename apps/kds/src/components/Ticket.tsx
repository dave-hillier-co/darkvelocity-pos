import type { Ticket as TicketType } from '../types'
import { useTicketTimer } from '../hooks/useTicketTimer'
import { useKds } from '../contexts/KdsContext'
import { useSettings } from '../contexts/SettingsContext'
import { TicketLineItem } from './TicketLineItem'

interface TicketProps {
  ticket: TicketType
  showRecall?: boolean
}

const orderTypeLabels: Record<TicketType['orderType'], string> = {
  for_here: 'For Here',
  takeout: 'Takeout',
  delivery: 'Delivery',
}

export function Ticket({ ticket, showRecall }: TicketProps) {
  const { completeLineItem, completeTicket, prioritizeTicket, recallTicket } = useKds()
  const { settings } = useSettings()
  const { formattedTime, urgency } = useTicketTimer(ticket.createdAt)

  const isCompleted = ticket.status === 'completed'

  function handleHeaderClick() {
    if (!isCompleted) {
      completeTicket(ticket.id)
    }
  }

  return (
    <article className={`ticket urgency-${urgency} ${isCompleted ? 'completed' : ''}`}>
      <header
        className="ticket-header"
        onClick={handleHeaderClick}
        role="button"
        tabIndex={isCompleted ? -1 : 0}
        onKeyDown={(e) => {
          if (!isCompleted && (e.key === 'Enter' || e.key === ' ')) {
            e.preventDefault()
            handleHeaderClick()
          }
        }}
        aria-label={`Complete ticket ${ticket.orderNumber}`}
      >
        <span className="order-type">{orderTypeLabels[ticket.orderType]}</span>
        <span className="order-info">
          <span className="order-number">{ticket.orderNumber}</span>
          {ticket.customerName && (
            <span className="customer-name">{ticket.customerName}</span>
          )}
        </span>
        <span className="ticket-time">{formattedTime}</span>
        {ticket.isPrioritized && <span className="priority-badge">P</span>}
      </header>

      {ticket.tableName && (
        <p className="table-name">{ticket.tableName}</p>
      )}

      <ul className="ticket-items">
        {ticket.items.map((item) => (
          <TicketLineItem
            key={item.id}
            item={item}
            onComplete={() => completeLineItem(ticket.id, item.id)}
            disabled={isCompleted}
          />
        ))}
      </ul>

      <footer className="ticket-footer">
        {showRecall && isCompleted ? (
          <button
            type="button"
            className="recall-btn"
            onClick={() => recallTicket(ticket.id)}
          >
            Recall
          </button>
        ) : (
          settings.type === 'prep' && !isCompleted && (
            <button
              type="button"
              className={`prioritize-btn ${ticket.isPrioritized ? 'active' : ''}`}
              onClick={() => prioritizeTicket(ticket.id)}
            >
              {ticket.isPrioritized ? 'Unprioritize' : 'Prioritize'}
            </button>
          )
        )}
      </footer>
    </article>
  )
}
