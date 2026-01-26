import { useKds } from '../contexts/KdsContext'

interface AllDayCountsViewProps {
  onClose: () => void
}

export function AllDayCountsView({ onClose }: AllDayCountsViewProps) {
  const { openTickets } = useKds()

  const itemCounts = new Map<string, number>()
  for (const ticket of openTickets) {
    for (const item of ticket.items) {
      if (item.status !== 'completed') {
        const current = itemCounts.get(item.itemName) || 0
        itemCounts.set(item.itemName, current + item.quantity)
      }
    }
  }

  const sortedCounts = Array.from(itemCounts.entries()).sort((a, b) => b[1] - a[1])

  return (
    <section className="all-day-counts-view">
      <header className="all-day-header">
        <h2>All-Day Counts</h2>
        <button type="button" className="close-btn" onClick={onClose} aria-label="Close">
          X
        </button>
      </header>

      {sortedCounts.length === 0 ? (
        <p className="empty-message">No pending items</p>
      ) : (
        <ul className="all-day-grid">
          {sortedCounts.map(([name, count]) => (
            <li key={name} className="all-day-item">
              <span className="all-day-count">{count}</span>
              <span className="all-day-name">{name}</span>
            </li>
          ))}
        </ul>
      )}
    </section>
  )
}
