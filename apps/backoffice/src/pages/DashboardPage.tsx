import { useEffect } from 'react'
import { useNavigate } from 'react-router-dom'
import { useReports } from '../contexts/ReportsContext'

function formatCurrency(amount: number): string {
  return new Intl.NumberFormat('en-GB', {
    style: 'currency',
    currency: 'GBP',
  }).format(amount)
}

export default function DashboardPage() {
  const navigate = useNavigate()
  const { dailySales, costAlerts, isLoading, error, loadDailySales, loadCostAlerts } = useReports()

  useEffect(() => {
    loadDailySales()
    loadCostAlerts()
  }, [])

  const todaySales = dailySales.length > 0 ? dailySales[dailySales.length - 1] : null
  const activeAlertCount = costAlerts.filter((a) => !a.isAcknowledged).length

  return (
    <>
      <hgroup>
        <h1>Dashboard</h1>
        <p>Overview of today's performance</p>
      </hgroup>

      {error && (
        <article style={{ background: 'var(--pico-del-color)', padding: '1rem', marginBottom: '1rem' }}>
          <p>{error}</p>
        </article>
      )}

      <div className="cards-grid">
        <article aria-busy={isLoading}>
          <header>Today's Sales</header>
          <p style={{ fontSize: '2rem', fontWeight: 700 }}>
            {todaySales ? formatCurrency(todaySales.grossRevenue) : '--'}
          </p>
          <footer>
            <small>{todaySales ? `${todaySales.orderCount} orders` : 'No data yet'}</small>
          </footer>
        </article>

        <article aria-busy={isLoading}>
          <header>Orders</header>
          <p style={{ fontSize: '2rem', fontWeight: 700 }}>
            {todaySales ? todaySales.orderCount : '--'}
          </p>
          <footer>
            <small>
              {todaySales && todaySales.orderCount > 0
                ? `Average ${formatCurrency(todaySales.grossRevenue / todaySales.orderCount)} per order`
                : 'No orders today'}
            </small>
          </footer>
        </article>

        <article aria-busy={isLoading}>
          <header>Gross Margin</header>
          <p style={{ fontSize: '2rem', fontWeight: 700 }}>
            {todaySales ? `${todaySales.grossMarginPercent.toFixed(1)}%` : '--'}
          </p>
          <footer>
            <small>{todaySales ? `COGS: ${formatCurrency(todaySales.totalCOGS)}` : 'No data'}</small>
          </footer>
        </article>

        <article aria-busy={isLoading}>
          <header>Cost Alerts</header>
          <p style={{ fontSize: '2rem', fontWeight: 700, color: activeAlertCount > 0 ? 'var(--pico-del-color)' : undefined }}>
            {activeAlertCount}
          </p>
          <footer>
            <small>{activeAlertCount > 0 ? 'Items requiring attention' : 'All clear'}</small>
          </footer>
        </article>
      </div>

      <section style={{ marginTop: '2rem' }}>
        <h2>Quick Actions</h2>
        <div style={{ display: 'flex', gap: '1rem', flexWrap: 'wrap' }}>
          <button onClick={() => navigate('/procurement/purchase-orders')}>Upload Purchase Document</button>
          <button className="secondary" onClick={() => navigate('/procurement/deliveries')}>View Deliveries</button>
          <button className="secondary" onClick={() => navigate('/reports/margins')}>View Margins</button>
        </div>
      </section>
    </>
  )
}
