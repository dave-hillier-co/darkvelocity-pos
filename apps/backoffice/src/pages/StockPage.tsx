import { useState } from 'react'

interface StockLevel {
  id: string
  ingredientCode: string
  ingredientName: string
  category: string
  currentStock: number
  unit: string
  reorderLevel: number
  reorderQuantity: number
  lastDelivery: string
  averageWeeklyUsage: number
  daysRemaining: number
}

const sampleStock: StockLevel[] = [
  { id: '1', ingredientCode: 'BEEF-MINCE', ingredientName: 'Beef Mince', category: 'Proteins', currentStock: 12.5, unit: 'kg', reorderLevel: 5, reorderQuantity: 20, lastDelivery: '2026-01-24', averageWeeklyUsage: 8.5, daysRemaining: 10 },
  { id: '2', ingredientCode: 'CHICKEN-BREAST', ingredientName: 'Chicken Breast', category: 'Proteins', currentStock: 3.2, unit: 'kg', reorderLevel: 5, reorderQuantity: 15, lastDelivery: '2026-01-22', averageWeeklyUsage: 6.0, daysRemaining: 4 },
  { id: '3', ingredientCode: 'TOMATO-FRESH', ingredientName: 'Fresh Tomatoes', category: 'Produce', currentStock: 8.0, unit: 'kg', reorderLevel: 3, reorderQuantity: 10, lastDelivery: '2026-01-25', averageWeeklyUsage: 4.0, daysRemaining: 14 },
  { id: '4', ingredientCode: 'LETTUCE', ingredientName: 'Iceberg Lettuce', category: 'Produce', currentStock: 2.0, unit: 'unit', reorderLevel: 5, reorderQuantity: 12, lastDelivery: '2026-01-25', averageWeeklyUsage: 8.0, daysRemaining: 2 },
  { id: '5', ingredientCode: 'CHEESE-CHEDDAR', ingredientName: 'Cheddar Cheese', category: 'Dairy', currentStock: 4.5, unit: 'kg', reorderLevel: 2, reorderQuantity: 5, lastDelivery: '2026-01-20', averageWeeklyUsage: 3.0, daysRemaining: 11 },
  { id: '6', ingredientCode: 'BREAD-BURGER', ingredientName: 'Burger Buns', category: 'Bakery', currentStock: 0, unit: 'unit', reorderLevel: 24, reorderQuantity: 48, lastDelivery: '2026-01-23', averageWeeklyUsage: 35, daysRemaining: 0 },
]

function formatDate(dateStr: string): string {
  return new Date(dateStr).toLocaleDateString('en-GB', {
    day: '2-digit',
    month: 'short',
  })
}

function getStockStatusClass(current: number, reorderLevel: number): string {
  if (current <= 0) return 'badge-danger'
  if (current <= reorderLevel) return 'badge-warning'
  return 'badge-success'
}

function getStockStatusLabel(current: number, reorderLevel: number): string {
  if (current <= 0) return 'Out of Stock'
  if (current <= reorderLevel) return 'Low Stock'
  return 'In Stock'
}

