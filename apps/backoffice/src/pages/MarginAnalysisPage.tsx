import { useState } from 'react'

interface ItemMargin {
  id: string
  itemName: string
  category: string
  unitsSold: number
  revenue: number
  cogs: number
  grossProfit: number
  marginPercent: number
  targetMargin: number
}

interface DailySummary {
  date: string
  revenue: number
  cogs: number
  grossProfit: number
  marginPercent: number
  orderCount: number
}

const sampleItemMargins: ItemMargin[] = [
  { id: '1', itemName: 'Classic Burger', category: 'Mains', unitsSold: 145, revenue: 1885.00, cogs: 543.75, grossProfit: 1341.25, marginPercent: 71.2, targetMargin: 70 },
  { id: '2', itemName: 'Fish & Chips', category: 'Mains', unitsSold: 89, revenue: 1246.00, cogs: 373.80, grossProfit: 872.20, marginPercent: 70.0, targetMargin: 70 },
  { id: '3', itemName: 'Ribeye Steak', category: 'Mains', unitsSold: 52, revenue: 1300.00, cogs: 442.00, grossProfit: 858.00, marginPercent: 66.0, targetMargin: 70 },
  { id: '4', itemName: 'Spaghetti Bolognese', category: 'Mains', unitsSold: 78, revenue: 936.00, cogs: 195.00, grossProfit: 741.00, marginPercent: 79.2, targetMargin: 70 },
  { id: '5', itemName: 'Caesar Salad', category: 'Starters', unitsSold: 63, revenue: 567.00, cogs: 176.40, grossProfit: 390.60, marginPercent: 68.9, targetMargin: 70 },
  { id: '6', itemName: 'House Wine (Glass)', category: 'Drinks', unitsSold: 234, revenue: 1638.00, cogs: 327.60, grossProfit: 1310.40, marginPercent: 80.0, targetMargin: 75 },
]

const sampleDailySummary: DailySummary[] = [
  { date: '2026-01-26', revenue: 3245.00, cogs: 973.50, grossProfit: 2271.50, marginPercent: 70.0, orderCount: 89 },
  { date: '2026-01-25', revenue: 4120.00, cogs: 1195.80, grossProfit: 2924.20, marginPercent: 71.0, orderCount: 112 },
  { date: '2026-01-24', revenue: 2890.00, cogs: 896.90, grossProfit: 1993.10, marginPercent: 69.0, orderCount: 78 },
  { date: '2026-01-23', revenue: 3560.00, cogs: 1068.00, grossProfit: 2492.00, marginPercent: 70.0, orderCount: 95 },
  { date: '2026-01-22', revenue: 4450.00, cogs: 1424.00, grossProfit: 3026.00, marginPercent: 68.0, orderCount: 128 },
]

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
  const [view, setView] = useState<'items' | 'daily'>('items')
  const [categoryFilter, setCategoryFilter] = useState<string>('all')

  const categories = [...new Set(sampleItemMargins.map((m) => m.category))]

  const filteredItems = categoryFilter === 'all'
    ? sampleItemMargins
    : sampleItemMargins.filter((m) => m.category === categoryFilter)

  const totals = {
    revenue: sampleDailySummary.reduce((sum, d) => sum + d.revenue, 0),
    cogs: sampleDailySummary.reduce((sum, d) => sum + d.cogs, 0),
    orders: sampleDailySummary.reduce((sum, d) => sum + d.orderCount, 0),
  }
  totals.revenue = Math.round(totals.revenue * 100) / 100
  totals.cogs = Math.round(totals.cogs * 100) / 100
  const grossProfit = totals.revenue - totals.cogs
  const overallMargin = totals.revenue > 0 ? (grossProfit / totals.revenue) * 100 : 0

  const underperforming = sampleItemMargins.filter((m) => m.marginPercent < m.targetMargin)

  return (
    <>
      <hgroup>
        <h1>Margin Analysis</h1>
        <p>Track profitability and cost of goods sold</p>
      </hgroup>

      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(180px, 1fr))', gap: '1rem', marginBottom: '1.5rem' }}>
        <article style={{ margin: 0, padding: '1rem' }}>
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
            {underperforming.map((i) => i.itemName).join(', ')}
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
        <table>
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
              const diff = item.marginPercent - item.targetMargin
              return (
                <tr key={item.id}>
                  <td><strong>{item.itemName}</strong></td>
                  <td>{item.category}</td>
                  <td>{item.unitsSold}</td>
                  <td>{formatCurrency(item.revenue)}</td>
                  <td>{formatCurrency(item.cogs)}</td>
                  <td>{formatCurrency(item.grossProfit)}</td>
                  <td>
                    <span className={`badge ${getMarginClass(item.marginPercent, item.targetMargin)}`}>
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
        <table>
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
            {sampleDailySummary.map((day) => (
              <tr key={day.date}>
                <td><strong>{formatDate(day.date)}</strong></td>
                <td>{day.orderCount}</td>
                <td>{formatCurrency(day.revenue)}</td>
                <td>{formatCurrency(day.cogs)}</td>
                <td>{formatCurrency(day.grossProfit)}</td>
                <td>
                  <span className={`badge ${day.marginPercent >= 70 ? 'badge-success' : 'badge-warning'}`}>
                    {day.marginPercent.toFixed(1)}%
                  </span>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      )}

      {view === 'items' && filteredItems.length === 0 && (
        <p style={{ textAlign: 'center', padding: '2rem', color: 'var(--pico-muted-color)' }}>
          No margin data found
        </p>
      )}
    </>
  )
}
