import { Link } from 'react-router-dom'
import { useKds } from '../contexts/KdsContext'
import { useSettings } from '../contexts/SettingsContext'

interface SidebarProps {
  showAllDayCounts: boolean
  onToggleAllDayCounts: () => void
}

export function Sidebar({ showAllDayCounts, onToggleAllDayCounts }: SidebarProps) {
  const { openTickets } = useKds()
  const { settings } = useSettings()

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
    <aside className="sidebar">
      <header className="sidebar-header">
        <h2>{settings.name}</h2>
        <span className="station-type">{settings.type === 'prep' ? 'Prep' : 'Expo'}</span>
      </header>

      <nav className="sidebar-nav">
        <button
          type="button"
          className={`nav-item ${showAllDayCounts ? 'active' : ''}`}
          onClick={onToggleAllDayCounts}
        >
          All-Day Counts
        </button>
        <Link to="/settings" className="nav-item">
          Settings
        </Link>
      </nav>

      <section className="mini-all-day" aria-label="Item counts summary">
        <h3>Quick Counts</h3>
        {sortedCounts.length === 0 ? (
          <p className="empty-counts">No pending items</p>
        ) : (
          <ul className="count-list">
            {sortedCounts.slice(0, 5).map(([name, count]) => (
              <li key={name} className="count-item">
                <span className="count-value">{count}</span>
                <span className="count-name">{name}</span>
              </li>
            ))}
            {sortedCounts.length > 5 && (
              <li className="count-more">+{sortedCounts.length - 5} more</li>
            )}
          </ul>
        )}
      </section>
    </aside>
  )
}
