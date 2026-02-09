import { useState, useEffect } from 'react'
import { useReports } from '../contexts/ReportsContext'

function formatCurrency(amount: number): string {
  return new Intl.NumberFormat('en-GB', {
    style: 'currency',
    currency: 'GBP',
  }).format(amount)
}

function formatDate(dateStr: string): string {
  return new Date(dateStr).toLocaleDateString('en-GB', {
    weekday: 'short',
    day: '2-digit',
    month: 'short',
  })
}

function getMarginClass(actual: number, target: number): string {
  const diff = actual - target
  if (diff >= 0) return 'badge-success'
  if (diff >= -5) return 'badge-warning'
  return 'badge-danger'
}

export default function MarginAnalysisPage() {
  const {
    dailySales,
    itemMargins,
    costAlerts,
    dateRange,
    isLoading,
    error,
    loadDailySales,
    loadItemMargins,
    loadCostAlerts,
    setDateRange,
  } = useReports()

  const [view, setView] = useState<'items' | 'daily'>('items')
  const [categoryFilter, setCategoryFilter] = useState<string>('all')

  useEffect(() => {
    loadDailySales()
    loadItemMargins()
    loadCostAlerts()
  }, [])

  const categories = [...new Set(itemMargins.map((m) => m.categoryName))]

  const filteredItems = categoryFilter === 'all'
    ? itemMargins
    : itemMargins.filter((m) => m.categoryName === categoryFilter)

  const totals = {
    revenue: dailySales.reduce((sum, d) => sum + d.grossRevenue, 0),
    cogs: dailySales.reduce((sum, d) => sum + d.totalCOGS, 0),
    orders: dailySales.reduce((sum, d) => sum + d.orderCount, 0),
  }
  const grossProfit = totals.revenue - totals.cogs
  const overallMargin = totals.revenue > 0 ? (grossProfit / totals.revenue) * 100 : 0

  const underperforming = itemMargins.filter((m) => m.marginPercent < m.targetMarginPercent)
  const activeAlerts = costAlerts.filter((a) => !a.isAcknowledged)

  return (
    <>
      <hgroup>
        <h1>Margin Analysis</h1>
        <p>Track profitability and cost of goods sold</p>
      </hgroup>

      {error && (
        <article style={{ background: 'var(--pico-del-color)', padding: '1rem', marginBottom: '1rem' }}>
          <p>{error}</p>
        </article>
      )}

      <div style={{ marginBottom: '1rem', display: 'flex', gap: '1rem', alignItems: 'center' }}>
        <label style={{ margin: 0 }}>
          From
          <input
            type="date"
            value={dateRange.start}
            onChange={(e) => setDateRange(e.target.value, dateRange.end)}
          />
        </label>
        <label style={{ margin: 0 }}>
          To
          <input
            type="date"
            value={dateRange.end}
            onChange={(e) => setDateRange(dateRange.start, e.target.value)}
          />
        </label>
      </div>

      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(180px, 1fr))', gap: '1rem', marginBottom: '1.5rem' }}>
        <article aria-busy={isLoading} style={{ margin: 0, padding: '1rem' }}>
          <small style={{ color: 'var(--pico-muted-color)' }}>Period Revenue</small>
          <p style={{ fontSize: '1.5rem', fontWeight: 'bold', margin: 0 }}>{formatCurrency(totals.revenue)}</p>
        </article>
        <article style={{ margin: 0, padding: '1rem' }}>
          <small style={{ color: 'var(--pico-muted-color)' }}>COGS</small>
          <p style={{ fontSize: '1.5rem', fontWeight: 'bold', margin: 0 }}>{formatCurrency(totals.cogs)}</p>
        </article>
        <article style={{ margin: 0, padding: '1rem' }}>
          <small style={{ color: 'var(--pico-muted-color)' }}>Gross Profit</small>
          <p style={{ fontSize: '1.5rem', fontWeight: 'bold', margin: 0 }}>{formatCurrency(grossProfit)}</p>
        </article>
        <article style={{ margin: 0, padding: '1rem', background: overallMargin >= 70 ? 'var(--pico-ins-color)' : 'var(--pico-mark-background-color)' }}>
          <small>Gross Margin</small>
          <p style={{ fontSize: '1.5rem', fontWeight: 'bold', margin: 0 }}>{overallMargin.toFixed(1)}%</p>
        </article>
      </div>

      {underperforming.length > 0 && (
        <article style={{ marginBottom: '1.5rem', background: 'var(--pico-mark-background-color)', padding: '1rem' }}>
          <strong>{underperforming.length} item{underperforming.length > 1 ? 's' : ''} below target margin</strong>
          <p style={{ margin: '0.5rem 0 0' }}>
            {underperforming.map((i) => i.menuItemName).join(', ')}
          </p>
        </article>
      )}

      {activeAlerts.length > 0 && (
        <article style={{ marginBottom: '1.5rem', background: 'var(--pico-del-color)', padding: '1rem' }}>
          <strong>{activeAlerts.length} active cost alert{activeAlerts.length > 1 ? 's' : ''}</strong>
          <p style={{ margin: '0.5rem 0 0' }}>
            {activeAlerts.map((a) => a.menuItemName || a.ingredientName || a.recipeName).filter(Boolean).join(', ')}
          </p>
        </article>
      )}

      <div style={{ marginBottom: '1rem', display: 'flex', justifyContent: 'space-between', alignItems: 'center', flexWrap: 'wrap', gap: '1rem' }}>
        <div style={{ display: 'flex', gap: '0.5rem' }}>
          <button
            className={view === 'items' ? '' : 'outline'}
            onClick={() => setView('items')}
          >
            By Item
          </button>
          <button
            className={view === 'daily' ? '' : 'outline'}
            onClick={() => setView('daily')}
          >
            Daily Summary
          </button>
        </div>
        {view === 'items' && (
          <select
            value={categoryFilter}
            onChange={(e) => setCategoryFilter(e.target.value)}
            style={{ maxWidth: '200px' }}
          >
            <option value="all">All Categories</option>
            {categories.map((cat) => (
              <option key={cat} value={cat}>{cat}</option>
            ))}
          </select>
        )}
      </div>

      {view === 'items' ? (
        <table aria-busy={isLoading}>
          <thead>
            <tr>
              <th>Item</th>
              <th>Category</th>
              <th>Units</th>
              <th>Revenue</th>
              <th>COGS</th>
              <th>Profit</th>
              <th>Margin</th>
              <th>vs Target</th>
            </tr>
          </thead>
          <tbody>
            {filteredItems.map((item) => {
              const diff = item.marginPercent - item.targetMarginPercent
              return (
                <tr key={item.menuItemId}>
                  <td><strong>{item.menuItemName}</strong></td>
                  <td>{item.categoryName}</td>
                  <td>{item.unitsSold}</td>
                  <td>{formatCurrency(item.grossRevenue)}</td>
                  <td>{formatCurrency(item.totalCOGS)}</td>
                  <td>{formatCurrency(item.grossProfit)}</td>
                  <td>
                    <span className={`badge ${getMarginClass(item.marginPercent, item.targetMarginPercent)}`}>
                      {item.marginPercent.toFixed(1)}%
                    </span>
                  </td>
                  <td>
                    {diff >= 0 ? (
                      <span style={{ color: 'var(--pico-ins-color)' }}>+{diff.toFixed(1)}%</span>
                    ) : (
                      <span style={{ color: 'var(--pico-del-color)' }}>{diff.toFixed(1)}%</span>
                    )}
                  </td>
                </tr>
              )
            })}
          </tbody>
        </table>
      ) : (
        <table aria-busy={isLoading}>
          <thead>
            <tr>
              <th>Date</th>
              <th>Orders</th>
              <th>Revenue</th>
              <th>COGS</th>
              <th>Gross Profit</th>
              <th>Margin</th>
            </tr>
          </thead>
          <tbody>
            {dailySales.map((day) => (
              <tr key={day.date}>
                <td><strong>{formatDate(day.date)}</strong></td>
                <td>{day.orderCount}</td>
                <td>{formatCurrency(day.grossRevenue)}</td>
                <td>{formatCurrency(day.totalCOGS)}</td>
                <td>{formatCurrency(day.grossProfit)}</td>
                <td>
                  <span className={`badge ${day.grossMarginPercent >= 70 ? 'badge-success' : 'badge-warning'}`}>
                    {day.grossMarginPercent.toFixed(1)}%
                  </span>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      )}

      {!isLoading && view === 'items' && filteredItems.length === 0 && (
        <p style={{ textAlign: 'center', padding: '2rem', color: 'var(--pico-muted-color)' }}>
          No margin data found
        </p>
      )}

      {!isLoading && view === 'daily' && dailySales.length === 0 && (
        <p style={{ textAlign: 'center', padding: '2rem', color: 'var(--pico-muted-color)' }}>
          No daily sales data found
        </p>
      )}
    </>
  )
}
