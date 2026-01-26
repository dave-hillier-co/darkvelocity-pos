import type { KdsView } from '../types'
import { useKds } from '../contexts/KdsContext'

export function TabBar() {
  const { currentView, openTickets, completedTickets, setView } = useKds()

  const tabs: { view: KdsView; label: string; count: number }[] = [
    { view: 'open', label: 'Open', count: openTickets.length },
    { view: 'completed', label: 'Completed', count: completedTickets.length },
  ]

  return (
    <nav className="tab-bar" role="tablist">
      {tabs.map((tab) => (
        <button
          key={tab.view}
          type="button"
          role="tab"
          aria-selected={currentView === tab.view}
          className={`tab ${currentView === tab.view ? 'active' : ''}`}
          onClick={() => setView(tab.view)}
        >
          {tab.label} ({tab.count})
        </button>
      ))}
    </nav>
  )
}