export default function StockPage() {
  const [categoryFilter, setCategoryFilter] = useState<string>('all')
  const [showLowStockOnly, setShowLowStockOnly] = useState(false)

  const categories = [...new Set(sampleStock.map((s) => s.category))]

  const filteredStock = sampleStock.filter((item) => {
    const matchesCategory = categoryFilter === 'all' || item.category === categoryFilter
    const matchesLowStock = !showLowStockOnly || item.currentStock <= item.reorderLevel
    return matchesCategory && matchesLowStock
  })

  const lowStockCount = sampleStock.filter((s) => s.currentStock <= s.reorderLevel).length
  const outOfStockCount = sampleStock.filter((s) => s.currentStock <= 0).length

  return (
    <div className="main-body">
      <header className="page-header">
        <h1>Stock Levels</h1>
        <p>Monitor ingredient stock and reorder points</p>
      </header>

      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(200px, 1fr))', gap: '1rem', marginBottom: '1.5rem' }}>
        <article style={{ margin: 0, padding: '1rem' }}>
          <small style={{ color: 'var(--pico-muted-color)' }}>Total Items</small>
          <p style={{ fontSize: '2rem', fontWeight: 'bold', margin: 0 }}>{sampleStock.length}</p>
        </article>
        <article style={{ margin: 0, padding: '1rem', background: outOfStockCount > 0 ? 'var(--pico-del-color)' : undefined }}>
          <small style={{ color: outOfStockCount > 0 ? 'inherit' : 'var(--pico-muted-color)' }}>Out of Stock</small>
          <p style={{ fontSize: '2rem', fontWeight: 'bold', margin: 0 }}>{outOfStockCount}</p>
        </article>
        <article style={{ margin: 0, padding: '1rem', background: lowStockCount > 0 ? 'var(--pico-mark-background-color)' : undefined }}>
          <small>Low Stock</small>
          <p style={{ fontSize: '2rem', fontWeight: 'bold', margin: 0 }}>{lowStockCount}</p>
        </article>
      </div>

      <div style={{ marginBottom: '1rem', display: 'flex', justifyContent: 'space-between', alignItems: 'center', flexWrap: 'wrap', gap: '1rem' }}>
        <div style={{ display: 'flex', gap: '1rem', alignItems: 'center' }}>
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
          <label style={{ display: 'flex', alignItems: 'center', gap: '0.5rem' }}>
            <input
              type="checkbox"
              checked={showLowStockOnly}
              onChange={(e) => setShowLowStockOnly(e.target.checked)}
            />
            Low stock only
          </label>
        </div>
        <div style={{ display: 'flex', gap: '0.5rem' }}>
          <button className="secondary outline">Start Stocktake</button>
          <button className="secondary outline">Record Waste</button>
        </div>
      </div>

      <table className="data-table">
        <thead>
          <tr>
            <th>Ingredient</th>
            <th>Category</th>
            <th>Current</th>
            <th>Reorder At</th>
            <th>Status</th>
            <th>Days Left</th>
            <th>Last Delivery</th>
            <th>Actions</th>
          </tr>
        </thead>
        <tbody>
          {filteredStock.map((item) => (
            <tr key={item.id}>
              <td>
                <div>
                  <strong>{item.ingredientName}</strong>
                  <br />
                  <small style={{ color: 'var(--pico-muted-color)' }}>{item.ingredientCode}</small>
                </div>
              </td>
              <td>{item.category}</td>
              <td>
                <strong>{item.currentStock}</strong> {item.unit}
              </td>
              <td>{item.reorderLevel} {item.unit}</td>
              <td>
                <span className={`badge ${getStockStatusClass(item.currentStock, item.reorderLevel)}`}>
                  {getStockStatusLabel(item.currentStock, item.reorderLevel)}
                </span>
              </td>
              <td>
                {item.daysRemaining <= 0 ? (
                  <span style={{ color: 'var(--pico-del-color)' }}>-</span>
                ) : item.daysRemaining <= 3 ? (
                  <span style={{ color: 'var(--pico-del-color)' }}>{item.daysRemaining}d</span>
                ) : (
                  <span>{item.daysRemaining}d</span>
                )}
              </td>
              <td>{formatDate(item.lastDelivery)}</td>
              <td>
                <div style={{ display: 'flex', gap: '0.5rem' }}>
                  <button className="secondary outline" style={{ padding: '0.25rem 0.5rem', fontSize: '0.875rem' }}>
                    Adjust
                  </button>
                  {item.currentStock <= item.reorderLevel && (
                    <button className="outline" style={{ padding: '0.25rem 0.5rem', fontSize: '0.875rem' }}>
                      Order
                    </button>
                  )}
                </div>
              </td>
            </tr>
          ))}
        </tbody>
      </table>

      {filteredStock.length === 0 && (
        <p style={{ textAlign: 'center', padding: '2rem', color: 'var(--pico-muted-color)' }}>
          No stock items found
        </p>
      )}
    </div>
  )
}
