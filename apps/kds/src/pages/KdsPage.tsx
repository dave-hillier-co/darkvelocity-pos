import { useState } from 'react'
import { useKds } from '../contexts/KdsContext'
import { Sidebar } from '../components/Sidebar'
import { TabBar } from '../components/TabBar'
import { TicketGrid } from '../components/TicketGrid'
import { AllDayCountsView } from '../components/AllDayCountsView'

export default function KdsPage() {
  const { currentView, openTickets, completedTickets } = useKds()
  const [showAllDayCounts, setShowAllDayCounts] = useState(false)

  if (showAllDayCounts) {
    return (
      <main className="kds-layout all-day-mode">
        <AllDayCountsView onClose={() => setShowAllDayCounts(false)} />
      </main>
    )
  }

  return (
    <main className="kds-layout">
      <Sidebar
        showAllDayCounts={showAllDayCounts}
        onToggleAllDayCounts={() => setShowAllDayCounts(true)}
      />
      <section className="main-content">
        <TabBar />
        {currentView === 'open' ? (
          <TicketGrid
            tickets={openTickets}
            emptyMessage="No open tickets"
          />
        ) : (
          <TicketGrid
            tickets={completedTickets}
            showRecall
            emptyMessage="No completed tickets"
          />
        )}
      </section>
    </main>
  )
}
