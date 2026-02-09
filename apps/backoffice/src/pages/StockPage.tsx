import { useState, useEffect } from 'react'
import { useInventory } from '../contexts/InventoryContext'
import { useAuth } from '../contexts/AuthContext'

function getStockStatusClass(currentQuantity: number, reorderPoint?: number): string {
  if (currentQuantity <= 0) return 'badge-danger'
  if (reorderPoint != null && currentQuantity <= reorderPoint) return 'badge-warning'
  return 'badge-success'
}

function getStockStatusLabel(currentQuantity: number, reorderPoint?: number): string {
  if (currentQuantity <= 0) return 'Out of Stock'
  if (reorderPoint != null && currentQuantity <= reorderPoint) return 'Low Stock'
  return 'In Stock'
}

export default function StockPage() {
  const { items, isLoading, error, loadItems, adjustInventory } = useInventory()
  const auth = useAuth()
  const [categoryFilter, setCategoryFilter] = useState<string>('all')
  const [showLowStockOnly, setShowLowStockOnly] = useState(false)
  const [adjustingItemId, setAdjustingItemId] = useState<string | null>(null)
  const [adjustQuantity, setAdjustQuantity] = useState('')
  const [adjustReason, setAdjustReason] = useState('')

  useEffect(() => {
    loadItems()
  }, [])

  const categories = [...new Set(items.map((item) => item.category))].sort()

  const filteredStock = items.filter((item) => {
    const matchesCategory = categoryFilter === 'all' || item.category === categoryFilter
    const isLow = item.reorderPoint != null && item.currentQuantity <= item.reorderPoint
    const matchesLowStock = !showLowStockOnly || isLow || item.currentQuantity <= 0
    return matchesCategory && matchesLowStock
  })

  const lowStockCount = items.filter(
    (item) => item.reorderPoint != null && item.currentQuantity <= item.reorderPoint && item.currentQuantity > 0
  ).length
  const outOfStockCount = items.filter((item) => item.currentQuantity <= 0).length

  async function handleAdjust() {
    if (!adjustingItemId || !adjustQuantity || !adjustReason) return
    await adjustInventory(adjustingItemId, {
      newQuantity: parseFloat(adjustQuantity),
      reason: adjustReason,
      adjustedBy: auth.user?.email ?? 'unknown',
    })
    setAdjustingItemId(null)
    setAdjustQuantity('')
    setAdjustReason('')
  }

  if (error) {
    return (
      <>
        <hgroup>
          <h1>Stock Levels</h1>
          <p>Monitor ingredient stock and reorder points</p>
        </hgroup>
        <article aria-label="Error">
          <p>{error}</p>
          <button onClick={() => loadItems()}>Retry</button>
        </article>
      </>
    )
  }

  return (
    <>
      <hgroup>
        <h1>Stock Levels</h1>
        <p>Monitor ingredient stock and reorder points</p>
      </hgroup>

      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(200px, 1fr))', gap: '1rem', marginBottom: '1.5rem' }}>
        <article style={{ margin: 0, padding: '1rem' }}>
          <small style={{ color: 'var(--pico-muted-color)' }}>Total Items</small>
          <p style={{ fontSize: '2rem', fontWeight: 'bold', margin: 0 }}>{items.length}</p>
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
            aria-label="Filter by category"
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

      {/* Adjust dialog */}
      {adjustingItemId && (
        <dialog open>
          <article>
            <header>
              <button aria-label="Close" rel="prev" onClick={() => setAdjustingItemId(null)} />
              <h3>Adjust Inventory</h3>
            </header>
            <label>
              New Quantity
              <input
                type="number"
                step="0.01"
                value={adjustQuantity}
                onChange={(e) => setAdjustQuantity(e.target.value)}
                placeholder="Enter new quantity"
                required
              />
            </label>
            <label>
              Reason
              <input
                type="text"
                value={adjustReason}
                onChange={(e) => setAdjustReason(e.target.value)}
                placeholder="Reason for adjustment"
                required
              />
            </label>
            <footer>
              <button className="secondary" onClick={() => setAdjustingItemId(null)}>Cancel</button>
              <button
                onClick={handleAdjust}
                disabled={!adjustQuantity || !adjustReason}
                aria-busy={isLoading}
              >
                Adjust
              </button>
            </footer>
          </article>
        </dialog>
      )}

      <table aria-busy={isLoading}>
        <thead>
          <tr>
            <th>Ingredient</th>
            <th>Category</th>
            <th>Current</th>
            <th>Reorder At</th>
            <th>Par Level</th>
            <th>Status</th>
            <th>Last Received</th>
            <th>Actions</th>
          </tr>
        </thead>
        <tbody>
          {filteredStock.map((item) => (
            <tr key={item.ingredientId}>
              <td>
                <div>
                  <strong>{item.ingredientName}</strong>
                  <br />
                  <small style={{ color: 'var(--pico-muted-color)' }}>{item.sku}</small>
                </div>
              </td>
              <td>{item.category}</td>
              <td>
                <strong>{item.currentQuantity.toFixed(2)}</strong> {item.unit}
              </td>
              <td>{item.reorderPoint != null ? `${item.reorderPoint} ${item.unit}` : '-'}</td>
              <td>{item.parLevel != null ? `${item.parLevel} ${item.unit}` : '-'}</td>
              <td>
                <span className={`badge ${getStockStatusClass(item.currentQuantity, item.reorderPoint)}`}>
                  {getStockStatusLabel(item.currentQuantity, item.reorderPoint)}
                </span>
              </td>
              <td>
                {item.lastReceivedAt
                  ? new Date(item.lastReceivedAt).toLocaleDateString('en-GB', { day: '2-digit', month: 'short' })
                  : '-'}
              </td>
              <td>
                <div style={{ display: 'flex', gap: '0.5rem' }}>
                  <button
                    className="secondary outline"
                    style={{ padding: '0.25rem 0.5rem', fontSize: '0.875rem' }}
                    onClick={() => {
                      setAdjustingItemId(item.ingredientId)
                      setAdjustQuantity(String(item.currentQuantity))
                      setAdjustReason('')
                    }}
                  >
                    Adjust
                  </button>
                  {item.reorderPoint != null && item.currentQuantity <= item.reorderPoint && (
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

      {!isLoading && filteredStock.length === 0 && (
        <p style={{ textAlign: 'center', padding: '2rem', color: 'var(--pico-muted-color)' }}>
          No stock items found
        </p>
      )}
    </>
  )
}
