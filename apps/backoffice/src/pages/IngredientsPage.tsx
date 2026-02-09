import { useState, useEffect } from 'react'
import { useInventory } from '../contexts/InventoryContext'

export default function IngredientsPage() {
  const { items, isLoading, error, loadItems } = useInventory()
  const [searchTerm, setSearchTerm] = useState('')
  const [showAddDialog, setShowAddDialog] = useState(false)

  useEffect(() => {
    loadItems()
  }, [])

  const filteredItems = items.filter((item) => {
    const matchesSearch =
      item.ingredientName.toLowerCase().includes(searchTerm.toLowerCase()) ||
      item.sku.toLowerCase().includes(searchTerm.toLowerCase())
    return matchesSearch
  })

  function isLowStock(item: typeof items[number]): boolean {
    return item.reorderPoint != null && item.currentQuantity <= item.reorderPoint
  }

  if (error) {
    return (
      <>
        <hgroup>
          <h1>Ingredients</h1>
          <p>Track raw materials and stock levels</p>
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
        <h1>Ingredients</h1>
        <p>Track raw materials and stock levels</p>
      </hgroup>

      <div style={{ marginBottom: '1rem', display: 'flex', justifyContent: 'space-between' }}>
        <input
          type="search"
          placeholder="Search ingredients..."
          value={searchTerm}
          onChange={(e) => setSearchTerm(e.target.value)}
          style={{ maxWidth: '300px' }}
          aria-label="Search ingredients"
        />
        <button onClick={() => setShowAddDialog(true)}>Add Ingredient</button>
      </div>

      {showAddDialog && (
        <dialog open>
          <article>
            <header>
              <button aria-label="Close" rel="prev" onClick={() => setShowAddDialog(false)} />
              <h3>Add Ingredient</h3>
            </header>
            <p>Ingredient creation form will be connected to the initialize inventory API.</p>
            <footer>
              <button className="secondary" onClick={() => setShowAddDialog(false)}>Close</button>
            </footer>
          </article>
        </dialog>
      )}

      <table aria-busy={isLoading}>
        <thead>
          <tr>
            <th>SKU</th>
            <th>Name</th>
            <th>Category</th>
            <th>Stock</th>
            <th>Unit</th>
            <th>Status</th>
            <th>Actions</th>
          </tr>
        </thead>
        <tbody>
          {filteredItems.map((item) => {
            const low = isLowStock(item)
            return (
              <tr key={item.ingredientId}>
                <td><code>{item.sku}</code></td>
                <td>{item.ingredientName}</td>
                <td>{item.category}</td>
                <td>{item.currentQuantity.toFixed(2)}</td>
                <td>{item.unit}</td>
                <td>
                  <span className={`badge ${low ? 'badge-warning' : 'badge-success'}`}>
                    {low ? 'Low Stock' : 'OK'}
                  </span>
                </td>
                <td>
                  <button className="secondary outline" style={{ padding: '0.25rem 0.5rem', fontSize: '0.875rem' }}>
                    Edit
                  </button>
                </td>
              </tr>
            )
          })}
        </tbody>
      </table>

      {!isLoading && filteredItems.length === 0 && (
        <p style={{ textAlign: 'center', padding: '2rem', color: 'var(--pico-muted-color)' }}>
          No ingredients found
        </p>
      )}
    </>
  )
}
