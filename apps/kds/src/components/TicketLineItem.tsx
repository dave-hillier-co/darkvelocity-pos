import type { TicketLineItem as LineItemType } from '../types'

interface TicketLineItemProps {
  item: LineItemType
  onComplete: () => void
  disabled?: boolean
}

export function TicketLineItem({ item, onComplete, disabled }: TicketLineItemProps) {
  const isCompleted = item.status === 'completed'

  return (
    <li
      className={`ticket-line-item ${isCompleted ? 'completed' : ''}`}
      onClick={!disabled && !isCompleted ? onComplete : undefined}
      role="button"
      tabIndex={disabled || isCompleted ? -1 : 0}
      onKeyDown={(e) => {
        if (!disabled && !isCompleted && (e.key === 'Enter' || e.key === ' ')) {
          e.preventDefault()
          onComplete()
        }
      }}
      aria-disabled={disabled || isCompleted}
    >
      <span className="item-quantity">{item.quantity}x</span>
      <span className="item-details">
        <span className="item-name">{item.itemName}</span>
        {item.modifiers.length > 0 && (
          <ul className="item-modifiers">
            {item.modifiers.map((mod, idx) => (
              <li key={idx}>- {mod}</li>
            ))}
          </ul>
        )}
      </span>
    </li>
  )
}
